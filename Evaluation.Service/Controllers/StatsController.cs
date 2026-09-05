using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Evaluation.Service.Data;
using Evaluation.Service.Models;
using Evaluation.Service.Pdf;
using Evaluation.Service.Security;
using EvaluationEntity = Evaluation.Service.Models.Evaluation;

namespace Evaluation.Service.Controllers;

/// <summary>
/// Admin-only statistics and attestation PDF endpoint.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAttestationPdfGenerator _pdfGenerator;

    public StatsController(AppDbContext db, IAttestationPdfGenerator pdfGenerator)
    {
        _db = db;
        _pdfGenerator = pdfGenerator;
    }

    /// <summary>
    /// Returns evaluation statistics for the admin dashboard.
    /// </summary>
    [HttpGet("evaluations/stats")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var totalEvaluations = await _db.Evaluations.CountAsync(ct);

        var validatedEvaluations = await _db.Evaluations
            .CountAsync(e => e.Statut == StatutEvaluation.Validee, ct);

        var averageScore = await _db.Evaluations
            .Where(e => e.Statut == StatutEvaluation.Validee)
            .AverageAsync(e => (double?)e.Note, ct) ?? 0;

        var byType = await _db.Evaluations
            .GroupBy(e => e.TypeEvaluation)
            .Select(g => new { type = g.Key.ToString(), count = g.Count() })
            .ToListAsync(ct);

        return Ok(new
        {
            totalEvaluations,
            validatedEvaluations,
            averageScore = Math.Round(averageScore, 2),
            byType
        });
    }

    /// <summary>
    /// Generates and returns the attestation de fin de stage PDF for a validated evaluation.
    /// Scoped: an admin sees all, a trainer sees their assigned stagiaires', a learner only their own.
    /// </summary>
    [HttpGet("evaluations/{id:guid}/attestation")]
    [Authorize(Roles = "ADMIN,TRAINER,LEARNER")]
    public async Task<IActionResult> GetAttestation(Guid id, CancellationToken ct)
    {
        // Scoped first, so an out-of-scope id is a 404 — same pattern as GetById in EvaluationsController.
        var evaluation = await _db.Evaluations.AsNoTracking()
            .ApplyReadScope(User)
            .SingleOrDefaultAsync(x => x.Id == id, ct);

        if (evaluation is null)
            return NotFound(new { error = "Évaluation non trouvée", code = "NOT_FOUND" });

        // Only validated evaluations get an attestation
        if (evaluation.Statut != StatutEvaluation.Validee)
            return BadRequest(new
            {
                error = "L'attestation n'est disponible que pour les évaluations validées.",
                code = "EVALUATION_NOT_VALIDATED"
            });

        var pdfBytes = _pdfGenerator.Generate(evaluation);
        var fileName = $"attestation-{evaluation.StagiaireNom}-{evaluation.StagiairePrenom}.pdf";

        return File(pdfBytes, "application/pdf", fileName);
    }
}
