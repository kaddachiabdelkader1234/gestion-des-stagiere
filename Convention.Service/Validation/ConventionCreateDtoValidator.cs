using FluentValidation;
using Convention.Service.DTOs;

namespace Convention.Service.Validation;

public class ConventionCreateDtoValidator : AbstractValidator<ConventionCreateDto>
{
    public ConventionCreateDtoValidator()
    {
        RuleFor(x => x.StagiaireId).NotEmpty();
        RuleFor(x => x.StagiaireNom).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StagiairePrenom).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StagiaireEmail).NotEmpty().MaximumLength(200).EmailAddress();
        RuleFor(x => x.Departement).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DateDebut).NotEmpty();
        RuleFor(x => x.DateFin).NotEmpty();
        RuleFor(x => x.DateGeneration).NotEmpty();
        // CheminPdf is no longer required: it is generated server-side by POST /{id}/generer.
        RuleFor(x => x.CheminPdf).MaximumLength(500);

        // A reversed range would render a nonsensical PDF, so reject it before it can be generated.
        RuleFor(x => x.DateFin)
            .GreaterThanOrEqualTo(x => x.DateDebut)
            .WithMessage("La date de fin doit être postérieure ou égale à la date de début.");
    }
}