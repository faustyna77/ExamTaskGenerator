using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using ExamCreateApp.Data;
using ExamCreateApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ExamCreateApp.Services;

public class PdfImportService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PdfImportService> _logger;

    public PdfImportService(AppDbContext dbContext, ILogger<PdfImportService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<int> ImportExamSheetsFromPdfs()
    {
        var pdfDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "ExamSheets");

        if (!Directory.Exists(pdfDirectory))
        {
            _logger.LogError($"❌ Directory not found: {pdfDirectory}");
            return 0;
        }

        var pdfFiles = Directory.GetFiles(pdfDirectory, "*.pdf");
        _logger.LogInformation($"📂 Found {pdfFiles.Length} PDF files");

        int totalImported = 0;

        foreach (var pdfPath in pdfFiles)
        {
            try
            {
                var fileName = Path.GetFileName(pdfPath);
                _logger.LogInformation($"📄 Processing: {fileName}");

                // Sprawdź czy już zaimportowano
                var existing = await _dbContext.ExamTasks
                    .Where(t => t.ExamSheetName == fileName)
                    .AnyAsync();

                if (existing)
                {
                    _logger.LogInformation($"⏭️  Already imported: {fileName}");
                    continue;
                }

                // Wyciągnij tekst z PDF
                var fullText = ExtractTextFromPdf(pdfPath);

                // Parsuj podstawowe info z nazwy pliku
                var examInfo = ParseFileNameInfo(fileName);

                // Podziel na zadania (prosty podział - można ulepszyć)
                var tasks = SplitIntoTasks(fullText, fileName, examInfo);

                // Zapisz do bazy
                await _dbContext.ExamTasks.AddRangeAsync(tasks);
                await _dbContext.SaveChangesAsync();

                totalImported += tasks.Count;
                _logger.LogInformation($"✅ Imported {tasks.Count} tasks from {fileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error processing {pdfPath}");
            }
        }

        _logger.LogInformation($"🎉 Total imported: {totalImported} tasks");
        return totalImported;
    }

    private string ExtractTextFromPdf(string pdfPath)
    {
        using var document = PdfDocument.Open(pdfPath);
        var text = string.Join("\n", document.GetPages().Select(p => p.Text));
        return text;
    }

    private (string level, string subject, int? year) ParseFileNameInfo(string fileName)
    {
        // fizyka-2024-grudzien-probna-rozszerzona.pdf
        var level = fileName.Contains("rozszerzona") ? "rozszerzony" : "podstawowy";
        var subject = "fizyka";

        var yearMatch = Regex.Match(fileName, @"(\d{4})");
        int? year = yearMatch.Success ? int.Parse(yearMatch.Value) : null;

        return (level, subject, year);
    }
    private List<ExamTask> SplitIntoTasks(string fullText, string fileName, (string level, string subject, int? year) examInfo)
    {
        var tasks = new List<ExamTask>();

        // Szukaj "Zadanie X" lub "Zad. X"
        var taskPattern = @"(?:Zadanie|Zad\.?)\s+(\d+)\.?\s*(.+?)(?=(?:Zadanie|Zad\.?)\s+\d+|--- PAGE BREAK ---|$)";
        var matches = Regex.Matches(fullText, taskPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        if (matches.Count == 0)
        {
            _logger.LogWarning($"⚠️ No task pattern found in {fileName}, creating single entry");

            tasks.Add(new ExamTask
            {
                Content = fullText.Substring(0, Math.Min(1000, fullText.Length)),
                ExamSheetName = fileName,
                Level = examInfo.level,
                Subject = examInfo.subject,
                Year = examInfo.year,
                TaskNumber = "1",  // ← string
                Page = 1
            });

            return tasks;
        }

        _logger.LogInformation($"📝 Found {matches.Count} tasks in {fileName}");

        foreach (Match match in matches)
        {
            var taskNumber = match.Groups[1].Value;  // string
            var content = match.Groups[2].Value.Trim();

            // Ogranicz długość
            if (content.Length > 800)
                content = content.Substring(0, 800) + "...";

            if (string.IsNullOrWhiteSpace(content))
                continue;

            tasks.Add(new ExamTask
            {
                Content = content,
                ExamSheetName = fileName,
                Level = examInfo.level,
                Subject = examInfo.subject,
                Year = examInfo.year,
                TaskNumber = taskNumber,  // string
                Page = int.TryParse(taskNumber, out int pageNum) ? pageNum : 1  // bezpieczna konwersja
            });
        }

        return tasks;
    }
}