using Microsoft.AspNetCore.Http;

namespace Smartek.Common.Errors;

/// <summary>
/// Base for failures a caller caused and can act on. The middleware turns these into the
/// documented status code; anything else becomes a 500 with a generic message.
/// </summary>
public abstract class ApiException : Exception
{
    protected ApiException(string message, string code, int statusCode) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public int StatusCode { get; }

    /// <summary>Populated for validation failures; null otherwise.</summary>
    public IDictionary<string, string[]>? Errors { get; protected init; }
}

/// <summary>404 — the addressed resource does not exist.</summary>
public sealed class NotFoundException : ApiException
{
    public NotFoundException(string message)
        : base(message, ErrorCodes.NotFound, StatusCodes.Status404NotFound)
    {
    }

    /// <summary>Builds the standard "&lt;resource&gt; &lt;id&gt; est introuvable." message.</summary>
    public static NotFoundException For(string resource, object id) =>
        new($"{resource} '{id}' est introuvable.");
}

/// <summary>400 — the request is malformed or semantically invalid beyond field validation.</summary>
public sealed class BadRequestException : ApiException
{
    public BadRequestException(string message, string code = ErrorCodes.BadRequest)
        : base(message, code, StatusCodes.Status400BadRequest)
    {
    }
}

/// <summary>
/// 409 — the request conflicts with current state (duplicate email, an already-signed convention,
/// a stagiaire evaluated twice).
/// </summary>
public sealed class ConflictException : ApiException
{
    public ConflictException(string message)
        : base(message, ErrorCodes.Conflict, StatusCodes.Status409Conflict)
    {
    }
}

/// <summary>
/// 403 — the caller is authenticated and the resource exists, but this action is not theirs to take
/// (an encadrant writing a journal entry that belongs to the stagiaire).
/// </summary>
/// <remarks>
/// Only appropriate when the caller may already see the resource. Where the caller has no business
/// knowing it exists at all, throw <see cref="NotFoundException"/> instead — a 403 confirms the id.
/// </remarks>
public sealed class ForbiddenException : ApiException
{
    public ForbiddenException(string message)
        : base(message, ErrorCodes.Forbidden, StatusCodes.Status403Forbidden)
    {
    }
}

/// <summary>
/// 400 with per-field detail. Thrown for validation discovered in a handler; FluentValidation's
/// automatic model validation is converted to the same shape by
/// <c>ValidationProblemFactory</c>, so both paths look identical to the client.
/// </summary>
public sealed class ValidationException : ApiException
{
    public ValidationException(IDictionary<string, string[]> errors)
        : base("Un ou plusieurs champs sont invalides.", ErrorCodes.ValidationError, StatusCodes.Status400BadRequest)
    {
        Errors = errors;
    }

    public ValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = new[] { message } })
    {
    }
}
