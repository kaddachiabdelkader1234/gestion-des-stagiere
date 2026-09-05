using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Convention.Service.Models;
// The entity type `Convention` is shadowed by the `Convention.Service` root namespace inside this
// namespace, so alias it — same pattern Stagiaire.Service uses for `Stagiaire`.
using ConventionEntity = Convention.Service.Models.Convention;

namespace Convention.Service.Pdf;

/// <summary>Builds the convention de stage as a PDF with QuestPDF.</summary>
public interface IConventionPdfGenerator
{
    byte[] Generate(ConventionEntity convention);
}

public sealed class ConventionPdfGenerator : IConventionPdfGenerator
{
    /// <summary>
    /// Installed by the runtime stage of this service's Dockerfile (<c>fonts-dejavu-core</c>).
    ///
    /// Pinned rather than left to QuestPDF's default (Lato, which is not present in the aspnet
    /// image): without an installed match SkiaSharp silently falls back to whatever fontconfig
    /// offers, and if it finds nothing it emits a PDF with no /Font resource at all — a blank page
    /// returned as a successful 200. DejaVu also covers the French accents this document needs.
    /// </summary>
    private const string FontFamily = "DejaVu Sans";

    public byte[] Generate(ConventionEntity convention)
    {
        // QuestPDF 2024+ is a commercial/community-licensed library; the free community tier is
        // sufficient for on-premise STB usage and must be opted-in explicitly.
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
                    col.Item().Text("CONVENTION DE STAGE")
                        .FontSize(18).Bold().FontColor(Colors.Blue.Darken3);
                    col.Item().Text("STB — Société Tunisienne de Banque")
                        .FontSize(11).FontColor(Colors.Grey.Darken1);
                    col.Item().Text($"Référence : {convention.Id:N}")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingTop(20).Column(col =>
                {
                    col.Spacing(14);

                    col.Item().Text(
                        $"Entre la Société Tunisienne de Banque (STB), ci-après « l'Établissement », " +
                        $"et M/Mme {convention.StagiairePrenom} {convention.StagiaireNom}, ci-après " +
                        "« le Stagiaire ».");

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                        });

                        Row(table, "Stagiaire", $"{convention.StagiairePrenom} {convention.StagiaireNom}");
                        Row(table, "Email", convention.StagiaireEmail);
                        Row(table, "Département", convention.Departement);
                        Row(table, "Début", convention.DateDebut.ToString("dd/MM/yyyy"));
                        Row(table, "Fin", convention.DateFin.ToString("dd/MM/yyyy"));
                        Row(table, "Statut", SignatureLabel(convention.StatutSignature));
                    });

                    col.Item().Text("Le stagiaire s'engage à respecter le règlement intérieur de " +
                        "l'Établissement et à observer une stricte confidentialité sur les informations " +
                        "auxquelles il aura accès durant son stage.");

                    col.Item().Text("Fait en deux exemplaires originaux.");

                    col.Item().PaddingTop(8).Column(sig =>
                    {
                        sig.Spacing(4);
                        sig.Item().Text("Signature de l'Établissement")
                            .FontSize(10).FontColor(Colors.Grey.Darken2);
                        sig.Item().Border(1).Height(140).BorderColor(Colors.Grey.Lighten2);
                        sig.Item().Text($"Date : {convention.DateGeneration:dd/MM/yyyy}")
                            .FontSize(10).FontColor(Colors.Grey.Darken2);
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Medium));
                    t.Span("Généré le ");
                    t.Span(convention.DateGeneration.ToString("dd/MM/yyyy"));
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

    private static string SignatureLabel(StatutSignature statut) => statut switch
    {
        StatutSignature.Signee => "Signée",
        StatutSignature.Refusee => "Refusée",
        _ => "En attente de signature"
    };
}