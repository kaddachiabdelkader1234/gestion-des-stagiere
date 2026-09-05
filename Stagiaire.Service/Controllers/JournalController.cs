using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Smartek.Common.Errors;
using Smartek.Common.Pagination;
using Smartek.Common.Security;
using Stagiaire.Service.Data;
using Stagiaire.Service.DTOs;
using Stagiaire.Service.Models;
using Stagiaire.Service.Security;
using Stagiaire.Service.Services;
using StagiaireEntity = Stagiaire.Service.Models.Stagiaire;

namespace Stagiaire.Service.Controllers;

/// <summary>
/// Journal de bord: the stagiaire's weekly log entries, and the encadrant's review comments.
///
/// A sub-resource of a stagiaire (<c>/api/v1/stagiaires/{stagiaireId}/journal</c>) because every
/// operation is authorised against that stagiaire — who owns it and who supervises it. Reading the
/// parent first is not an extra round-trip we tolerate, it is where the security decision is made.
/// </summary>
[ApiController]
[Route("api/v1/stagiaires/{stagiaireId:guid}/journal")]
[Produces("application/json")]
// Defence in depth alongside the gateway's route rules — a direct call to :5070 must not bypass auth.
[Authorize]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
public class JournalController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<JournalController> _logger;

    public JournalController(AppDbContext dbContext, IAuditService auditService, ILogger<JournalController> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Lists a stagiaire's journal entries, newest week first, with server-side paging.
    /// </summary>
    /// <remarks>
    /// Visible to the stagiaire themselves, their assigned encadrant, and an admin. Anyone else gets
    /// a 404 on the stagiaire — the gateway permits all three roles on <c>GET</c>, so this is the
    /// only place the boundary exists.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<JournalEntryReadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<JournalEntryReadDto>>> GetAll(
        Guid stagiaireId,
        [FromQuery] JournalQueryParameters query,
        CancellationToken cancellationToken)
    {
        await LoadVisibleStagiaireAsync(stagiaireId, cancellationToken);

        var source = ApplyFilters(
            _dbContext.JournalEntrees.AsNoTracking().Where(x => x.StagiaireId == stagiaireId),
            query);

        var page = await source
            // Deterministic ordering: OFFSET/LIMIT without a tiebreaker can repeat or skip rows.
            .OrderByDescending(x => x.DateEntree)
            .ThenBy(x => x.Id)
            .Select(ToReadDtoExpression())
            .ToPagedResultAsync(query, cancellationToken);

        return Ok(page);
    }

    [HttpGet("{entryId:guid}")]
    [ProducesResponseType(typeof(JournalEntryReadDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<JournalEntryReadDto>> GetById(
        Guid stagiaireId,
        Guid entryId,
        CancellationToken cancellationToken)
    {
        await LoadVisibleStagiaireAsync(stagiaireId, cancellationToken);

        var entry = await _dbContext.JournalEntrees.AsNoTracking()
            .Where(x => x.Id == entryId && x.StagiaireId == stagiaireId)
            .Select(ToReadDtoExpression())
            .SingleOrDefaultAsync(cancellationToken);

        if (entry is null)
        {
            throw NotFoundException.For("L'entrée de journal", entryId);
        }

        return Ok(entry);
    }

    /// <summary>
    /// Adds a weekly entry. Written by the stagiaire who owns the stage, or by an admin.
    /// </summary>
    /// <remarks>
    /// An encadrant is deliberately refused with a 403: they can see this stagiaire, so pretending
    /// the resource does not exist would be a lie, and the journal is the trainee's own account of
    /// their work — an encadrant contributes through <c>POST /{entryId}/commentaire</c>.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(JournalEntryReadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JournalEntryReadDto>> Creer(
        Guid stagiaireId,
        [FromBody] JournalEntryCreateDto dto,
        CancellationToken cancellationToken)
    {
        var stagiaire = await LoadVisibleStagiaireAsync(stagiaireId, cancellationToken);

        if (!User.CanWrite(stagiaire))
        {
            throw new ForbiddenException(
                "Le journal de bord est rédigé par le stagiaire. Utilisez le commentaire pour y répondre.");
        }

        EnsureStageStarted(stagiaire);
        await EnsureWeekIsFreeAsync(stagiaireId, dto.DateEntree, null, cancellationToken);

        var entity = new JournalEntry
        {
            Id = Guid.NewGuid(),
            StagiaireId = stagiaireId,
            DateEntree = dto.DateEntree,
            Texte = dto.Texte.Trim(),
            DateCreation = DateTime.UtcNow
        };

        _dbContext.JournalEntrees.Add(entity);
        await SaveDetectingDuplicateWeekAsync(dto.DateEntree, cancellationToken);

        _logger.LogInformation(
            "Journal entry {EntryId} created for stagiaire {StagiaireId}, week {Week}",
            entity.Id, stagiaireId, entity.DateEntree);

        return CreatedAtAction(
            nameof(GetById),
            new { stagiaireId, entryId = entity.Id },
            MapToReadDto(entity));
    }

    /// <summary>
    /// Corrects an entry. Author or admin, and only while the encadrant has not commented on it.
    /// </summary>
    /// <remarks>
    /// The freeze applies to admins too: once a comment exists, the entry and the comment are one
    /// review record, and editing the text would leave the comment answering something that was
    /// never written. Correcting a commented entry means deleting it and re-adding it.
    /// </remarks>
    [HttpPut("{entryId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Modifier(
        Guid stagiaireId,
        Guid entryId,
        [FromBody] JournalEntryUpdateDto dto,
        CancellationToken cancellationToken)
    {
        var stagiaire = await LoadVisibleStagiaireAsync(stagiaireId, cancellationToken);

        if (!User.CanWrite(stagiaire))
        {
            throw new ForbiddenException("Seul l'auteur de l'entrée peut la modifier.");
        }

        var entity = await LoadEntryAsync(stagiaireId, entryId, cancellationToken);

        if (entity.CommentaireEncadrant is not null)
        {
            throw new ConflictException(
                "Cette entrée a été commentée par l'encadrant et ne peut plus être modifiée.");
        }

        if (entity.DateEntree != dto.DateEntree)
        {
            await EnsureWeekIsFreeAsync(stagiaireId, dto.DateEntree, entryId, cancellationToken);
            entity.DateEntree = dto.DateEntree;
        }

        entity.Texte = dto.Texte.Trim();
        entity.DateModification = DateTime.UtcNow;

        await SaveDetectingDuplicateWeekAsync(dto.DateEntree, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Records the encadrant's review note on an entry. Assigned encadrant, or an admin.
    /// </summary>
    /// <remarks>
    /// Re-posting replaces the note rather than conflicting: it belongs to its author, and this is
    /// the only way to write it, so refusing would make a typo permanent. Doing so freezes the
    /// entry's text — see <see cref="Modifier"/>.
    /// </remarks>
    [HttpPost("{entryId:guid}/commentaire")]
    [ProducesResponseType(typeof(JournalEntryReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JournalEntryReadDto>> Commenter(
        Guid stagiaireId,
        Guid entryId,
        [FromBody] JournalCommentaireDto dto,
        CancellationToken cancellationToken)
    {
        var stagiaire = await LoadVisibleStagiaireAsync(stagiaireId, cancellationToken);

        if (!User.CanSupervise(stagiaire))
        {
            throw new ForbiddenException(
                "Seul l'encadrant assigné à ce stagiaire peut commenter son journal.");
        }

        var entity = await LoadEntryAsync(stagiaireId, entryId, cancellationToken);

        entity.CommentaireEncadrant = dto.Commentaire.Trim();
        entity.CommentaireParId = User.GetUserId();
        entity.DateCommentaire = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Journal entry {EntryId} commented by user {UserId}",
            entity.Id, entity.CommentaireParId);

        await _auditService.LogAsync(User, "JOURNAL_COMMENT", "JournalEntry",
            entity.Id.ToString(),
            $"Commentaire sur l'entrée du {entity.DateEntree:dd/MM/yyyy} du stagiaire {stagiaireId}",
            cancellationToken);

        return Ok(MapToReadDto(entity));
    }

    /// <summary>Removes an entry. Admin only — the gateway restricts DELETE the same way.</summary>
    [HttpDelete("{entryId:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Supprimer(
        Guid stagiaireId,
        Guid entryId,
        CancellationToken cancellationToken)
    {
        await LoadVisibleStagiaireAsync(stagiaireId, cancellationToken);

        var entity = await LoadEntryAsync(stagiaireId, entryId, cancellationToken);

        _dbContext.JournalEntrees.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Loads the parent stagiaire if the caller is entitled to see it, else 404.
    /// </summary>
    /// <remarks>
    /// Every action starts here. Returning 404 rather than 403 for an out-of-scope stagiaire keeps
    /// an id the caller has no business knowing about indistinguishable from one that never existed.
    /// </remarks>
    private async Task<StagiaireEntity> LoadVisibleStagiaireAsync(
        Guid stagiaireId,
        CancellationToken cancellationToken)
    {
        var stagiaire = await _dbContext.Stagiaires.AsNoTracking()
            .ApplyReadScope(User)
            .SingleOrDefaultAsync(x => x.Id == stagiaireId, cancellationToken);

        if (stagiaire is null)
        {
            throw NotFoundException.For("Le stagiaire", stagiaireId);
        }

        return stagiaire;
    }

    /// <summary>
    /// Loads a tracked entry, matching on the parent id too so an entry belonging to a different
    /// stagiaire cannot be reached through a stagiaire the caller happens to be allowed to see.
    /// </summary>
    private async Task<JournalEntry> LoadEntryAsync(
        Guid stagiaireId,
        Guid entryId,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.JournalEntrees
            .SingleOrDefaultAsync(x => x.Id == entryId && x.StagiaireId == stagiaireId, cancellationToken);

        if (entity is null)
        {
            throw NotFoundException.For("L'entrée de journal", entryId);
        }

        return entity;
    }

    /// <summary>
    /// A journal only exists once there is a stage to journal about.
    /// </summary>
    private static void EnsureStageStarted(StagiaireEntity stagiaire)
    {
        if (stagiaire.Statut is StatutStagiaire.EnAttente or StatutStagiaire.Rejetee)
        {
            throw new ConflictException(
                $"Le journal de bord n'est disponible qu'après l'acceptation de la candidature " +
                $"(statut actuel : {stagiaire.Statut}).");
        }
    }

    /// <summary>
    /// Rejects a second entry for a week that already has one, with a message naming the week.
    /// </summary>
    /// <remarks>
    /// Not the only guard — the unique index is, since two concurrent submits would both pass this
    /// check. This exists so the ordinary case gets a clear message instead of a database error.
    /// </remarks>
    private async Task EnsureWeekIsFreeAsync(
        Guid stagiaireId,
        DateOnly dateEntree,
        Guid? excludeEntryId,
        CancellationToken cancellationToken)
    {
        var taken = await _dbContext.JournalEntrees
            .AsNoTracking()
            .AnyAsync(
                x => x.StagiaireId == stagiaireId
                     && x.DateEntree == dateEntree
                     && (excludeEntryId == null || x.Id != excludeEntryId),
                cancellationToken);

        if (taken)
        {
            throw new ConflictException(
                $"Une entrée de journal existe déjà pour la semaine du {dateEntree:dd/MM/yyyy}.");
        }
    }

    /// <summary>
    /// Saves, turning a unique-index violation on (StagiaireId, DateEntree) into the same 409 the
    /// pre-check would have produced.
    /// </summary>
    /// <remarks>
    /// Without this, a double-submit that races past <see cref="EnsureWeekIsFreeAsync"/> surfaces as
    /// an unhandled <c>DbUpdateException</c> — a 500 for what is squarely a caller-side conflict.
    /// </remarks>
    private async Task SaveDetectingDuplicateWeekAsync(
        DateOnly dateEntree,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new ConflictException(
                $"Une entrée de journal existe déjà pour la semaine du {dateEntree:dd/MM/yyyy}.");
        }
    }

    private static IQueryable<JournalEntry> ApplyFilters(
        IQueryable<JournalEntry> source,
        JournalQueryParameters query)
    {
        if (query.Du is { } du)
        {
            source = source.Where(x => x.DateEntree >= du);
        }

        if (query.Au is { } au)
        {
            source = source.Where(x => x.DateEntree <= au);
        }

        if (query.SansCommentaire == true)
        {
            source = source.Where(x => x.CommentaireEncadrant == null);
        }

        return source;
    }

    private static Expression<Func<JournalEntry, JournalEntryReadDto>> ToReadDtoExpression() =>
        e => new JournalEntryReadDto
        {
            Id = e.Id,
            StagiaireId = e.StagiaireId,
            DateEntree = e.DateEntree,
            Texte = e.Texte,
            CommentaireEncadrant = e.CommentaireEncadrant,
            CommentaireParId = e.CommentaireParId,
            DateCommentaire = e.DateCommentaire,
            DateCreation = e.DateCreation,
            DateModification = e.DateModification,
            EstCommentee = e.CommentaireEncadrant != null,
            Modifiable = e.CommentaireEncadrant == null
        };

    private static JournalEntryReadDto MapToReadDto(JournalEntry e) => new()
    {
        Id = e.Id,
        StagiaireId = e.StagiaireId,
        DateEntree = e.DateEntree,
        Texte = e.Texte,
        CommentaireEncadrant = e.CommentaireEncadrant,
        CommentaireParId = e.CommentaireParId,
        DateCommentaire = e.DateCommentaire,
        DateCreation = e.DateCreation,
        DateModification = e.DateModification,
        EstCommentee = e.CommentaireEncadrant is not null,
        Modifiable = e.CommentaireEncadrant is null
    };
}
