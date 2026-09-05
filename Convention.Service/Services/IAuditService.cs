using System.Security.Claims;

namespace Convention.Service.Services;

/// <summary>
/// Writes structured audit entries for every state-changing action.
/// </summary>
public interface IAuditService
{
    Task LogAsync(
        ClaimsPrincipal user,
        string action,
        string entityType,
        string? entityId = null,
        string? details = null,
        CancellationToken cancellationToken = default);
}
