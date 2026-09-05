using System.Security.Claims;
using Convention.Service.Data;
using Convention.Service.Models;

namespace Convention.Service.Services;

public sealed class AuditService : IAuditService
{
    private readonly AppDbContext _dbContext;

    public AuditService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LogAsync(
        ClaimsPrincipal user,
        string action,
        string entityType,
        string? entityId = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new AuditEntry
        {
            Action = action,
            UserId = GetUserId(user),
            UserRole = GetPrimaryRole(user),
            UserEmail = user.FindFirstValue(ClaimTypes.Email),
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            Timestamp = DateTime.UtcNow
        };

        _dbContext.AuditEntries.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static long? GetUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst("userId") ?? user.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && long.TryParse(claim.Value, out var id) ? id : null;
    }

    private static string? GetPrimaryRole(ClaimsPrincipal user)
    {
        return user.FindFirst("role")?.Value;
    }
}
