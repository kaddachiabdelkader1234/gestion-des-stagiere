using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smartek.Common.Errors;
using Smartek.Common.Pagination;
using Evaluation.Service.Data;
using Evaluation.Service.DTOs;
using AuditEntryEntity = Evaluation.Service.Models.AuditEntry;

namespace Evaluation.Service.Controllers;

/// <summary>
/// Admin-only audit trail for evaluation actions: who created or validated what, when.
/// Exposed at <c>/api/v1/evaluations/audit</c> so the gateway routes it to Evaluation.Service
/// via the existing <c>/api/v1/evaluations/**</c> predicate — no gateway config changes needed.
/// </summary>
/// <remarks>
/// The schema and query parameters mirror Stagiaire.Service's <c>AuditController</c> so the
/// frontend can merge results from all three services into a single audit view.
/// </remarks>
[ApiController]
[Route("api/v1/evaluations/audit")]
[Produces("application/json")]
[Authorize(Roles = "ADMIN")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
public class EvaluationAuditController(AppDbContext dbContext) : ControllerBase
{
    /// <summary>
    /// Lists evaluation audit entries with server-side paging and filtering.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditEntryReadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditEntryReadDto>>> GetAll(
        [FromQuery] AuditQueryParameters query,
        CancellationToken cancellationToken)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = Math.Max(query.Page, 1);

        IQueryable<AuditEntryEntity> source = dbContext.AuditEntries
            .AsNoTracking()
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id);

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            source = source.Where(x => x.Action == query.Action.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            source = source.Where(x => x.EntityType == query.EntityType.Trim());
        }

        if (query.UserId is { } userId)
        {
            source = source.Where(x => x.UserId == userId);
        }

        if (query.DateFrom is { } from)
        {
            source = source.Where(x => x.Timestamp >= from);
        }

        if (query.DateTo is { } to)
        {
            source = source.Where(x => x.Timestamp < to.AddDays(1));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            source = source.Where(x =>
                EF.Functions.ILike(x.Details ?? "", pattern) ||
                EF.Functions.ILike(x.UserEmail ?? "", pattern));
        }

        var totalCount = await source.CountAsync(cancellationToken);

        var items = await source
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AuditEntryReadDto
            {
                Id = x.Id,
                Action = x.Action,
                UserId = x.UserId,
                UserRole = x.UserRole,
                UserEmail = x.UserEmail,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                Details = x.Details,
                Timestamp = x.Timestamp,
                TraceId = x.TraceId
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<AuditEntryReadDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>Retrieves a single audit entry by id.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(AuditEntryReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditEntryReadDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var entry = await dbContext.AuditEntries
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new AuditEntryReadDto
            {
                Id = x.Id,
                Action = x.Action,
                UserId = x.UserId,
                UserRole = x.UserRole,
                UserEmail = x.UserEmail,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                Details = x.Details,
                Timestamp = x.Timestamp,
                TraceId = x.TraceId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (entry is null)
        {
            throw NotFoundException.For("L'entrée d'audit", id);
        }

        return Ok(entry);
    }
}
