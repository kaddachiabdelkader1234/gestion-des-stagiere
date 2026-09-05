using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Smartek.Common.Security;
using Stagiaire.Service.Data;
using Stagiaire.Service.Models;

namespace Stagiaire.Service.Services;

/// <summary>
/// Writes audit entries to the AuditEntries table. Called from controllers for local actions
/// and from RabbitMQ consumers for cross-service actions (convention signed, evaluation submitted, etc.).
/// </summary>
public sealed class AuditService : IAuditService
{
    private readonly AppDbContext _dbContext;

    public AuditService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
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
            UserId = user.GetUserId(),
            UserRole = GetPrimaryRole(user),
            UserEmail = user.GetEmail(),
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            Timestamp = DateTime.UtcNow
        };

        _dbContext.AuditEntries.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task LogFromEventAsync(
        long? userId,
        string? userRole,
        string? userEmail,
        string action,
        string entityType,
        string? entityId = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new AuditEntry
        {
            Action = action,
            UserId = userId,
            UserRole = userRole,
            UserEmail = userEmail,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            Timestamp = DateTime.UtcNow
        };

        _dbContext.AuditEntries.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? GetPrimaryRole(ClaimsPrincipal user)
    {
        if (user.IsAdmin()) return "ADMIN";
        if (user.IsTrainer()) return "TRAINER";
        if (user.IsLearner()) return "LEARNER";
        return user.FindFirst("role")?.Value;
    }
}
