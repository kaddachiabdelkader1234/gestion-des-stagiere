using FluentValidation;
using Stagiaire.Service.DTOs;

namespace Stagiaire.Service.Validation;

public class CandidatureCreateDtoValidator : AbstractValidator<CandidatureCreateDto>
{
    public CandidatureCreateDtoValidator()
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
            .NotEmpty().WithMessage("Le département souhaité est obligatoire.")
            .MaximumLength(150).WithMessage("Le département ne peut pas dépasser 150 caractères.");

        RuleFor(x => x.Ecole)
            .NotEmpty().WithMessage("L'école est obligatoire.")
            .MaximumLength(200).WithMessage("L'école ne peut pas dépasser 200 caractères.");

        RuleFor(x => x.TypeStage)
            .IsInEnum().WithMessage("Le type de stage doit être PFE, StageEte ou StageOuvrier.");

        RuleFor(x => x.DateDebut)
            .NotEmpty().WithMessage("La date de début souhaitée est obligatoire.");

        RuleFor(x => x.DateFin)
            .NotEmpty().WithMessage("La date de fin souhaitée est obligatoire.")
            .GreaterThanOrEqualTo(x => x.DateDebut)
                .WithMessage("La date de fin doit être postérieure ou égale à la date de début.");

        RuleFor(x => x)
            .Must(dto => dto.DateFin.DayNumber - dto.DateDebut.DayNumber <= 366)
            .WithName("dateFin")
            .WithMessage("La durée du stage ne peut pas dépasser 12 mois.")
            .When(dto => dto.DateFin >= dto.DateDebut);

        RuleFor(x => x.Motivation)
            .MaximumLength(2000).WithMessage("La motivation ne peut pas dépasser 2000 caractères.");
    }
}

public class CandidatureAccepterDtoValidator : AbstractValidator<CandidatureAccepterDto>
{
    public CandidatureAccepterDtoValidator()
    {
        RuleFor(x => x.EncadrantId)
            .GreaterThan(0).WithMessage("Un encadrant doit être assigné.");

        RuleFor(x => x.EncadrantNom)
            .NotEmpty().WithMessage("Le nom de l'encadrant est obligatoire.")
            .MaximumLength(200).WithMessage("Le nom de l'encadrant ne peut pas dépasser 200 caractères.");

        RuleFor(x => x.Departement)
            .MaximumLength(150).WithMessage("Le département ne peut pas dépasser 150 caractères.");

        // Both-or-neither: supplying one bound only would silently mix a new date with an old one.
        RuleFor(x => x.DateFin)
            .NotNull().WithMessage("Fournissez les deux dates ou aucune.")
            .When(x => x.DateDebut is not null);

        RuleFor(x => x.DateDebut)
            .NotNull().WithMessage("Fournissez les deux dates ou aucune.")
            .When(x => x.DateFin is not null);

        RuleFor(x => x.DateFin)
            .GreaterThanOrEqualTo(x => x.DateDebut!.Value)
                .WithMessage("La date de fin doit être postérieure ou égale à la date de début.")
            .When(x => x.DateDebut is not null && x.DateFin is not null);
    }
}

public class CandidatureRejeterDtoValidator : AbstractValidator<CandidatureRejeterDto>
{
    public CandidatureRejeterDtoValidator()
    {
        RuleFor(x => x.MotifRejet)
            .NotEmpty().WithMessage("Le motif de rejet est obligatoire.")
            .MinimumLength(10).WithMessage("Le motif doit comporter au moins 10 caractères.")
            .MaximumLength(1000).WithMessage("Le motif ne peut pas dépasser 1000 caractères.");
    }
}
