using Smartek.Common.Pagination;
using Evaluation.Service.Models;

namespace Evaluation.Service.DTOs;

/// <summary>
/// Filters for <c>GET /api/v1/evaluations</c>, on top of <c>page</c>/<c>pageSize</c>.
/// </summary>
/// <remarks>
/// These narrow what the caller may already see; they cannot widen it. The controller applies
/// <c>ApplyReadScope</c> first, so a TRAINER passing another trainer's <c>encadrantId</c> gets an
/// empty page rather than someone else's roster.
/// </remarks>
public class EvaluationQueryParameters : PaginationQuery
{
    /// <summary>A single stagiaire's evaluation(s).</summary>
    public Guid? StagiaireId { get; set; }

    /// <summary>
    /// Restrict to one encadrant. Meaningful for an ADMIN only — a TRAINER is already pinned to
    /// their own id by the visibility scope.
    /// </summary>
    public long? EncadrantId { get; set; }

    public StatutEvaluation? Statut { get; set; }

    public TypeEvaluation? TypeEvaluation { get; set; }
}
