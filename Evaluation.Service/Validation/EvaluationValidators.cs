using FluentValidation;
using Evaluation.Service.DTOs;

namespace Evaluation.Service.Validation;

/// <summary>
/// Shared rules for the two evaluation payloads.
/// </summary>
/// <remarks>
/// <c>Note</c> was <c>InclusiveBetween(0, 99.9)</c> — the full range the <c>numeric(3,1)</c> column
/// allows — while the whole system treats a note as being out of 20 (Notification.Service even logs
/// "note {Note}/20"). A score of 87.5 was accepted and stored.
/// </remarks>
internal static class EvaluationRules
{
    internal const decimal NoteMax = 20m;

    internal static IRuleBuilderOptions<T, decimal> NoteRules<T>(this IRuleBuilder<T, decimal> rule) =>
        rule
            .InclusiveBetween(0m, NoteMax)
                .WithMessage($"La note doit être comprise entre 0 et {NoteMax}.")
            // One decimal place: the column is numeric(3,1), so 15.25 would be silently rounded.
            .PrecisionScale(3, 1, false)
                .WithMessage("La note ne peut avoir qu'un seul chiffre après la virgule.");

    internal static IRuleBuilderOptions<T, string> CommentaireRules<T>(
        this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("L'appréciation est obligatoire.")
            .MinimumLength(10).WithMessage("L'appréciation doit comporter au moins 10 caractères.")
            .MaximumLength(4000).WithMessage("L'appréciation ne peut pas dépasser 4000 caractères.");
}

public class EvaluationCreateDtoValidator : AbstractValidator<EvaluationCreateDto>
{
    public EvaluationCreateDtoValidator()
    {
        RuleFor(x => x.StagiaireId)
            .NotEmpty().WithMessage("Le stagiaire à évaluer est obligatoire.");

        RuleFor(x => x.TypeEvaluation)
            .IsInEnum().WithMessage("Le type d'évaluation doit être MiParcours ou Finale.");

        RuleFor(x => x.DateEvaluation)
            .NotEmpty().WithMessage("La date de l'évaluation est obligatoire.");

        RuleFor(x => x.Note).NoteRules();
        RuleFor(x => x.Commentaire).CommentaireRules();
    }
}

public class EvaluationUpdateDtoValidator : AbstractValidator<EvaluationUpdateDto>
{
    public EvaluationUpdateDtoValidator()
    {
        // No StagiaireId/EncadrantId rules: the DTO deliberately no longer carries them, since a
        // replacement PUT must not be able to change what visibility is enforced on.
        RuleFor(x => x.TypeEvaluation)
            .IsInEnum().WithMessage("Le type d'évaluation doit être MiParcours ou Finale.");

        RuleFor(x => x.DateEvaluation)
            .NotEmpty().WithMessage("La date de l'évaluation est obligatoire.");

        RuleFor(x => x.Note).NoteRules();
        RuleFor(x => x.Commentaire).CommentaireRules();
    }
}
