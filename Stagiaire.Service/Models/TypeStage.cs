namespace Stagiaire.Service.Models;

/// <summary>
/// Type of internship requested, per the brief: PFE / STAGE_ETE / STAGE_OUVRIER.
///
/// Crosses the wire as the member name — "PFE", "StageEte", "StageOuvrier".
/// </summary>
public enum TypeStage
{
    /// <summary>Projet de fin d'études.</summary>
    PFE,

    /// <summary>Stage d'été.</summary>
    StageEte,

    /// <summary>Stage ouvrier / d'initiation.</summary>
    StageOuvrier
}
