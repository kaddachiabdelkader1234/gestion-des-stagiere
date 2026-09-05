using FluentValidation;
using Convention.Service.DTOs;

namespace Convention.Service.Validation;

public class ConventionUpdateDtoValidator : AbstractValidator<ConventionUpdateDto>
{
    public ConventionUpdateDtoValidator()
    {
        RuleFor(x => x.StagiaireId).NotEmpty();
        RuleFor(x => x.StagiaireNom).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StagiairePrenom).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StagiaireEmail).NotEmpty().MaximumLength(200).EmailAddress();
        RuleFor(x => x.Departement).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DateDebut).NotEmpty();
        RuleFor(x => x.DateFin).NotEmpty();
        RuleFor(x => x.DateGeneration).NotEmpty();

        // Was `RuleFor(x => x.CheminPdf).NotEmpty()`, which rejected every update: the PDF is
        // generated server-side by POST /{id}/generer, so a client has no path to send. Only the
        // length still matters, and only when a value is actually supplied.
        RuleFor(x => x.CheminPdf).MaximumLength(500);

        RuleFor(x => x.DateFin)
            .GreaterThanOrEqualTo(x => x.DateDebut)
            .WithMessage("La date de fin doit être postérieure ou égale à la date de début.");
    }
}
