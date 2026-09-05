using FluentValidation;
using Stagiaire.Service.DTOs;

namespace Stagiaire.Service.Validation;

public class StagiaireUpdateDtoValidator : AbstractValidator<StagiaireUpdateDto>
{
    public StagiaireUpdateDtoValidator()
    {
        RuleFor(x => x.Nom)
            .NotEmpty().WithMessage("Le nom est obligatoire.")
            .MaximumLength(100).WithMessage("Le nom ne peut pas dépasser 100 caractères.");

        RuleFor(x => x.Prenom)
            .NotEmpty().WithMessage("Le prénom est obligatoire.")
            .MaximumLength(100).WithMessage("Le prénom ne peut pas dépasser 100 caractères.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("L'email est obligatoire.")
            .EmailAddress().WithMessage("L'email n'est pas valide.")
            .MaximumLength(200).WithMessage("L'email ne peut pas dépasser 200 caractères.");

        RuleFor(x => x.Departement)
            .NotEmpty().WithMessage("Le département est obligatoire.")
            .MaximumLength(150).WithMessage("Le département ne peut pas dépasser 150 caractères.");

        RuleFor(x => x.Ecole)
            .NotEmpty().WithMessage("L'école est obligatoire.")
            .MaximumLength(200).WithMessage("L'école ne peut pas dépasser 200 caractères.");

        RuleFor(x => x.TypeStage)
            .IsInEnum().WithMessage("Le type de stage doit être PFE, StageEte ou StageOuvrier.");

        RuleFor(x => x.MotifRejet)
            .MaximumLength(1000).WithMessage("Le motif ne peut pas dépasser 1000 caractères.");

        RuleFor(x => x.DateDebut)
            .NotEmpty().WithMessage("La date de début est obligatoire.");

        RuleFor(x => x.DateFin)
            .NotEmpty().WithMessage("La date de fin est obligatoire.")
            .GreaterThanOrEqualTo(x => x.DateDebut)
                .WithMessage("La date de fin doit être postérieure ou égale à la date de début.");

        RuleFor(x => x)
            .Must(dto => dto.DateFin.DayNumber - dto.DateDebut.DayNumber <= 366)
            .WithName("dateFin")
            .WithMessage("La durée du stage ne peut pas dépasser 12 mois.")
            .When(dto => dto.DateFin >= dto.DateDebut);

        RuleFor(x => x.Statut)
            .IsInEnum().WithMessage("Le statut fourni n'est pas reconnu.");
    }
}
