using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smartek.Common.Errors;
using Smartek.Common.Pagination;
using Smartek.Common.Security;
using Notification.Service.Data;
using Notification.Service.DTOs;
using NotificationEntity = Notification.Service.Models.Notification;

namespace Notification.Service.Controllers;

/// <summary>
/// Notifications addressed to a user. Scoping was added on 2026-08-26 after changing
/// <c>DestinataireId</c> from <c>Guid</c> to <c>long</c> — the identical pattern applied to
/// <c>Evaluation.EncadrantId</c> in step 7.
/// </summary>
/// <remarks>
/// An admin sees every notification; a learner or trainer sees only notifications addressed to
/// them (<c>DestinataireId == caller.userId</c>). An out-of-scope id is indistinguishable from a
/// nonexistent one — always 404, never 403 — so the id space does not leak.
///
/// Writes remain ADMIN-only (the consumers will create notifications from events; the admin
/// endpoint is for manual/test creation).
/// </remarks>
[ApiController]
[Route("api/v1/notifications")]
[Produces("application/json")]
// Defence in depth alongside the gateway's route rules — a direct call to :5073 must not bypass auth.
[Authorize]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
public class NotificationsController(AppDbContext dbContext) : ControllerBase
{
    /// <summary>
    /// Lists notifications, newest first, paged and filterable.
    /// </summary>
    /// <remarks>
    /// Scoped by role: an ADMIN sees everything, a LEARNER or TRAINER only their own. The
    /// <c>destinataireId</c> query parameter can only narrow that — it is applied after the scope,
    /// so passing someone else's id yields an empty page rather than their data.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NotificationReadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<NotificationReadDto>>> GetAll(
        [FromQuery] NotificationQueryParameters query,
        CancellationToken cancellationToken)
    {
        var source = ApplyReadScope(dbContext.Notifications.AsNoTracking());

        if (query.DestinataireId is { } destinataireId)
        {
            source = source.Where(x => x.DestinataireId == destinataireId);
        }

        if (query.Lu is { } lu)
        {
            source = source.Where(x => x.Lu == lu);
        }

        if (query.Type is { } type)
        {
            source = source.Where(x => x.Type == type);
        }

        var page = await source
            .OrderByDescending(x => x.DateCreation)
            .ThenBy(x => x.Id)
            .Select(ToReadDtoExpression())
            .ToPagedResultAsync(query, cancellationToken);

        return Ok(page);
    }

    /// <summary>
    /// Gets a single notification by id. Scoped first, so an out-of-scope id is a 404.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NotificationReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationReadDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await ApplyReadScope(dbContext.Notifications.AsNoTracking())
            .Where(x => x.Id == id)
            .Select(ToReadDtoExpression())
            .SingleOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            throw NotFoundException.For("La notification", id);
        }

        return Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(NotificationReadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NotificationReadDto>> Create(
        [FromBody] NotificationCreateDto dto,
        CancellationToken cancellationToken)
    {
        var entity = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            DestinataireId = dto.DestinataireId,
            DestinataireRole = dto.DestinataireRole,
            Type = dto.Type,
            Message = dto.Message.Trim(),
            Lu = dto.Lu,
            DateCreation = dto.DateCreation == default ? DateTime.UtcNow : dto.DateCreation
        };

        dbContext.Notifications.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToReadDto(entity));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] NotificationUpdateDto dto,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.Notifications.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            throw NotFoundException.For("La notification", id);
        }

        // DestinataireId and DestinataireRole are not on the DTO — ownership is not settable
        // through PUT (same rule as ConventionUpdateDto, EvaluationUpdateDto).
        entity.DestinataireRole = dto.DestinataireRole;
        entity.Type = dto.Type;
        entity.Message = dto.Message.Trim();
        entity.Lu = dto.Lu;
        entity.DateCreation = dto.DateCreation;

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Notifications.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            throw NotFoundException.For("La notification", id);
        }

        dbContext.Notifications.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Narrows a query to what the caller is entitled to see, before any client-supplied filter.
    /// </summary>
    /// <remarks>
    /// An admin sees everything; a learner or trainer sees only notifications addressed to them.
    /// A caller with no userId claim sees nothing — the safe failure direction.
    /// </remarks>
    private IQueryable<NotificationEntity> ApplyReadScope(IQueryable<NotificationEntity> source)
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

        return source.Where(x => x.DestinataireId == callerId);
    }

    private static NotificationReadDto ToReadDto(NotificationEntity entity) => new()
    {
        Id = entity.Id,
        DestinataireId = entity.DestinataireId,
        DestinataireRole = entity.DestinataireRole,
        Type = entity.Type,
        Message = entity.Message,
        Lu = entity.Lu,
        DateCreation = entity.DateCreation
    };

    private static Expression<Func<NotificationEntity, NotificationReadDto>> ToReadDtoExpression() =>
        x => new NotificationReadDto
        {
            Id = x.Id,
            DestinataireId = x.DestinataireId,
            DestinataireRole = x.DestinataireRole,
            Type = x.Type,
            Message = x.Message,
            Lu = x.Lu,
            DateCreation = x.DateCreation
        };
}
