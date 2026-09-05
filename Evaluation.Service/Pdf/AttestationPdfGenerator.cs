using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Evaluation.Service.Models;
using EvaluationEntity = Evaluation.Service.Models.Evaluation;

namespace Evaluation.Service.Pdf;

/// <summary>
/// Builds an attestation de fin de stage as a PDF with QuestPDF.
/// Reuses the same font-installing approach as Convention.Service/ConventionPdfGenerator.
/// </summary>
public interface IAttestationPdfGenerator
{
    byte[] Generate(EvaluationEntity evaluation);
}

public sealed class AttestationPdfGenerator : IAttestationPdfGenerator
{
    /// <summary>
    /// Installed by the runtime stage of this service's Dockerfile (libfontconfig1 + fonts-dejavu-core).
    /// Same rationale as ConventionPdfGenerator: without an installed font, SkiaSharp silently
    /// produces a blank PDF with no /Font resource.
    /// </summary>
    private const string FontFamily = "DejaVu Sans";

    public byte[] Generate(EvaluationEntity evaluation)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontFamily(FontFamily).FontSize(11).FontColor(Colors.Grey.Darken3));

                page.Header().AlignCenter().Column(col =>
                {
                    col.Spacing(2);
                    col.Item().Text("ATTESTATION DE FIN DE STAGE")
                        .FontSize(18).Bold().FontColor(Colors.Blue.Darken3);
                    col.Item().Text("STB — Société Tunisienne de Banque")
                        .FontSize(11).FontColor(Colors.Grey.Darken1);
                    col.Item().Text($"Référence : {evaluation.Id:N}")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingTop(20).Column(col =>
                {
                    col.Spacing(14);

                    col.Item().Text(
                        $"La Société Tunisienne de Banque (STB) atteste que M/Mme " +
                        $"{evaluation.StagiairePrenom} {evaluation.StagiaireNom} " +
                        $"a effectué un stage au sein de son établissement.");

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                        });

                        Row(table, "Stagiaire", $"{evaluation.StagiairePrenom} {evaluation.StagiaireNom}");
                        Row(table, "Email", evaluation.StagiaireEmail);
                        Row(table, "Type d'évaluation", evaluation.TypeEvaluation switch
                        {
                            TypeEvaluation.MiParcours => "Mi-parcours",
                            TypeEvaluation.Finale => "Finale",
                            _ => evaluation.TypeEvaluation.ToString()
                        });
                        Row(table, "Date", evaluation.DateEvaluation.ToString("dd/MM/yyyy"));
                        Row(table, "Note", $"{evaluation.Note}/20");
                        Row(table, "Statut", evaluation.Statut switch
                        {
                            StatutEvaluation.Validee => "Validée",
                            StatutEvaluation.Soumise => "Soumise",
                            _ => "En attente"
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(evaluation.Commentaire))
                    {
                        col.Item().PaddingTop(10).Column(comment =>
                        {
                            comment.Item().Text("Commentaire de l'encadrant :").Bold().FontSize(10);
                            comment.Item().Text(evaluation.Commentaire).FontSize(10);
                        });
                    }

                    col.Item().PaddingTop(10).Text(
                        "Le stagiaire s'est acquitté des missions qui lui ont été confiées " +
                        "avec sérieux et professionnalisme durant la période de stage.");

                    col.Item().Text("Fait en un exemplaire original.");

                    col.Item().PaddingTop(8).Column(sig =>
                    {
                        sig.Spacing(4);
                        sig.Item().Text("Signature de l'Établissement")
                            .FontSize(10).FontColor(Colors.Grey.Darken2);
                        sig.Item().Border(1).Height(140).BorderColor(Colors.Grey.Lighten2);
                        sig.Item().Text($"Date : {DateTime.UtcNow:dd/MM/yyyy}")
                            .FontSize(10).FontColor(Colors.Grey.Darken2);
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Medium));
                    t.Span("Généré le ");
                    t.Span(DateTime.UtcNow.ToString("dd/MM/yyyy"));
                    t.Span(" — Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void Row(TableDescriptor table, string label, string value)
    {
        table.Cell().Border(1).Padding(6).Text(label).Bold().FontSize(10);
        table.Cell().Border(1).Padding(6).Text(value).FontSize(10);
    }
}
