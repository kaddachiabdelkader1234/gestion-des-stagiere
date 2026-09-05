using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Evaluation.Service.Models;

public class Evaluation
{
    public Guid Id { get; set; }

    /// <summary>References <c>Stagiaires.Id</c> in Stagiaire.Service — genuinely a Guid.</summary>
    public Guid StagiaireId { get; set; }

    /// <summary>
    /// auth-service <c>users.user_id</c> of the **assigned** encadrant, not necessarily the caller
    /// who submitted the form.
    /// </summary>
    /// <remarks>
    /// Was a <c>Guid</c>, which could never equal the <c>userId</c> claim in a JWT — so no role
    /// scoping could be built on it. It is a BIGINT in auth-service, hence <c>long</c>, matching
    /// <c>Stagiaire.EncadrantId</c> and <c>Convention.EncadrantId</c>.
    ///
    /// Deliberately the *assigned* encadrant rather than the author: a trainer's scope query is
    /// "evaluations for my assigned stagiaires", which must still include one an admin recorded on
    /// their behalf. Set server-side from <see cref="StagiaireAffectation"/>, never from the body.
    /// </remarks>
    public long EncadrantId { get; set; }

    /// <summary>
    /// auth-service <c>users.user_id</c> of the stagiaire being evaluated — what scopes a LEARNER's
    /// reads to their own result.
    /// </summary>
    /// <remarks>
    /// Nullable because a stagiaire record may exist without an account (an admin can create one
    /// directly). Null means "not visible to any learner", which is the safe direction.
    /// </remarks>
    public long? UtilisateurId { get; set; }

    /// <summary>
    /// Denormalised from <see cref="StagiaireAffectation"/> at create time, so an admin or encadrant
    /// list renders without a lookup per row — the same approach as <c>Convention</c>.
    /// </summary>
    [MaxLength(100)]
    public string StagiaireNom { get; set; } = string.Empty;

    [MaxLength(100)]
    public string StagiairePrenom { get; set; } = string.Empty;

    [MaxLength(200)]
    public string StagiaireEmail { get; set; } = string.Empty;

    public TypeEvaluation TypeEvaluation { get; set; }

    public DateOnly DateEvaluation { get; set; }

    [Precision(3, 1)]
    public decimal Note { get; set; }

    [Column(TypeName = "text")]
    public string Commentaire { get; set; } = string.Empty;

    public StatutEvaluation Statut { get; set; } = StatutEvaluation.EnAttente;
}
