using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Smartek.Common.Errors;

namespace Smartek.Common.Storage;

/// <summary>
/// Stores uploaded files on a local disk volume.
///
/// Local disk rather than S3 is a settled decision for this project. The root comes from
/// <c>Storage:RootPath</c> (default <c>/app/storage</c>, mounted as a Docker volume so files
/// survive container rebuilds).
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Validates and saves an upload, returning the stored path relative to the storage root.
    /// </summary>
    /// <param name="file">The uploaded file.</param>
    /// <param name="subFolder">Logical folder, e.g. "cv".</param>
    /// <param name="allowedExtensions">Permitted extensions, lowercase and dot-prefixed.</param>
    /// <param name="maxBytes">Hard size limit.</param>
    Task<string> SaveAsync(
        IFormFile file,
        string subFolder,
        string[] allowedExtensions,
        long maxBytes,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a stored file for reading. Throws <see cref="NotFoundException"/> if absent.</summary>
    Stream OpenRead(string relativePath);

    bool Exists(string relativePath);

    void Delete(string relativePath);

    /// <summary>
    /// Persists a server-generated file (e.g. a PDF) under the storage root, returning its relative
    /// path. Unlike <see cref="SaveAsync"/> there is no client-supplied name or size limit; the
    /// extension is supplied by the caller and is whitelisted here to PDF only.
    /// </summary>
    Task<string> SaveGeneratedAsync(
        Stream content,
        string subFolder,
        string extension,
        CancellationToken cancellationToken = default);
}

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;
    private readonly ILogger<LocalFileStorage> _logger;

    public LocalFileStorage(IConfiguration configuration, ILogger<LocalFileStorage> logger)
    {
        _rootPath = configuration["Storage:RootPath"] ?? "/app/storage";
        _logger = logger;

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(
        IFormFile file,
        string subFolder,
        string[] allowedExtensions,
        long maxBytes,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationException("fichier", "Le fichier est vide ou absent.");
        }

        if (file.Length > maxBytes)
        {
            throw new ValidationException(
                "fichier",
                $"Le fichier dépasse la taille maximale de {maxBytes / (1024 * 1024)} Mo.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            throw new ValidationException(
                "fichier",
                $"Extension non autorisée. Formats acceptés : {string.Join(", ", allowedExtensions)}.");
        }

        var folder = Path.Combine(_rootPath, subFolder);
        Directory.CreateDirectory(folder);

        // A generated name, never the client's: an uploaded filename can carry path traversal
        // ("../../etc/passwd") or collide with an existing file.
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(folder, storedName);

        await using (var target = File.Create(absolutePath))
        {
            await file.CopyToAsync(target, cancellationToken);
        }

        _logger.LogInformation(
            "Stored upload {Original} as {Stored} ({Bytes} bytes)",
            file.FileName, storedName, file.Length);

        // Forward slashes so the value is portable between Windows and Linux containers.
        return $"{subFolder}/{storedName}";
    }

    public Stream OpenRead(string relativePath)
    {
        var absolutePath = ResolveWithinRoot(relativePath);

        if (!File.Exists(absolutePath))
        {
            throw new NotFoundException("Le fichier demandé est introuvable sur le serveur.");
        }

        return File.OpenRead(absolutePath);
    }

    public bool Exists(string relativePath)
    {
        try
        {
            return File.Exists(ResolveWithinRoot(relativePath));
        }
        catch (BadRequestException)
        {
            return false;
        }
    }

    public async Task<string> SaveGeneratedAsync(
        Stream content,
        string subFolder,
        string extension,
        CancellationToken cancellationToken = default)
    {
        if (content is null || content.Length == 0)
        {
            throw new ValidationException("fichier", "Le fichier généré est vide.");
        }

        // Caller-supplied extension — restrict it to PDF so a future bug cannot write an arbitrary
        // extension, and normalise the shape.
        if (!extension.StartsWith(".", StringComparison.Ordinal))
        {
            extension = "." + extension;
        }

        extension = extension.ToLowerInvariant();

        if (extension != ".pdf")
        {
            throw new ValidationException("fichier", "Format non pris en charge.");
        }

        var folder = Path.Combine(_rootPath, subFolder);
        Directory.CreateDirectory(folder);

        var storedName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(folder, storedName);

        await using (var target = File.Create(absolutePath))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        _logger.LogInformation(
            "Stored generated file {Stored} ({Bytes} bytes)",
            storedName, content.Length);

        // Forward slashes so the value is portable between Windows and Linux containers.
        return $"{subFolder}/{storedName}";
    }

    public void Delete(string relativePath)
    {
        var absolutePath = ResolveWithinRoot(relativePath);

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
    }

    /// <summary>
    /// Resolves a stored relative path and refuses anything that escapes the storage root.
    /// </summary>
    /// <remarks>
    /// Paths come from the database, but treating them as trusted would turn any future write bug
    /// into arbitrary file read. Checked here, once, rather than at each call site.
    /// </remarks>
    private string ResolveWithinRoot(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new BadRequestException("Chemin de fichier invalide.");
        }

        var root = Path.GetFullPath(_rootPath);
        var candidate = Path.GetFullPath(Path.Combine(root, relativePath));

        if (!candidate.StartsWith(root, StringComparison.Ordinal))
        {
            _logger.LogWarning("Rejected path traversal attempt: {Path}", relativePath);
            throw new BadRequestException("Chemin de fichier invalide.");
        }

        return candidate;
    }
}
