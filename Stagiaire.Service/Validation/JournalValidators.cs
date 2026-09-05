using FluentValidation;
using Stagiaire.Service.DTOs;

namespace Stagiaire.Service.Validation;

public class JournalEntryCreateDtoValidator : AbstractValidator<JournalEntryCreateDto>
{
    public JournalEntryCreateDtoValidator()
    {
        RuleFor(x => x.DateEntree)
            .NotEmpty().WithMessage("La date de l'entrée est obligatoire.")
            .Must(JournalRules.IsNotTooFarAhead)
                .WithMessage("La date de l'entrée ne peut pas être postérieure de plus de 7 jours.");

        RuleFor(x => x.Texte)
            .NotEmpty().WithMessage("Le texte de l'entrée est obligatoire.")
            .MinimumLength(10).WithMessage("Le texte doit comporter au moins 10 caractères.")
            .MaximumLength(4000).WithMessage("Le texte ne peut pas dépasser 4000 caractères.");
    }
}

public class JournalEntryUpdateDtoValidator : AbstractValidator<JournalEntryUpdateDto>
{
    public JournalEntryUpdateDtoValidator()
    {
        RuleFor(x => x.DateEntree)
            .NotEmpty().WithMessage("La date de l'entrée est obligatoire.")
            .Must(JournalRules.IsNotTooFarAhead)
                .WithMessage("La date de l'entrée ne peut pas être postérieure de plus de 7 jours.");

        RuleFor(x => x.Texte)
            .NotEmpty().WithMessage("Le texte de l'entrée est obligatoire.")
            .MinimumLength(10).WithMessage("Le texte doit comporter au moins 10 caractères.")
            .MaximumLength(4000).WithMessage("Le texte ne peut pas dépasser 4000 caractères.");
    }
}

public class JournalCommentaireDtoValidator : AbstractValidator<JournalCommentaireDto>
{
    public JournalCommentaireDtoValidator()
    {
        RuleFor(x => x.Commentaire)
            .NotEmpty().WithMessage("Le commentaire est obligatoire.")
            .MinimumLength(3).WithMessage("Le commentaire doit comporter au moins 3 caractères.")
            .MaximumLength(2000).WithMessage("Le commentaire ne peut pas dépasser 2000 caractères.");
    }
}

internal static class JournalRules
{
    /// <summary>
    /// Catches a mistyped year (2126 for 2026) without rejecting an entry dated to the end of the
    /// current week, which is the natural way to log "week of…" on a Monday.
    /// </summary>
    /// <remarks>
    /// Evaluated per call, not captured once: a validator instance outlives any single day.
    /// </remarks>
    internal static bool IsNotTooFarAhead(DateOnly date) =>
        date <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);
}
