using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smartek.Common.Errors;
using Smartek.Common.Pagination;
using Stagiaire.Service.Data;
using Stagiaire.Service.DTOs;
using Stagiaire.Service.Models;

namespace Stagiaire.Service.Controllers;

/// <summary>
/// Admin-only audit trail: who did what, when, to which entity.
/// Paginated and filterable by action type, entity type, user, and date range.
/// </summary>
[ApiController]
[Route("api/v1/audit")]
[Produces("application/json")]
[Authorize(Roles = "ADMIN")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public AuditController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Lists audit entries with server-side paging and filtering.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditEntryReadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditEntryReadDto>>> GetAll(
        [FromQuery] AuditQueryParameters query,
        CancellationToken cancellationToken)
    {
        // Clamp page size to a sane maximum.
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = Math.Max(query.Page, 1);

        IQueryable<AuditEntry> source = _dbContext.AuditEntries
            .AsNoTracking()
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id);

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            source = source.Where(x => x.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            var entityType = query.EntityType.Trim();
            source = source.Where(x => x.EntityType == entityType);
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
            // Include the whole day.
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
            // TotalPages is computed from TotalCount and PageSize — no setter needed.
        });
    }

    /// <summary>Retrieves a single audit entry by id.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(AuditEntryReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditEntryReadDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var entry = await _dbContext.AuditEntries
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
