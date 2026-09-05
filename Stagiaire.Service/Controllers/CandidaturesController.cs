using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smartek.Common.Errors;
using Smartek.Common.Security;
using Smartek.Common.Storage;
using Stagiaire.Service.Data;
using Stagiaire.Service.DTOs;
using Stagiaire.Service.Models;
using Stagiaire.Service.Security;
using Stagiaire.Contracts.Events;
using StagiaireEntity = Stagiaire.Service.Models.Stagiaire;
using MassTransit;
using Stagiaire.Service.Services;

namespace Stagiaire.Service.Controllers;

/// <summary>
/// Candidature lifecycle: a learner applies, an admin accepts or rejects.
///
/// Separate from <see cref="StagiairesController"/> (plain CRUD) because these are state
/// transitions with side effects — publishing events and, on acceptance, triggering the convention.
/// </summary>
[ApiController]
[Route("api/v1/candidatures")]
[Produces("application/json")]
// Defence in depth. The gateway already enforces per-route roles, but a request that reaches this
// service directly on :5070 would otherwise bypass all of it.
[Authorize]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
public class CandidaturesController : ControllerBase
{
    /// <summary>CV uploads: 5 MB, documents only.</summary>
    private const long MaxCvBytes = 5 * 1024 * 1024;

    private static readonly string[] AllowedCvExtensions = { ".pdf", ".doc", ".docx" };

    private readonly AppDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IFileStorage _fileStorage;
    private readonly IAuditService _auditService;
    private readonly ILogger<CandidaturesController> _logger;

    public CandidaturesController(
        AppDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        IFileStorage fileStorage,
        IAuditService auditService,
        ILogger<CandidaturesController> logger)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _fileStorage = fileStorage;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Submits a candidature. The caller becomes its owner.
    /// </summary>
    /// <remarks>
    /// Always created as <c>EnAttente</c>: status comes from this method, never the request body,
    /// so an applicant cannot self-approve.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = "LEARNER,ADMIN")]
    [ProducesResponseType(typeof(StagiaireReadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StagiaireReadDto>> Soumettre(
        [FromBody] CandidatureCreateDto dto,
        CancellationToken cancellationToken)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var callerId = User.GetUserId();

        var duplicate = await _dbContext.Stagiaires
            .AsNoTracking()
            .AnyAsync(x => x.Email == email, cancellationToken);

        if (duplicate)
        {
            throw new ConflictException(
                $"Une candidature existe déjà pour l'email '{email}'.");
        }

        // One open application per account, so a learner cannot flood the admin queue.
        if (callerId is not null)
        {
            var alreadyPending = await _dbContext.Stagiaires
                .AsNoTracking()
                .AnyAsync(
                    x => x.UtilisateurId == callerId && x.Statut == StatutStagiaire.EnAttente,
                    cancellationToken);

            if (alreadyPending)
            {
                throw new ConflictException(
                    "Vous avez déjà une candidature en attente de traitement.");
            }
        }

        var entity = new StagiaireEntity
        {
            Id = Guid.NewGuid(),
            UtilisateurId = callerId,
            Nom = dto.Nom.Trim(),
            Prenom = dto.Prenom.Trim(),
            Email = email,
            Departement = dto.Departement.Trim(),
            TypeStage = dto.TypeStage,
            Ecole = dto.Ecole.Trim(),
            DateDebut = dto.DateDebut,
            DateFin = dto.DateFin,
            Motivation = dto.Motivation?.Trim(),
            Statut = StatutStagiaire.EnAttente,
            DateSoumission = DateTime.UtcNow
        };

        _dbContext.Stagiaires.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Candidature {Id} submitted by user {UserId} ({Email})",
            entity.Id, callerId, email);

        await _auditService.LogAsync(User, "CANDIDATURE_SUBMITTED", "Stagiaire",
            entity.Id.ToString(),
            $"Candidature de {dto.Prenom} {dto.Nom} ({email})",
            cancellationToken);

        // No event here: a submission is not an acceptance. CandidatureAccepted is published by
        // Accepter below.
        return CreatedAtAction(
            nameof(StagiairesController.GetById),
            "Stagiaires",
            new { id = entity.Id },
            MapToReadDto(entity));
    }

    /// <summary>
    /// Attaches or replaces the CV of a candidature. Multipart form upload, field name <c>fichier</c>.
    /// </summary>
    [HttpPost("{id:guid}/cv")]
    [Authorize(Roles = "LEARNER,ADMIN")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(StagiaireReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StagiaireReadDto>> TeleverserCv(
        Guid id,
        [FromForm(Name = "fichier")] IFormFile fichier,
        CancellationToken cancellationToken)
    {
        var entity = await FindOwnedAsync(id, cancellationToken);

        var storedPath = await _fileStorage.SaveAsync(
            fichier, "cv", AllowedCvExtensions, MaxCvBytes, cancellationToken);

        // Replacing an existing CV: drop the old blob so the volume does not accumulate orphans.
        if (!string.IsNullOrWhiteSpace(entity.CvCheminFichier))
        {
            _fileStorage.Delete(entity.CvCheminFichier);
        }

        entity.CvCheminFichier = storedPath;
        entity.CvNomFichier = Path.GetFileName(fichier.FileName);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapToReadDto(entity));
    }

    /// <summary>Downloads the CV. Owner, assigned encadrant, or admin only.</summary>
    [HttpGet("{id:guid}/cv")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TelechargerCv(Guid id, CancellationToken cancellationToken)
    {
        var entity = await FindVisibleAsync(id, cancellationToken);

        if (string.IsNullOrWhiteSpace(entity.CvCheminFichier))
        {
            throw new NotFoundException("Aucun CV n'a été déposé pour cette candidature.");
        }

        var stream = _fileStorage.OpenRead(entity.CvCheminFichier);
        var downloadName = entity.CvNomFichier ?? $"cv-{entity.Nom}{Path.GetExtension(entity.CvCheminFichier)}";        return File(stream, ContentTypeFor(entity.CvCheminFichier), downloadName);
    }

    /// <summary>
    /// Attaches or replaces the scanned candidature document (university form).
    /// Multipart form upload, field name <c>fichier</c>.
    /// </summary>
    [HttpPost("{id:guid}/document")]
    [Authorize(Roles = "LEARNER,ADMIN")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(StagiaireReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StagiaireReadDto>> TeleverserDocument(
        Guid id,
        [FromForm(Name = "fichier")] IFormFile fichier,
        CancellationToken cancellationToken)
    {
        var entity = await FindOwnedAsync(id, cancellationToken);

        var storedPath = await _fileStorage.SaveAsync(
            fichier, "document", AllowedCvExtensions, MaxCvBytes, cancellationToken);

        if (!string.IsNullOrWhiteSpace(entity.DocumentCheminFichier))
        {
            _fileStorage.Delete(entity.DocumentCheminFichier);
        }

        entity.DocumentCheminFichier = storedPath;
        entity.DocumentNomFichier = Path.GetFileName(fichier.FileName);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapToReadDto(entity));
    }

    /// <summary>Downloads the candidature document. Owner, assigned encadrant, or admin only.</summary>
    [HttpGet("{id:guid}/document")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TelechargerDocument(Guid id, CancellationToken cancellationToken)
    {
        var entity = await FindVisibleAsync(id, cancellationToken);

        if (string.IsNullOrWhiteSpace(entity.DocumentCheminFichier))
        {
            throw new NotFoundException("Aucun document de candidature n'a été déposé.");
        }

        var stream = _fileStorage.OpenRead(entity.DocumentCheminFichier);
        var downloadName = entity.DocumentNomFichier ?? $"candidature-{entity.Nom}{Path.GetExtension(entity.DocumentCheminFichier)}";

        return File(stream, ContentTypeFor(entity.DocumentCheminFichier), downloadName);
    }

    /// <summary>
    /// Accepts a candidature: assigns département and encadrant, publishes
    /// <c>CandidatureAccepted</c> so Convention.Service can draft the convention and
    /// Notification.Service can email the candidate.
    /// </summary>
    [HttpPost("{id:guid}/accepter")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(StagiaireReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StagiaireReadDto>> Accepter(
        Guid id,
        [FromBody] CandidatureAccepterDto dto,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Stagiaires.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFoundException.For("La candidature", id);

        EnsurePending(entity);

        if (!string.IsNullOrWhiteSpace(dto.Departement))
        {
            entity.Departement = dto.Departement.Trim();
        }

        if (dto.DateDebut is { } debut)
        {
            entity.DateDebut = debut;
        }

        if (dto.DateFin is { } fin)
        {
            entity.DateFin = fin;
        }

        if (entity.DateFin < entity.DateDebut)
        {
            throw new ValidationException(
                "dateFin", "La date de fin doit être postérieure ou égale à la date de début.");
        }

        entity.EncadrantId = dto.EncadrantId;
        entity.EncadrantNom = dto.EncadrantNom.Trim();
        entity.Statut = StatutStagiaire.Acceptee;
        entity.MotifRejet = null;
        entity.DateDecision = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish(new CandidatureAccepted(
            entity.Id,
            entity.Nom,
            entity.Prenom,
            entity.Email,
            entity.Departement,
            entity.DateDebut,
            entity.DateFin,
            entity.UtilisateurId,
            entity.EncadrantId,
            HttpContext.TraceIdentifier
        ), cancellationToken);

        _logger.LogInformation(
            "Candidature {Id} accepted; encadrant {EncadrantId}, département {Departement}",
            entity.Id, entity.EncadrantId, entity.Departement);

        await _auditService.LogAsync(User, "CANDIDATURE_ACCEPTED", "Stagiaire",
            entity.Id.ToString(),
            $"Candidature de {entity.Prenom} {entity.Nom} acceptée — encadrant: {entity.EncadrantNom ?? "N/A"}, département: {entity.Departement}",
            cancellationToken);

        return Ok(MapToReadDto(entity));
    }

    /// <summary>Rejects a candidature with a mandatory reason.</summary>
    [HttpPost("{id:guid}/rejeter")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(StagiaireReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StagiaireReadDto>> Rejeter(
        Guid id,
        [FromBody] CandidatureRejeterDto dto,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Stagiaires.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFoundException.For("La candidature", id);

        EnsurePending(entity);

        entity.Statut = StatutStagiaire.Rejetee;
        entity.MotifRejet = dto.MotifRejet.Trim();
        entity.EncadrantId = null;
        entity.EncadrantNom = null;
        entity.DateDecision = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Candidature {Id} rejected", entity.Id);

        await _auditService.LogAsync(User, "CANDIDATURE_REJECTED", "Stagiaire",
            entity.Id.ToString(),
            $"Candidature de {entity.Prenom} {entity.Nom} rejetée — motif: {entity.MotifRejet}",
            cancellationToken);

        await _publishEndpoint.Publish(new CandidatureRejected(
            entity.Id,
            entity.Nom,
            entity.Prenom,
            entity.Email,
            entity.MotifRejet ?? string.Empty,
            entity.UtilisateurId,
            HttpContext.TraceIdentifier
        ), cancellationToken);

        return Ok(MapToReadDto(entity));
    }

    /// <summary>
    /// The caller's own candidature — what the stagiaire dashboard reads on load.
    /// </summary>
    [HttpGet("moi")]
    [ProducesResponseType(typeof(StagiaireReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StagiaireReadDto>> MaCandidature(CancellationToken cancellationToken)
    {
        var callerId = User.GetUserId()
            ?? throw new BadRequestException("Le token ne contient pas d'identifiant utilisateur.");

        var entity = await _dbContext.Stagiaires
            .AsNoTracking()
            .Where(x => x.UtilisateurId == callerId)
            .OrderByDescending(x => x.DateSoumission)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            throw new NotFoundException("Vous n'avez pas encore soumis de candidature.");
        }

        return Ok(MapToReadDto(entity));
    }

    /// <summary>
    /// Rejects a transition out of a state that is no longer pending, so a decision cannot be
    /// silently overwritten by a second click.
    /// </summary>
    private static void EnsurePending(StagiaireEntity entity)
    {
        if (entity.Statut != StatutStagiaire.EnAttente)
        {
            throw new ConflictException(
                $"Cette candidature a déjà été traitée (statut actuel : {entity.Statut}).");
        }
    }

    /// <summary>
    /// Loads a candidature the caller is allowed to modify: an admin, or the learner who owns it.
    /// </summary>
    private async Task<StagiaireEntity> FindOwnedAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Stagiaires.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFoundException.For("La candidature", id);

        // 404 rather than 403: confirming that an id exists would leak the identifier space.
        if (!User.CanWrite(entity))
        {
            throw NotFoundException.For("La candidature", id);
        }

        return entity;
    }

    /// <summary>
    /// Loads a candidature the caller may read: admin, the owning learner, or the assigned encadrant.
    /// </summary>
    private async Task<StagiaireEntity> FindVisibleAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Stagiaires
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFoundException.For("La candidature", id);

        if (!User.CanRead(entity))
        {
            throw NotFoundException.For("La candidature", id);
        }

        return entity;
    }

    private static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        _ => "application/octet-stream"
    };

    internal static StagiaireReadDto MapToReadDto(StagiaireEntity s) => new()
    {
        Id = s.Id,
        UtilisateurId = s.UtilisateurId,
        Nom = s.Nom,
        Prenom = s.Prenom,
        Email = s.Email,
        Departement = s.Departement,
        TypeStage = s.TypeStage,
        Ecole = s.Ecole,
        DateDebut = s.DateDebut,
        DateFin = s.DateFin,
        Motivation = s.Motivation,
        CvDisponible = !string.IsNullOrWhiteSpace(s.CvCheminFichier),
        CvNomFichier = s.CvNomFichier,
        DocumentDisponible = !string.IsNullOrWhiteSpace(s.DocumentCheminFichier),
        DocumentNomFichier = s.DocumentNomFichier,
        Statut = s.Statut,
        EncadrantId = s.EncadrantId,
        EncadrantNom = s.EncadrantNom,
        MotifRejet = s.MotifRejet,
        DateSoumission = s.DateSoumission,
        DateDecision = s.DateDecision
    };
}
