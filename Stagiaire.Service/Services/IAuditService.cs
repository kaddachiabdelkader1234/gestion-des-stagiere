using System.Security.Claims;
using Stagiaire.Service.Models;

namespace Stagiaire.Service.Services;

/// <summary>
/// Writes structured audit entries for every state-changing action.
/// Inject into controllers that perform administrative or supervisory work.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Records an audit entry. Called within the same transaction as the business action
    /// so the audit trail and the data are always consistent.
    /// </summary>
    Task LogAsync(
        ClaimsPrincipal user,
        string action,
        string entityType,
        string? entityId = null,
        string? details = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an audit entry from a RabbitMQ event (cross-service audit trail).
    /// The caller information comes from the event payload, not from an HTTP request.
    /// </summary>
    Task LogFromEventAsync(
        long? userId,
        string? userRole,
        string? userEmail,
        string action,
        string entityType,
        string? entityId = null,
        string? details = null,
        CancellationToken cancellationToken = default);
}
