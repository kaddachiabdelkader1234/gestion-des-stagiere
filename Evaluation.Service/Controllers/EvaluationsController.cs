using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Smartek.Common.Errors;
using Smartek.Common.Pagination;
using Evaluation.Service.Data;
using Evaluation.Service.DTOs;
using Evaluation.Service.Models;
using Evaluation.Service.Security;
using Evaluation.Service.Services;
using MassTransit;
using EvaluationEntity = Evaluation.Service.Models.Evaluation;
// The contract enums mirror the model enums member-for-member, so importing both namespaces would
// make every bare `TypeEvaluation` ambiguous. Model enums stay bare; contract types are aliased.
using EvaluationSubmitted = Stagiaire.Contracts.Events.EvaluationSubmitted;
using EventStatut = Stagiaire.Contracts.Events.StatutEvaluation;
using EventType = Stagiaire.Contracts.Events.TypeEvaluation;

namespace Evaluation.Service.Controllers;

/// <summary>
/// Evaluations: an encadrant grades their assigned stagiaires, an admin oversees and validates, and
/// a stagiaire reads their own result.
/// </summary>
/// <remarks>
/// Authorization here is load-bearing, not decorative. The gateway permits ADMIN, TRAINER **and**
/// LEARNER on every <c>GET /api/v1/evaluations/**</c>, and this controller previously carried no
/// <c>[Authorize]</c> at all — so any authenticated learner could list every evaluation in the bank,
/// and any trainer could grade a stagiaire who was not theirs. See
/// <see cref="EvaluationVisibility"/> for the single definition of who may see and touch what.
/// </remarks>
[ApiController]
[Route("api/v1/evaluations")]
[Produces("application/json")]
// Defence in depth alongside the gateway's route rules — a direct call to :5072 must not bypass auth.
[Authorize]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
public class EvaluationsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IAuditService _auditService;
    private readonly ILogger<EvaluationsController> _logger;

    public EvaluationsController(
        AppDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        IAuditService auditService,
        ILogger<EvaluationsController> logger)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Lists evaluations, most recent first, paged and filterable.
    /// </summary>
    /// <remarks>
    /// Scoped by role from the JWT: an ADMIN sees everything, an encadrant only their assigned
    /// stagiaires', a learner only their own. The <c>encadrantId</c> and <c>stagiaireId</c> filters
    /// can only narrow that — they are applied after the scope, so passing someone else's id yields
    /// an empty page rather than their data.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EvaluationReadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<EvaluationReadDto>>> GetAll(
        [FromQuery] EvaluationQueryParameters query,
        CancellationToken cancellationToken)
    {
        var source = ApplyFilters(
            _dbContext.Evaluations.AsNoTracking().ApplyReadScope(User),
            query);

        var page = await source
            // Deterministic ordering: OFFSET/LIMIT without a tiebreaker can repeat or skip rows.
            .OrderByDescending(x => x.DateEvaluation)
            .ThenBy(x => x.Id)
            .Select(ToReadDtoExpression())
            .ToPagedResultAsync(query, cancellationToken);

        return Ok(page);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EvaluationReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EvaluationReadDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        // Scoped first, so an out-of-scope id is indistinguishable from a nonexistent one.
        var item = await _dbContext.Evaluations.AsNoTracking()
            .ApplyReadScope(User)
            .Where(x => x.Id == id)
            .Select(ToReadDtoExpression())
            .SingleOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            throw NotFoundException.For("L'évaluation", id);
        }

        return Ok(item);
    }

    /// <summary>
    /// Records an evaluation for a stagiaire. The assigned encadrant, or an admin.
    /// </summary>
    /// <remarks>
    /// Ownership is resolved server-side from the <see cref="StagiaireAffectation"/> projection, never
    /// from the request body. That is the whole point: the body used to carry <c>EncadrantId</c> and
    /// the stagiaire's name, so a trainer could grade anyone and the notification email said whatever
    /// the caller typed.
    ///
    /// <c>Statut</c> is set here rather than accepted, so an encadrant cannot submit an evaluation
    /// already marked validated. Validation is <see cref="Valider"/>, admin only.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = "TRAINER,ADMIN")]
    [ProducesResponseType(typeof(EvaluationReadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EvaluationReadDto>> Create(
        [FromBody] EvaluationCreateDto dto,
        CancellationToken cancellationToken)
    {
        var affectation = await _dbContext.StagiaireAffectations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.StagiaireId == dto.StagiaireId, cancellationToken);

        // No projection row means no accepted candidature — there is no stage to evaluate. 404 rather
        // than 403, since the caller has no business learning whether the id exists.
        if (affectation is null)
        {
            throw new NotFoundException(
                $"Aucun stage accepté ne correspond au stagiaire '{dto.StagiaireId}'.");
        }

        // 403 not 404: an assigned trainer can see this stagiaire, so denying the id would be a lie.
        if (!User.CanEvaluate(affectation))
        {
            throw new ForbiddenException(
                "Seul l'encadrant assigné à ce stagiaire peut l'évaluer.");
        }

        if (affectation.EncadrantId is not { } encadrantId)
        {
            throw new ConflictException(
                "Ce stagiaire n'a pas d'encadrant assigné ; l'évaluation ne peut pas être rattachée.");
        }

        await EnsureTypeIsFreeAsync(dto.StagiaireId, dto.TypeEvaluation, null, cancellationToken);

        var entity = new EvaluationEntity
        {
            Id = Guid.NewGuid(),
            StagiaireId = dto.StagiaireId,
            // From the projection, not the body — this is what visibility is enforced on.
            EncadrantId = encadrantId,
            UtilisateurId = affectation.UtilisateurId,
            StagiaireNom = affectation.Nom,
            StagiairePrenom = affectation.Prenom,
            StagiaireEmail = affectation.Email,
            TypeEvaluation = dto.TypeEvaluation,
            DateEvaluation = dto.DateEvaluation,
            Note = dto.Note,
            Commentaire = dto.Commentaire.Trim(),
            Statut = StatutEvaluation.Soumise
        };

        _dbContext.Evaluations.Add(entity);
        await SaveDetectingDuplicateTypeAsync(dto.TypeEvaluation, cancellationToken);

        await _publishEndpoint.Publish(new EvaluationSubmitted(
            entity.Id,
            entity.StagiaireId,
            entity.StagiaireNom,
            entity.StagiairePrenom,
            entity.StagiaireEmail,
            entity.UtilisateurId,
            entity.EncadrantId,
            ToEventType(entity.TypeEvaluation),
            entity.Note,
            entity.Commentaire,
            ToEventStatut(entity.Statut)
        ), cancellationToken);

        _logger.LogInformation(
            "Evaluation {EvaluationId} ({Type}) recorded for stagiaire {StagiaireId} by encadrant {EncadrantId}",
            entity.Id, entity.TypeEvaluation, entity.StagiaireId, entity.EncadrantId);

        await _auditService.LogAsync(User, "EVALUATION_CREATED", "Evaluation",
            entity.Id.ToString(),
            $"Évaluation {entity.TypeEvaluation} pour {entity.StagiairePrenom} {entity.StagiaireNom} — note: {entity.Note}/20",
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToReadDto(entity));
    }

    /// <summary>
    /// Corrects an evaluation. The assigned encadrant, or an admin, and only before validation.
    /// </summary>
    /// <remarks>
    /// The freeze after validation applies to admins too, for the same reason a commented journal
    /// entry freezes: an approved note that can still be edited is not an approval of anything.
    /// Correcting a validated evaluation means deleting it and recording it again.
    /// </remarks>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "TRAINER,ADMIN")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] EvaluationUpdateDto dto,
        CancellationToken cancellationToken)
    {
        var entity = await LoadModifiableAsync(id, cancellationToken);

        if (entity.Statut == StatutEvaluation.Validee)
        {
            throw new ConflictException(
                "Cette évaluation a été validée et ne peut plus être modifiée.");
        }

        if (entity.TypeEvaluation != dto.TypeEvaluation)
        {
            await EnsureTypeIsFreeAsync(entity.StagiaireId, dto.TypeEvaluation, id, cancellationToken);
            entity.TypeEvaluation = dto.TypeEvaluation;
        }

        // StagiaireId, EncadrantId and UtilisateurId are deliberately untouched: the DTO does not
        // carry them, so a replacement PUT cannot orphan the row (HANDOFF.md §11).
        entity.DateEvaluation = dto.DateEvaluation;
        entity.Note = dto.Note;
        entity.Commentaire = dto.Commentaire.Trim();

        await SaveDetectingDuplicateTypeAsync(dto.TypeEvaluation, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Validates a submitted evaluation. Admin only.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Create"/> so an encadrant cannot self-validate: creation always lands
    /// on <c>Soumise</c>, and only an admin moves it to <c>Validee</c>.
    /// </remarks>
    [HttpPost("{id:guid}/valider")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(EvaluationReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EvaluationReadDto>> Valider(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Evaluations.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFoundException.For("L'évaluation", id);

        if (entity.Statut == StatutEvaluation.Validee)
        {
            throw new ConflictException("Cette évaluation est déjà validée.");
        }

        entity.Statut = StatutEvaluation.Validee;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Evaluation {EvaluationId} validated", entity.Id);

        await _auditService.LogAsync(User, "EVALUATION_VALIDATED", "Evaluation",
            entity.Id.ToString(),
            $"Évaluation {entity.TypeEvaluation} de {entity.StagiairePrenom} {entity.StagiaireNom} validée — note: {entity.Note}/20",
            cancellationToken);

        return Ok(ToReadDto(entity));
    }

    /// <summary>Removes an evaluation. Admin only — the gateway restricts DELETE the same way.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Evaluations.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            throw NotFoundException.For("L'évaluation", id);
        }

        _dbContext.Evaluations.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Loads a tracked evaluation the caller may modify, or 404.
    /// </summary>
    /// <remarks>
    /// Uses <c>CanModify</c>, not the read scope: the read scope also matches evaluations *about* the
    /// caller, which would let a trainer who is themselves on a stage edit their own grade.
    /// </remarks>
    private async Task<EvaluationEntity> LoadModifiableAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Evaluations.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFoundException.For("L'évaluation", id);

        if (!User.CanModify(entity))
        {
            throw NotFoundException.For("L'évaluation", id);
        }

        return entity;
    }

    /// <summary>
    /// Rejects a second evaluation of the same type for one stagiaire.
    /// </summary>
    /// <remarks>
    /// Not the only guard — the unique index is, since two concurrent submits would both pass this
    /// check. This exists so the ordinary case gets a clear message instead of a database error.
    /// </remarks>
    private async Task EnsureTypeIsFreeAsync(
        Guid stagiaireId,
        TypeEvaluation type,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var taken = await _dbContext.Evaluations
            .AsNoTracking()
            .AnyAsync(
                x => x.StagiaireId == stagiaireId
                     && x.TypeEvaluation == type
                     && (excludeId == null || x.Id != excludeId),
                cancellationToken);

        if (taken)
        {
            throw new ConflictException(
                $"Une évaluation de type '{type}' existe déjà pour ce stagiaire.");
        }
    }

    /// <summary>
    /// Saves, turning a unique-index violation on (StagiaireId, TypeEvaluation) into the same 409 the
    /// pre-check would have produced, rather than an unhandled 500.
    /// </summary>
    private async Task SaveDetectingDuplicateTypeAsync(
        TypeEvaluation type,
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
                $"Une évaluation de type '{type}' existe déjà pour ce stagiaire.");
        }
    }

    private static IQueryable<EvaluationEntity> ApplyFilters(
        IQueryable<EvaluationEntity> source,
        EvaluationQueryParameters query)
    {
        if (query.StagiaireId is { } stagiaireId)
        {
            source = source.Where(x => x.StagiaireId == stagiaireId);
        }

        // Only meaningful for an admin: ApplyReadScope has already pinned a trainer to their own id,
        // so this cannot widen what they see.
        if (query.EncadrantId is { } encadrantId)
        {
            source = source.Where(x => x.EncadrantId == encadrantId);
        }

        if (query.Statut is { } statut)
        {
            source = source.Where(x => x.Statut == statut);
        }

        if (query.TypeEvaluation is { } type)
        {
            source = source.Where(x => x.TypeEvaluation == type);
        }

        return source;
    }

    /// <summary>
    /// Maps the model enum onto the contract enum <b>by name</b>.
    /// </summary>
    /// <remarks>
    /// This used to be <c>(Events.TypeEvaluation)(int)entity.TypeEvaluation</c>. The two enums did not
    /// agree — the contract's members were <c>Technique | Comportementale | Ponctualite</c> against the
    /// model's <c>MiParcours | Finale</c> — so every published event carried a different value than the
    /// row it described, and an ordinal cast between enums always compiles, so nothing caught it.
    ///
    /// A future added member throws loudly here instead of being silently mislabelled.
    /// </remarks>
    private static EventType ToEventType(TypeEvaluation type) => type switch
    {
        TypeEvaluation.MiParcours => EventType.MiParcours,
        TypeEvaluation.Finale => EventType.Finale,
        _ => throw new InvalidOperationException($"TypeEvaluation non mappé : {type}")
    };

    /// <inheritdoc cref="ToEventType"/>
    private static EventStatut ToEventStatut(StatutEvaluation statut) => statut switch
    {
        StatutEvaluation.EnAttente => EventStatut.EnAttente,
        StatutEvaluation.Soumise => EventStatut.Soumise,
        StatutEvaluation.Validee => EventStatut.Validee,
        _ => throw new InvalidOperationException($"StatutEvaluation non mappé : {statut}")
    };

    private static EvaluationReadDto ToReadDto(EvaluationEntity entity) => new()
    {
        Id = entity.Id,
        StagiaireId = entity.StagiaireId,
        UtilisateurId = entity.UtilisateurId,
        EncadrantId = entity.EncadrantId,
        StagiaireNom = entity.StagiaireNom,
        StagiairePrenom = entity.StagiairePrenom,
        StagiaireEmail = entity.StagiaireEmail,
        TypeEvaluation = entity.TypeEvaluation,
        DateEvaluation = entity.DateEvaluation,
        Note = entity.Note,
        Commentaire = entity.Commentaire,
        Statut = entity.Statut
    };

    private static Expression<Func<EvaluationEntity, EvaluationReadDto>> ToReadDtoExpression() =>
        x => new EvaluationReadDto
        {
            Id = x.Id,
            StagiaireId = x.StagiaireId,
            UtilisateurId = x.UtilisateurId,
            EncadrantId = x.EncadrantId,
            StagiaireNom = x.StagiaireNom,
            StagiairePrenom = x.StagiairePrenom,
            StagiaireEmail = x.StagiaireEmail,
            TypeEvaluation = x.TypeEvaluation,
            DateEvaluation = x.DateEvaluation,
            Note = x.Note,
            Commentaire = x.Commentaire,
            Statut = x.Statut
        };
}
