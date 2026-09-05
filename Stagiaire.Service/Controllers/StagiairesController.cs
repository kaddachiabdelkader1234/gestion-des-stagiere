using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smartek.Common.Errors;
using Smartek.Common.Pagination;
using Stagiaire.Service.Data;
using Stagiaire.Service.DTOs;
using Stagiaire.Service.Models;
using Stagiaire.Service.Security;
using StagiaireEntity = Stagiaire.Service.Models.Stagiaire;

namespace Stagiaire.Service.Controllers;

/// <summary>
/// CRUD over stagiaire records. Candidature state transitions live in
/// <see cref="CandidaturesController"/>.
/// </summary>
[ApiController]
[Route("api/v1/stagiaires")]
[Produces("application/json")]
// Defence in depth alongside the gateway's route rules — a direct call to :5070 must not bypass auth.
[Authorize]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
public class StagiairesController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public StagiairesController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Lists stagiaires with server-side paging, filtering and search.
    /// </summary>
    /// <remarks>
    /// Scoped by role: a TRAINER only ever sees stagiaires assigned to them, and a LEARNER only
    /// their own record — enforced from the JWT, so the <c>encadrantId</c> query parameter cannot
    /// be used to read someone else's roster.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<StagiaireReadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<StagiaireReadDto>>> GetAll(
        [FromQuery] StagiaireQueryParameters query,
        CancellationToken cancellationToken)
    {
        var source = _dbContext.Stagiaires.AsNoTracking().ApplyReadScope(User);
        source = ApplyFilters(source, query);

        var page = await source
            // Deterministic ordering: OFFSET/LIMIT without a tiebreaker can repeat or skip rows.
            .OrderByDescending(x => x.DateSoumission)
            .ThenBy(x => x.Id)
            .Select(ToReadDtoExpression())
            .ToPagedResultAsync(query, cancellationToken);

        return Ok(page);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StagiaireReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StagiaireReadDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        // Scoped first, so an out-of-scope id is indistinguishable from a nonexistent one.
        var stagiaire = await _dbContext.Stagiaires.AsNoTracking()
            .ApplyReadScope(User)
            .Where(x => x.Id == id)
            .Select(ToReadDtoExpression())
            .SingleOrDefaultAsync(cancellationToken);

        if (stagiaire is null)
        {
            throw NotFoundException.For("Le stagiaire", id);
        }

        return Ok(stagiaire);
    }

    /// <summary>
    /// Creates a record directly. Admin only — a learner applies via
    /// <c>POST /api/v1/candidatures</c>, which cannot set the status.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(StagiaireReadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StagiaireReadDto>> Create(
        [FromBody] StagiaireCreateDto dto,
        CancellationToken cancellationToken)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        await EnsureEmailIsFreeAsync(email, null, cancellationToken);

        var entity = new StagiaireEntity
        {
            Id = Guid.NewGuid(),
            UtilisateurId = dto.UtilisateurId,
            Nom = dto.Nom.Trim(),
            Prenom = dto.Prenom.Trim(),
            Email = email,
            Departement = dto.Departement.Trim(),
            TypeStage = dto.TypeStage,
            Ecole = dto.Ecole.Trim(),
            DateDebut = dto.DateDebut,
            DateFin = dto.DateFin,
            Motivation = dto.Motivation?.Trim(),
            Statut = dto.Statut,
            DateSoumission = DateTime.UtcNow
        };

        _dbContext.Stagiaires.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // No CandidatureAccepted here. It used to fire on every create, which was wrong: a new
        // record is EnAttente, not accepted. It now belongs to CandidaturesController.Accepter.
        return CreatedAtAction(
            nameof(GetById),
            new { id = entity.Id },
            CandidaturesController.MapToReadDto(entity));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] StagiaireUpdateDto dto,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Stagiaires.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            throw NotFoundException.For("Le stagiaire", id);
        }

        var email = dto.Email.Trim().ToLowerInvariant();
        await EnsureEmailIsFreeAsync(email, id, cancellationToken);

        if (dto.Statut == StatutStagiaire.Rejetee && string.IsNullOrWhiteSpace(dto.MotifRejet))
        {
            throw new ValidationException(
                "motifRejet", "Un motif est obligatoire pour un statut Rejetee.");
        }

        entity.Nom = dto.Nom.Trim();
        entity.Prenom = dto.Prenom.Trim();
        entity.Email = email;
        entity.Departement = dto.Departement.Trim();
        entity.TypeStage = dto.TypeStage;
        entity.Ecole = dto.Ecole.Trim();
        entity.DateDebut = dto.DateDebut;
        entity.DateFin = dto.DateFin;
        entity.Motivation = dto.Motivation?.Trim();
        entity.Statut = dto.Statut;
        entity.EncadrantId = dto.EncadrantId;
        entity.EncadrantNom = dto.EncadrantNom?.Trim();
        entity.MotifRejet = dto.MotifRejet?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Stagiaires.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            throw NotFoundException.For("Le stagiaire", id);
        }

        _dbContext.Stagiaires.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static IQueryable<StagiaireEntity> ApplyFilters(
        IQueryable<StagiaireEntity> source,
        StagiaireQueryParameters query)
    {
        if (query.Statut is { } statut)
        {
            source = source.Where(x => x.Statut == statut);
        }

        if (query.TypeStage is { } typeStage)
        {
            source = source.Where(x => x.TypeStage == typeStage);
        }

        if (!string.IsNullOrWhiteSpace(query.Departement))
        {
            var departement = query.Departement.Trim();
            source = source.Where(x => x.Departement.ToLower() == departement.ToLower());
        }

        // Only meaningful for an admin: ApplyReadScope has already pinned a trainer to their own
        // id, so this cannot widen what they see.
        if (query.EncadrantId is { } encadrantId)
        {
            source = source.Where(x => x.EncadrantId == encadrantId);
        }

        if (!string.IsNullOrWhiteSpace(query.Recherche))
        {
            // ILIKE so the match is case-insensitive in PostgreSQL.
            var pattern = $"%{query.Recherche.Trim()}%";
            source = source.Where(x =>
                EF.Functions.ILike(x.Nom, pattern) ||
                EF.Functions.ILike(x.Prenom, pattern) ||
                EF.Functions.ILike(x.Email, pattern) ||
                EF.Functions.ILike(x.Ecole, pattern));
        }

        return source;
    }

    private async Task EnsureEmailIsFreeAsync(string email, Guid? excludeId, CancellationToken cancellationToken)
    {
        var taken = await _dbContext.Stagiaires
            .AsNoTracking()
            .AnyAsync(x => x.Email == email && (excludeId == null || x.Id != excludeId), cancellationToken);

        if (taken)
        {
            throw new ConflictException($"Un stagiaire avec l'email '{email}' existe déjà.");
        }
    }

    private static Expression<Func<StagiaireEntity, StagiaireReadDto>> ToReadDtoExpression() => s => new StagiaireReadDto
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
        CvDisponible = s.CvCheminFichier != null && s.CvCheminFichier != "",
        CvNomFichier = s.CvNomFichier,
        DocumentDisponible = s.DocumentCheminFichier != null && s.DocumentCheminFichier != "",
        DocumentNomFichier = s.DocumentNomFichier,
        Statut = s.Statut,
        EncadrantId = s.EncadrantId,
        EncadrantNom = s.EncadrantNom,
        MotifRejet = s.MotifRejet,
        DateSoumission = s.DateSoumission,
        DateDecision = s.DateDecision
    };
}
