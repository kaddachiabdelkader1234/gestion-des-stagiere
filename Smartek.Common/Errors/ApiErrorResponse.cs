using System.Text.Json.Serialization;

namespace Smartek.Common.Errors;

/// <summary>
/// The single error shape every endpoint returns on failure.
///
/// Matches what the Angular client parses in <c>core/http/api-error.ts</c>:
/// <code>
/// { "error": "Le département est obligatoire.", "code": "VALIDATION_ERROR",
///   "errors": { "departement": ["..."] } }
/// </code>
///
/// Deliberately not ASP.NET's default <c>ProblemDetails</c>: that leaks a type URI and, in
/// Development, a full exception dump including stack traces. The brief requires no raw stack
/// traces reaching the browser.
/// </summary>
public sealed class ApiErrorResponse
{
    /// <summary>Human-readable message. Safe to display — never contains exception details.</summary>
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    /// <summary>Stable machine-readable code from <see cref="ErrorCodes"/>.</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = ErrorCodes.InternalError;

    /// <summary>Per-field validation messages. Omitted when the failure is not field-specific.</summary>
    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]>? Errors { get; set; }

    /// <summary>
    /// Correlates a client-visible error with the server logs. For a 500 this is the only handle
    /// on the underlying exception, since the message itself is deliberately generic.
    /// </summary>
    [JsonPropertyName("traceId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; set; }

    public static ApiErrorResponse Create(string message, string code, string? traceId = null) => new()
    {
        Error = message,
        Code = code,
        TraceId = traceId
    };
}

/// <summary>
/// Error codes clients may branch on. Keep these stable — the frontend and tests match on them.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string BadRequest = "BAD_REQUEST";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string InternalError = "INTERNAL_ERROR";
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
}
