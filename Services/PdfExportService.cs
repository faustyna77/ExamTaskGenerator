using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.Json;
using ExamCreateApp.Models;

namespace ExamCreateApp.Services;

public class PdfExportService
{
    private readonly ILogger<PdfExportService> _logger;

    public PdfExportService(ILogger<PdfExportService> logger)
    {
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community; // Darmowa licencja
    }

    public byte[] GeneratePdfFromTask(GeneratedTask task)
    {
        try
        {
            // Parsuj JSON
            var taskData = JsonSerializer.Deserialize<TaskGenerationResponse>(
                task.GeneratedText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (taskData?.Tasks == null || taskData.Tasks.Count == 0)
            {
                throw new InvalidOperationException("No tasks found in JSON");
            }

            // Generuj PDF
            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(12).FontFamily("Arial"));

                    page.Header()
                        .Text("Wygenerowane Zadanie z Fizyki")
                        .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Spacing(10);

                            // Metadata
                            column.Item().Text($"Prompt: {task.Prompt}").FontSize(10).Italic();
                            column.Item().Text($"Data: {task.CreatedAt:yyyy-MM-dd HH:mm}").FontSize(10).Italic();
                            column.Item().LineHorizontal(1);

                            // Każde zadanie
                            int taskNumber = 1;
                            foreach (var t in taskData.Tasks)
                            {
                                column.Item().PaddingTop(10).Column(taskCol =>
                                {
                                    // Treść zadania
                                    taskCol.Item().Text($"Zadanie {taskNumber}:")
                                        .SemiBold().FontSize(14);

                                    taskCol.Item().PaddingLeft(10)
                                        .Text(t.Content ?? "")
                                        .FontSize(12);

                                    // Odpowiedzi
                                    taskCol.Item().PaddingTop(5).Text("Odpowiedzi:")
                                        .SemiBold();

                                    if (t.Answers != null)
                                    {
                                        foreach (var answer in t.Answers)
                                        {
                                            taskCol.Item().PaddingLeft(10)
                                                .Text(answer)
                                                .FontSize(11);
                                        }
                                    }

                                    // Rozwiązanie (opcjonalnie ukryte)
                                    taskCol.Item().PaddingTop(10).Column(solutionCol =>
                                    {
                                        solutionCol.Item().Text("Poprawna odpowiedź:")
                                            .SemiBold().FontColor(Colors.Green.Darken2);

                                        solutionCol.Item().PaddingLeft(10)
                                            .Text(t.CorrectAnswer ?? "")
                                            .FontSize(11).FontColor(Colors.Green.Darken2);

                                        solutionCol.Item().PaddingTop(5)
                                            .Text("Rozwiązanie:")
                                            .SemiBold();

                                        solutionCol.Item().PaddingLeft(10)
                                            .Text(t.Solution ?? "")
                                            .FontSize(10);
                                    });

                                    // Źródło
                                    if (!string.IsNullOrEmpty(t.Source))
                                    {
                                        taskCol.Item().PaddingTop(5)
                                            .Text($"Źródło: {t.Source}")
                                            .Italic().FontSize(9);
                                    }

                                    taskCol.Item().PaddingTop(10)
                                        .LineHorizontal(0.5f)
                                        .LineColor(Colors.Grey.Lighten2);
                                });

                                taskNumber++;
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Strona ");
                            x.CurrentPageNumber();
                            x.Span(" z ");
                            x.TotalPages();
                        });
                });
            });

            return pdf.GeneratePdf();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error generating PDF");
            throw;
        }
    }

    // Wersja BEZ rozwiązań (dla quizu)
    public byte[] GeneratePdfWithoutSolutions(GeneratedTask task)
    {
        try
        {
            var taskData = JsonSerializer.Deserialize<TaskGenerationResponse>(
                task.GeneratedText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (taskData?.Tasks == null || taskData.Tasks.Count == 0)
            {
                throw new InvalidOperationException("No tasks found in JSON");
            }

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(12).FontFamily("Arial"));

                    page.Header()
                        .Text("Test z Fizyki")
                        .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Spacing(10);

                            column.Item().Text($"Temat: {task.Prompt}").FontSize(10).Italic();
                            column.Item().LineHorizontal(1);

                            int taskNumber = 1;
                            foreach (var t in taskData.Tasks)
                            {
                                column.Item().PaddingTop(10).Column(taskCol =>
                                {
                                    taskCol.Item().Text($"Zadanie {taskNumber}:")
                                        .SemiBold().FontSize(14);

                                    taskCol.Item().PaddingLeft(10)
                                        .Text(t.Content ?? "")
                                        .FontSize(12);

                                    taskCol.Item().PaddingTop(5).Text("Odpowiedzi:")
                                        .SemiBold();

                                    if (t.Answers != null)
                                    {
                                        foreach (var answer in t.Answers)
                                        {
                                            taskCol.Item().PaddingLeft(10)
                                                .Text(answer)
                                                .FontSize(11);
                                        }
                                    }

                                    // Miejsce na odpowiedź
                                    taskCol.Item().PaddingTop(10)
                                        .Text("Twoja odpowiedź: ___________")
                                        .FontSize(11);

                                    taskCol.Item().PaddingTop(10)
                                        .LineHorizontal(0.5f)
                                        .LineColor(Colors.Grey.Lighten2);
                                });

                                taskNumber++;
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text("Powodzenia!");
                });
            });

            return pdf.GeneratePdf();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error generating PDF");
            throw;
        }
    }
}