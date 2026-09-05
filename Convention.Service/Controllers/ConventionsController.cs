using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smartek.Common.Errors;
using Smartek.Common.Pagination;
using Smartek.Common.Security;
using Smartek.Common.Storage;
using Convention.Service.Data;
using Convention.Service.DTOs;
using Convention.Service.Pdf;
using Convention.Service.Services;
using Stagiaire.Contracts.Events;
using ConventionEntity = Convention.Service.Models.Convention;
using MassTransit;

namespace Convention.Service.Controllers;

[ApiController]
[Route("api/v1/conventions")]
[Produces("application/json")]
// Defence in depth. The gateway already restricts /api/v1/conventions/** to an ADMIN for writes and
// to authenticated users for reads; a direct call on :5071 would otherwise bypass all of it.
[Authorize]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
public class ConventionsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IFileStorage _fileStorage;
    private readonly IConventionPdfGenerator _pdfGenerator;
    private readonly IAuditService _auditService;

    public ConventionsController(
        AppDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        IFileStorage fileStorage,
        IConventionPdfGenerator pdfGenerator,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _fileStorage = fileStorage;
        _pdfGenerator = pdfGenerator;
        _auditService = auditService;
    }

    /// <summary>
    /// Lists conventions, most recently generated first, paged and filterable.
    /// </summary>
    /// <remarks>
    /// Scoped by role: a TRAINER only sees conventions of stagiaires assigned to them, and a LEARNER
    /// only their own — enforced from the JWT, so the <c>stagiaireId</c> query parameter cannot be
    /// used to read someone else's convention.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ConventionReadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ConventionReadDto>>> GetAll(
        [FromQuery] ConventionQueryParameters query,
        CancellationToken cancellationToken)
    {
        var source = ApplyVisibilityScope(_dbContext.Conventions.AsNoTracking());

        if (query.StatutSignature is { } statut)
        {
            source = source.Where(x => x.StatutSignature == statut);
        }

        if (query.StagiaireId is { } stagiaireId)
        {
            source = source.Where(x => x.StagiaireId == stagiaireId);
        }

        var page = await source
            .OrderByDescending(x => x.DateGeneration)
            .ThenBy(x => x.Id)
            .Select(ToReadDtoProjection())
            .ToPagedResultAsync(query, cancellationToken);

        return Ok(page);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ConventionReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConventionReadDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        // Scoped first, so an out-of-scope id is indistinguishable from a nonexistent one.
        var item = await ApplyVisibilityScope(_dbContext.Conventions.AsNoTracking())
            .Where(x => x.Id == id)
            .Select(ToReadDtoProjection())
            .SingleOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            throw NotFoundException.For("La convention", id);
        }

        return Ok(item);
    }

    /// <summary>
    /// Manually creates a convention (normally a draft is auto-created when a candidature is accepted,
    /// via the <c>CandidatureAcceptedConsumer</c>). The PDF is generated separately (POST /{id}/generer).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ConventionReadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConventionReadDto>> Create(
        [FromBody] ConventionCreateDto dto,
        CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Conventions
            .AsNoTracking()
            .AnyAsync(x => x.StagiaireId == dto.StagiaireId, cancellationToken);

        if (exists)
        {
            throw new ConflictException(
                $"Une convention existe déjà pour le stagiaire '{dto.StagiaireId}'.");
        }

        var entity = new ConventionEntity
        {
            Id = Guid.NewGuid(),
            StagiaireId = dto.StagiaireId,
            UtilisateurId = dto.UtilisateurId,
            EncadrantId = dto.EncadrantId,
            StagiaireNom = dto.StagiaireNom.Trim(),
            StagiairePrenom = dto.StagiairePrenom.Trim(),
            StagiaireEmail = dto.StagiaireEmail.Trim(),
            Departement = dto.Departement.Trim(),
            DateDebut = dto.DateDebut,
            DateFin = dto.DateFin,
            DateGeneration = dto.DateGeneration,
            StatutSignature = dto.StatutSignature,
            CheminPdf = dto.CheminPdf?.Trim() ?? string.Empty
        };

        _dbContext.Conventions.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // No ConventionGenerated here: creating the row is not generating the document. This used to
        // publish with an empty CheminPdf, so Notification.Service announced a convention the
        // stagiaire could not download. The event is published by Generer below, once a PDF exists.
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToReadDto(entity));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] ConventionUpdateDto dto,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Conventions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            throw NotFoundException.For("La convention", id);
        }

        entity.StagiaireId = dto.StagiaireId;
        // UtilisateurId / EncadrantId are intentionally not updated here — see ConventionUpdateDto.
        entity.StagiaireNom = dto.StagiaireNom.Trim();
        entity.StagiairePrenom = dto.StagiairePrenom.Trim();
        entity.StagiaireEmail = dto.StagiaireEmail.Trim();
        entity.Departement = dto.Departement.Trim();
        entity.DateDebut = dto.DateDebut;
        entity.DateFin = dto.DateFin;
        entity.DateGeneration = dto.DateGeneration;
        entity.StatutSignature = dto.StatutSignature;
        entity.CheminPdf = dto.CheminPdf?.Trim() ?? entity.CheminPdf;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Conventions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            throw NotFoundException.For("La convention", id);
        }

        // Drop the PDF blob alongside the row so the volume does not accumulate orphans.
        if (!string.IsNullOrWhiteSpace(entity.CheminPdf))
        {
            _fileStorage.Delete(entity.CheminPdf);
        }

        _dbContext.Conventions.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Generates the convention PDF from the persisted stagiaire details and stores it on the
    /// storage volume. Overwrites the previous PDF if one existed.
    /// </summary>
    [HttpPost("{id:guid}/generer")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ConventionReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConventionReadDto>> Generer(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Conventions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFoundException.For("La convention", id);

        byte[] pdf = _pdfGenerator.Generate(entity);

        await using var stream = new MemoryStream(pdf);
        var relativePath = await _fileStorage.SaveGeneratedAsync(stream, "conventions", ".pdf", cancellationToken);

        // Replacing: drop the previous blob so the volume does not accumulate orphans.
        if (!string.IsNullOrWhiteSpace(entity.CheminPdf))
        {
            _fileStorage.Delete(entity.CheminPdf);
        }

        entity.CheminPdf = relativePath;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Published here, not on create: this is the point at which a downloadable document exists.
        // Notification.Service consumes it to email the stagiaire their convention.
        await _publishEndpoint.Publish(new ConventionGenerated(
            entity.Id,
            entity.StagiaireId,
            entity.StagiaireNom,
            entity.StagiairePrenom,
            entity.StagiaireEmail,
            entity.UtilisateurId,
            entity.DateGeneration,
            ToEventStatutSignature(entity.StatutSignature),
            entity.CheminPdf
        ), cancellationToken);

        await _auditService.LogAsync(User, "CONVENTION_GENERATED", "Convention",
            entity.Id.ToString(),
            $"Convention de {entity.StagiairePrenom} {entity.StagiaireNom} générée",
            cancellationToken);

        return Ok(ToReadDto(entity));
    }

    /// <summary>Downloads the generated convention PDF.</summary>
    [HttpGet("{id:guid}/pdf")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TelechargerPdf(Guid id, CancellationToken cancellationToken)
    {
        // Scoped: the PDF carries the stagiaire's name, email and département, so the download must
        // respect the same boundary as the list.
        var entity = await ApplyVisibilityScope(_dbContext.Conventions.AsNoTracking())
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFoundException.For("La convention", id);

        if (string.IsNullOrWhiteSpace(entity.CheminPdf))
        {
            throw new NotFoundException("La convention n'a pas encore de PDF généré.");
        }

        var stream = _fileStorage.OpenRead(entity.CheminPdf);
        var downloadName = $"convention-{entity.StagiairePrenom}-{entity.StagiaireNom}.pdf";
        return File(stream, "application/pdf", downloadName);
    }

    /// <summary>Marks the convention as signed — a simple admin action, no e-signature (decided).</summary>
    [HttpPost("{id:guid}/signer")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ConventionReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConventionReadDto>> Signer(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Conventions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFoundException.For("La convention", id);

        if (entity.StatutSignature == Convention.Service.Models.StatutSignature.Signee)
        {
            throw new ConflictException("La convention est déjà signée.");
        }

        entity.StatutSignature = Convention.Service.Models.StatutSignature.Signee;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(User, "CONVENTION_SIGNED", "Convention",
            entity.Id.ToString(),
            $"Convention de {entity.StagiairePrenom} {entity.StagiaireNom} signée",
            cancellationToken);

        return Ok(ToReadDto(entity));
    }

    /// <summary>
    /// Narrows a query to what the caller is entitled to see, before any client-supplied filter.
    /// </summary>
    /// <remarks>
    /// A convention exposes the stagiaire's name, email, département and dates, and its PDF contains
    /// all of it — so without this any authenticated learner could enumerate every convention in the
    /// bank. Mirrors <c>StagiairesController.ApplyVisibilityScope</c> in Stagiaire.Service.
    ///
    /// A caller whose token carries no userId sees nothing, which is the safe failure direction; the
    /// same applies to rows whose <c>UtilisateurId</c>/<c>EncadrantId</c> is null, including any
    /// written before those columns existed.
    /// </remarks>
    private IQueryable<ConventionEntity> ApplyVisibilityScope(IQueryable<ConventionEntity> source)
    {
        if (User.IsAdmin())
        {
            return source;
        }

        var callerId = User.GetUserId();

        if (callerId is null)
        {
            return source.Where(_ => false);
        }

        if (User.IsTrainer())
        {
            return source.Where(x => x.EncadrantId == callerId);
        }

        // LEARNER, or any other authenticated role: own convention only.
        return source.Where(x => x.UtilisateurId == callerId);
    }

    private static System.Linq.Expressions.Expression<Func<ConventionEntity, ConventionReadDto>> ToReadDtoProjection() =>
        x => new ConventionReadDto
        {
            Id = x.Id,
            StagiaireId = x.StagiaireId,
            UtilisateurId = x.UtilisateurId,
            EncadrantId = x.EncadrantId,
            StagiaireNom = x.StagiaireNom,
            StagiairePrenom = x.StagiairePrenom,
            StagiaireEmail = x.StagiaireEmail,
            Departement = x.Departement,
            DateDebut = x.DateDebut,
            DateFin = x.DateFin,
            DateGeneration = x.DateGeneration,
            StatutSignature = x.StatutSignature,
            PdfDisponible = !string.IsNullOrWhiteSpace(x.CheminPdf)
        };

    private static ConventionReadDto ToReadDto(ConventionEntity entity) => new()
    {
        Id = entity.Id,
        StagiaireId = entity.StagiaireId,
        UtilisateurId = entity.UtilisateurId,
        EncadrantId = entity.EncadrantId,
        StagiaireNom = entity.StagiaireNom,
        StagiairePrenom = entity.StagiairePrenom,
        StagiaireEmail = entity.StagiaireEmail,
        Departement = entity.Departement,
        DateDebut = entity.DateDebut,
        DateFin = entity.DateFin,
        DateGeneration = entity.DateGeneration,
        StatutSignature = entity.StatutSignature,
        PdfDisponible = !string.IsNullOrWhiteSpace(entity.CheminPdf)
    };

    /// <summary>
    /// Maps the model enum onto the contract enum by name.
    /// Avoids the fragile ordinal cast that previously existed here.
    /// </summary>
    private static Stagiaire.Contracts.Events.StatutSignature ToEventStatutSignature(
        Convention.Service.Models.StatutSignature statut) => statut switch
    {
        Convention.Service.Models.StatutSignature.EnAttente => Stagiaire.Contracts.Events.StatutSignature.EnAttente,
        Convention.Service.Models.StatutSignature.Signee => Stagiaire.Contracts.Events.StatutSignature.Signee,
        Convention.Service.Models.StatutSignature.Refusee => Stagiaire.Contracts.Events.StatutSignature.Refusee,
        _ => throw new InvalidOperationException($"StatutSignature non mappé : {statut}")
    };
}