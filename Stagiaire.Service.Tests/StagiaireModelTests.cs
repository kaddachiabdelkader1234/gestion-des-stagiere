using Stagiaire.Service.Models;
using StagiaireEntity = Stagiaire.Service.Models.Stagiaire;

namespace Stagiaire.Service.Tests;

public class StagiaireModelTests
{
    [Fact]
    public void NewStagiaire_DefaultsToEnAttente()
    {
        var stagiaire = new StagiaireEntity();
        Assert.Equal(StatutStagiaire.EnAttente, stagiaire.Statut);
    }

    [Fact]
    public void StatutStagiaire_HasAllExpectedValues()
    {
        // Verify the enum has the values the frontend expects
        Assert.True(Enum.IsDefined(typeof(StatutStagiaire), StatutStagiaire.EnAttente));
        Assert.True(Enum.IsDefined(typeof(StatutStagiaire), StatutStagiaire.Acceptee));
        Assert.True(Enum.IsDefined(typeof(StatutStagiaire), StatutStagiaire.Rejetee));
        Assert.True(Enum.IsDefined(typeof(StatutStagiaire), StatutStagiaire.EnCours));
        Assert.True(Enum.IsDefined(typeof(StatutStagiaire), StatutStagiaire.Termine));
    }

    [Fact]
    public void TypeStage_HasExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(TypeStage), TypeStage.PFE));
        Assert.True(Enum.IsDefined(typeof(TypeStage), TypeStage.StageEte));
    }

    [Fact]
    public void Stagiaire_CanSetAllFields()
    {
        var stagiaire = new StagiaireEntity
        {
            Id = Guid.NewGuid(),
            UtilisateurId = 42,
            Nom = "Kaddachi",
            Prenom = "Gadour",
            Email = "test@gmail.com",
            Departement = "IT",
            TypeStage = TypeStage.PFE,
            Ecole = "ENIT",
            DateDebut = new DateOnly(2026, 9, 1),
            DateFin = new DateOnly(2027, 2, 28),
            Motivation = "Test",
            Statut = StatutStagiaire.Acceptee,
            EncadrantId = 1,
            EncadrantNom = "Encadrant Test",
            DateSoumission = DateTime.UtcNow,
            DateDecision = DateTime.UtcNow
        };

        Assert.Equal("Kaddachi", stagiaire.Nom);
        Assert.Equal("Gadour", stagiaire.Prenom);
        Assert.Equal(StatutStagiaire.Acceptee, stagiaire.Statut);
        Assert.Equal(42, stagiaire.UtilisateurId);
        Assert.Equal(1, stagiaire.EncadrantId);
    }
}
