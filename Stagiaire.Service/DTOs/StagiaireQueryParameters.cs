using Smartek.Common.Pagination;
using Stagiaire.Service.Models;

namespace Stagiaire.Service.DTOs;

/// <summary>
/// Filters for <c>GET /api/v1/stagiaires</c>, on top of the inherited <c>page</c>/<c>pageSize</c>.
///
/// Example: <c>?page=1&amp;pageSize=20&amp;statut=EnAttente&amp;departement=IT&amp;recherche=ben</c>
/// </summary>
public class StagiaireQueryParameters : PaginationQuery
{
    /// <summary>Exact status match. Omit for all statuses.</summary>
    public StatutStagiaire? Statut { get; set; }

    /// <summary>Exact département match, case-insensitive.</summary>
    public string? Departement { get; set; }

    /// <summary>Filter by internship type.</summary>
    public TypeStage? TypeStage { get; set; }

    /// <summary>
    /// Restrict to one encadrant's stagiaires.
    /// </summary>
    /// <remarks>
    /// Ignored for a TRAINER caller: the controller forces the value from the JWT so a trainer
    /// cannot read another's stagiaires by passing a different id. Effective for ADMIN only.
    /// </remarks>
    public long? EncadrantId { get; set; }

    /// <summary>Free-text search across nom, prénom, email and école (case-insensitive substring).</summary>
    public string? Recherche { get; set; }
}
