namespace Stagiaire.Service.DTOs;

/// <summary>
/// Body for <c>POST /api/v1/candidatures/{id}/accepter</c>.
///
/// The admin confirms the département (which may differ from the one requested) and assigns an
/// encadrant chosen from the TRAINER users.
/// </summary>
public class CandidatureAccepterDto
{
    /// <summary>Final département. Omit to keep the one the candidate requested.</summary>
    public string? Departement { get; set; }

    /// <summary>auth-service <c>user_id</c> of the TRAINER who will supervise.</summary>
    public long EncadrantId { get; set; }

    /// <summary>
    /// Display name of that encadrant, stored denormalized so listing stagiaires needs no
    /// round-trip to auth-service.
    /// </summary>
    public string EncadrantNom { get; set; } = string.Empty;

    /// <summary>Optionally adjust the agreed dates while accepting.</summary>
    public DateOnly? DateDebut { get; set; }

    public DateOnly? DateFin { get; set; }
}

/// <summary>
/// Body for <c>POST /api/v1/candidatures/{id}/rejeter</c>. The reason is mandatory — a rejection
/// with no explanation is not actionable for the candidate.
/// </summary>
public class CandidatureRejeterDto
{
    public string MotifRejet { get; set; } = string.Empty;
}
