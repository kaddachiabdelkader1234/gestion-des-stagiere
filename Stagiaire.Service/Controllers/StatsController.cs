using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stagiaire.Service.Data;

namespace Stagiaire.Service.Controllers;

/// <summary>
/// Admin-only statistics endpoint for the dashboard.
/// Returns real, non-mocked data from the stagiaire database.
/// </summary>
[ApiController]
[Route("api/v1/stagiaires")]
[Authorize]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _db;

    public StatsController(AppDbContext db) => _db = db;

    /// <summary>
    /// Returns aggregate stats for the admin dashboard.
    /// Only admins should see the full picture — the gateway enforces this.
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var totalStagiaires = await _db.Stagiaires.CountAsync(ct);

        var pendingCandidatures = await _db.Stagiaires
            .CountAsync(s => s.Statut == Models.StatutStagiaire.EnAttente, ct);

        var acceptedStagiaires = await _db.Stagiaires
            .CountAsync(s => s.Statut == Models.StatutStagiaire.Acceptee
                          || s.Statut == Models.StatutStagiaire.EnCours
                          || s.Statut == Models.StatutStagiaire.Termine, ct);

        var rejectedCandidatures = await _db.Stagiaires
            .CountAsync(s => s.Statut == Models.StatutStagiaire.Rejetee, ct);

        // Breakdown by département
        var byDepartement = await _db.Stagiaires
            .GroupBy(s => s.Departement)
            .Select(g => new { departement = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToListAsync(ct);

        // Breakdown by type de stage
        var byTypeStage = await _db.Stagiaires
            .GroupBy(s => s.TypeStage)
            .Select(g => new { typeStage = g.Key.ToString(), count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToListAsync(ct);

        // Breakdown by statut
        var byStatut = await _db.Stagiaires
            .GroupBy(s => s.Statut)
            .Select(g => new { statut = g.Key.ToString(), count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToListAsync(ct);

        return Ok(new
        {
            totalStagiaires,
            pendingCandidatures,
            acceptedStagiaires,
            rejectedCandidatures,
            byDepartement,
            byTypeStage,
            byStatut
        });
    }
}
