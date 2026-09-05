using System.Linq.Expressions;
using System.Security.Claims;
using Smartek.Common.Security;
using StagiaireEntity = Stagiaire.Service.Models.Stagiaire;

namespace Stagiaire.Service.Security;

/// <summary>
/// The single definition of "which stagiaire records may this caller touch".
///
/// This is a security boundary, not a convenience filter: the gateway permits ADMIN, TRAINER and
/// LEARNER alike on every <c>GET /api/v1/stagiaires/**</c>, so nothing upstream distinguishes them.
/// Identity comes from the validated JWT (<c>userId</c> claim), never from a query parameter or
/// request body, which is what stops a trainer reading another trainer's roster by passing a
/// different <c>encadrantId</c>.
///
/// Previously this logic was copy-pasted across <c>StagiairesController.ApplyVisibilityScope</c>,
/// <c>CandidaturesController.FindOwnedAsync</c> and <c>FindVisibleAsync</c>, and the journal
/// endpoints would have been a fourth. Each predicate below is written once and reused both as SQL
/// (via <see cref="IQueryable{T}"/>) and against a loaded entity, so the two can never drift.
/// </summary>
public static class StagiaireVisibility
{
    /// <summary>
    /// Records the caller may <b>read</b>: an admin sees everything, a trainer sees the stagiaires
    /// assigned to them, and anyone else sees only their own record.
    /// </summary>
    /// <remarks>
    /// A caller with no <c>userId</c> claim matches nothing — the safe failure direction, since the
    /// alternative would be an unscoped query.
    ///
    /// A trainer also matches records they own. That covers the trainer-who-also-applied case and,
    /// more importantly, keeps this predicate identical to the one used for by-id reads: a row that
    /// <c>GET /{id}</c> will serve must also appear in <c>GET /</c>, or the two disagree about what
    /// exists.
    /// </remarks>
    public static Expression<Func<StagiaireEntity, bool>> ReadableBy(ClaimsPrincipal caller)
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
            return x => x.UtilisateurId == callerId || x.EncadrantId == callerId;
        }

        return x => x.UtilisateurId == callerId;
    }

    /// <summary>
    /// Records the caller may <b>modify</b>: an admin, or the learner who owns the record.
    /// </summary>
    /// <remarks>
    /// Deliberately narrower than <see cref="ReadableBy"/> — an encadrant reads their stagiaires'
    /// data but does not edit their candidature. Encadrant-authored writes (a journal comment) have
    /// their own rule and must not be routed through this one.
    /// </remarks>
    public static Expression<Func<StagiaireEntity, bool>> WritableBy(ClaimsPrincipal caller)
    {
        if (caller.IsAdmin())
        {
            return _ => true;
        }

        var callerId = caller.GetUserId();

        return callerId is null
            ? _ => false
            : x => x.UtilisateurId == callerId;
    }

    /// <summary>
    /// Narrows a query to the readable set. Apply this <b>before</b> any client-supplied filter, so
    /// a filter can only ever subtract from what the caller is entitled to see.
    /// </summary>
    public static IQueryable<StagiaireEntity> ApplyReadScope(
        this IQueryable<StagiaireEntity> source,
        ClaimsPrincipal caller) => source.Where(ReadableBy(caller));

    /// <summary>True when the caller may read this already-loaded record.</summary>
    public static bool CanRead(this ClaimsPrincipal caller, StagiaireEntity stagiaire) =>
        ReadableBy(caller).Compile()(stagiaire);

    /// <summary>True when the caller may modify this already-loaded record.</summary>
    public static bool CanWrite(this ClaimsPrincipal caller, StagiaireEntity stagiaire) =>
        WritableBy(caller).Compile()(stagiaire);

    /// <summary>
    /// True when the caller is the encadrant assigned to this stagiaire, or an admin — the rule for
    /// writes that belong to the supervisor rather than the trainee.
    /// </summary>
    public static bool CanSupervise(this ClaimsPrincipal caller, StagiaireEntity stagiaire)
    {
        if (caller.IsAdmin())
        {
            return true;
        }

        var callerId = caller.GetUserId();

        return callerId is not null
            && caller.IsTrainer()
            && stagiaire.EncadrantId == callerId;
    }
}
