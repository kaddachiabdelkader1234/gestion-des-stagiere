using System.Linq.Expressions;
using System.Security.Claims;
using Smartek.Common.Security;
using Evaluation.Service.Models;
using EvaluationEntity = Evaluation.Service.Models.Evaluation;

namespace Evaluation.Service.Security;

/// <summary>
/// The single definition of "which evaluations may this caller touch".
///
/// Mirrors <c>Stagiaire.Service/Security/StagiaireVisibility.cs</c>, deliberately: the gateway
/// permits ADMIN, TRAINER **and** LEARNER on every <c>GET /api/v1/evaluations/**</c>, so nothing
/// upstream distinguishes them and this is the only boundary. Before this existed, any authenticated
/// learner could list every evaluation in the bank — note, comment and all — and <c>?encadrantId=</c>
/// was a filter the client chose to apply rather than one imposed on it.
///
/// Each rule is written once as an <see cref="Expression"/> and used both as SQL and against a loaded
/// entity, so the list query and the by-id check cannot disagree about what exists.
/// </summary>
public static class EvaluationVisibility
{
    /// <summary>
    /// Evaluations the caller may <b>read</b>: an admin sees everything, an encadrant those of their
    /// assigned stagiaires, a learner only their own result.
    /// </summary>
    /// <remarks>
    /// A caller with no <c>userId</c> claim matches nothing — the safe failure direction.
    ///
    /// A trainer also matches evaluations about themselves, for the same reason as in
    /// <c>StagiaireVisibility</c>: it keeps this predicate identical to the by-id check, so a row
    /// <c>GET /{id}</c> will serve always appears in <c>GET /</c> too.
    /// </remarks>
    public static Expression<Func<EvaluationEntity, bool>> ReadableBy(ClaimsPrincipal caller)
    {
        if (caller.IsAdmin())
        {
            return _ => true;
        }

        var callerId = caller.GetUserId();

        if (callerId is null)
        {
            return _ => false;
        }

        if (caller.IsTrainer())
        {
            return x => x.EncadrantId == callerId || x.UtilisateurId == callerId;
        }

        return x => x.UtilisateurId == callerId;
    }

    /// <summary>
    /// Narrows a query to the readable set. Apply <b>before</b> any client-supplied filter, so a
    /// filter can only ever subtract from what the caller is entitled to see.
    /// </summary>
    public static IQueryable<EvaluationEntity> ApplyReadScope(
        this IQueryable<EvaluationEntity> source,
        ClaimsPrincipal caller) => source.Where(ReadableBy(caller));

    /// <summary>True when the caller may read this already-loaded evaluation.</summary>
    public static bool CanRead(this ClaimsPrincipal caller, EvaluationEntity evaluation) =>
        ReadableBy(caller).Compile()(evaluation);

    /// <summary>
    /// True when the caller may <b>modify</b> an existing evaluation: an admin, or the encadrant it is
    /// assigned to.
    /// </summary>
    /// <remarks>
    /// Deliberately **not** <see cref="CanRead"/>. The read predicate also matches evaluations *about*
    /// the caller, so reusing it here would let a trainer who is themselves on a stage edit their own
    /// evaluation. Reading your own result is fine; grading it is not.
    /// </remarks>
    public static bool CanModify(this ClaimsPrincipal caller, EvaluationEntity evaluation)
    {
        if (caller.IsAdmin())
        {
            return true;
        }

        var callerId = caller.GetUserId();

        return callerId is not null
            && caller.IsTrainer()
            && evaluation.EncadrantId == callerId;
    }

    /// <summary>
    /// True when the caller may write an evaluation <b>about this stagiaire</b> — the assigned
    /// encadrant, or an admin.
    /// </summary>
    /// <remarks>
    /// Checked against the projection rather than the evaluation, because on create there is no
    /// evaluation yet and the only thing the client supplied is a <c>StagiaireId</c>. This is what
    /// stops a trainer recording a note for someone else's stagiaire.
    /// </remarks>
    public static bool CanEvaluate(this ClaimsPrincipal caller, StagiaireAffectation affectation)
    {
        if (caller.IsAdmin())
        {
            return true;
        }

        var callerId = caller.GetUserId();

        return callerId is not null
            && caller.IsTrainer()
            && affectation.EncadrantId == callerId;
    }
}
