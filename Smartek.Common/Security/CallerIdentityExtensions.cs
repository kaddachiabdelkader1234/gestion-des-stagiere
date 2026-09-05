using System.Security.Claims;

namespace Smartek.Common.Security;

/// <summary>
/// Reads the caller's identity from the validated JWT.
///
/// The token is minted by auth-service with claims <c>sub</c> (email), <c>role</c> and
/// <c>userId</c>, and is signature-checked by the JWT middleware before any of this runs — so these
/// values are trustworthy in a way request-body or query-string values are not.
///
/// This is what makes "an encadrant only ever sees their own assigned stagiaires" a real boundary
/// rather than a filter the client chooses to apply.
/// </summary>
public static class CallerIdentityExtensions
{
    public const string RoleAdmin = "ADMIN";
    public const string RoleTrainer = "TRAINER";
    public const string RoleLearner = "LEARNER";

    /// <summary>
    /// auth-service <c>users.user_id</c> of the caller, or null if the claim is absent/unparseable.
    /// </summary>
    public static long? GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirst("userId")?.Value;

        return long.TryParse(raw, out var userId) ? userId : null;
    }

    /// <summary>Caller's email — the token's <c>sub</c>.</summary>
    public static string? GetEmail(this ClaimsPrincipal principal) =>
        principal.FindFirst("sub")?.Value
        ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    /// <summary>
    /// True when the caller holds the given role.
    /// </summary>
    /// <remarks>
    /// Checks both the raw <c>role</c> claim and the mapped <see cref="ClaimTypes.Role"/> claims,
    /// because each service's <c>JwtRoleClaimsTransformation</c> copies <c>role</c> across and also
    /// adds <c>Encadrant</c> for a TRAINER.
    /// </remarks>
    public static bool HasRole(this ClaimsPrincipal principal, string role) =>
        principal.FindAll("role").Any(claim => Matches(claim.Value, role))
        || principal.FindAll(ClaimTypes.Role).Any(claim => Matches(claim.Value, role));

    public static bool IsAdmin(this ClaimsPrincipal principal) => principal.HasRole(RoleAdmin);

    public static bool IsTrainer(this ClaimsPrincipal principal) => principal.HasRole(RoleTrainer);

    public static bool IsLearner(this ClaimsPrincipal principal) => principal.HasRole(RoleLearner);

    private static bool Matches(string value, string role) =>
        string.Equals(value, role, StringComparison.OrdinalIgnoreCase);
}
