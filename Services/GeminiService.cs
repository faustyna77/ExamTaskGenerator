using ExamCreateApp.Models;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ExamCreateApp.Services;

public class GeminiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiService> _logger;

    private const string GeminiApiBaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    private const string ModelName = "gemini-2.5-flash";

    public GeminiService(
        IConfiguration configuration,
        HttpClient httpClient,
        ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Missing Gemini API Key");
        _logger = logger;
    }

    public async Task<string> GenerateExamTasks(TaskGenerationRequest request, string context)
    {
        // ✅ ZMIANA: Wybierz prompt na podstawie typu zadania
        var prompt = request.TaskType == "open"
            ? BuildOpenTaskPrompt(request, context)
            : BuildClosedTaskPrompt(request, context);

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.7,
                topK = 40,
                topP = 0.95,
                maxOutputTokens = 4096,
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var url = $"{GeminiApiBaseUrl}/models/{ModelName}:generateContent";

            _logger.LogInformation($"🔵 Calling Gemini API: {ModelName} (TaskType: {request.TaskType})");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            httpRequest.Headers.Add("x-goog-api-key", _apiKey);

            var response = await _httpClient.SendAsync(httpRequest);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"❌ Gemini API error ({response.StatusCode}): {errorContent}");
                throw new HttpRequestException($"Gemini API returned {response.StatusCode}: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("✅ Gemini API response received");
            _logger.LogInformation($"📦 Response: {responseContent.Substring(0, Math.Min(500, responseContent.Length))}...");

            return ParseResponse(responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Gemini API");
            throw;
        }
    }

    // ✅ ZADANIA ZAMKNIĘTE (A/B/C/D)
    private string BuildClosedTaskPrompt(TaskGenerationRequest request, string context)
    {
        var taskCount = Math.Min(request.TaskCount, 3);
        var yearMatch = Regex.Match(context, @"\b(20\d{2})\b");
        var exampleYear = yearMatch.Success ? yearMatch.Value : "2024";

        return $@"Wygeneruj {taskCount} zadanie/zadania z fizyki jako JSON.

PARAMETRY:
Poziom: {request.DifficultyLevel}
Dzial: {request.PhysicsSubject ?? "mechanika"}
Temat: {request.TaskTopic ?? "podstawy fizyki"}

KONTEKST - Przyklady z prawdziwych egzaminow:
{context}

PRZYKLAD JSON (DOKLADNIE TAKA STRUKTURA):
{{
  ""tasks"": [
    {{
      ""content"": ""Samochod przejezdza 120 km w czasie 2 godzin. Oblicz predkosc srednia pojazdu."",
      ""answers"": [""A) 30 km/h"", ""B) 60 km/h"", ""C) 90 km/h"", ""D) 120 km/h""],
      ""correctAnswer"": ""B"",
      ""solution"": ""Predkosc srednia v = droga/czas = 120 km / 2 h = 60 km/h. Odpowiedz: B"",
      ""source"": ""Matura {exampleYear} CKE""
    }}
  ]
}}

KLUCZOWE ZASADY:
1. Wygeneruj DOKLADNIE {taskCount} zadanie/zadania
2. Bez polskich znakow (a,c,e,l,n,o,s,z zamiast ą,ć,ę,ł,ń,ó,ś,ź)
3. Kazde zadanie w osobnym obiekcie
4. 4 odpowiedzi A,B,C,D
5. W polu ""source"" OBOWIAZKOWO uzyj formatu: ""Matura {exampleYear} CKE""
6. Zamknij wszystkie nawiasy }}]}}
7. Zwroc TYLKO JSON bez zadnych dodatkowych tekstow, bez ``` i bez preamble

Wygeneruj teraz JSON:";
    }

    // ✅ NOWE - ZADANIA OTWARTE (obliczeniowe)
    private string BuildOpenTaskPrompt(TaskGenerationRequest request, string context)
    {
        var taskCount = Math.Min(request.TaskCount, 3);
        var yearMatch = Regex.Match(context, @"\b(20\d{2})\b");
        var exampleYear = yearMatch.Success ? yearMatch.Value : "2024";

        return $@"Wygeneruj {taskCount} OTWARTE zadanie/zadania z fizyki jako JSON.

PARAMETRY:
Poziom: {request.DifficultyLevel}
Dzial: {request.PhysicsSubject ?? "mechanika"}
Temat: {request.TaskTopic ?? "podstawy fizyki"}

KONTEKST - Przyklady z prawdziwych egzaminow:
{context}

PRZYKLAD JSON (zadanie OTWARTE - obliczeniowe):
{{
  ""tasks"": [
    {{
      ""content"": ""Samochod porusza sie ruchem jednostajnym prostoliniowym z predkoscia 72 km/h. Oblicz droge przebyta przez samochod w ciagu 15 minut. Wynik podaj w metrach i przedstaw obliczenia."",
      ""answers"": null,
      ""correctAnswer"": ""18000 m"",
      ""solution"": ""Dane: v = 72 km/h = 20 m/s, t = 15 min = 900 s. Wzor: s = v * t. Obliczenia: s = 20 m/s * 900 s = 18000 m. Odpowiedz: 18000 m"",
      ""source"": ""Matura {exampleYear} CKE"",
      ""pointsAvailable"": 2
    }}
  ]
}}

KLUCZOWE ZASADY:
1. Zadanie OTWARTE - wymaga obliczen i podania wyniku liczbowego
2. ""answers"" ZAWSZE ustawione na null (nie ma opcji A/B/C/D)
3. ""correctAnswer"" to poprawny wynik liczbowy z jednostka (np. ""18000 m"", ""5 m/s"", ""2.5 s"")
4. ""solution"" zawiera:
   - Dane: (co jest dane w zadaniu)
   - Wzor: (jaki wzor uzywamy)
   - Obliczenia: (krok po kroku)
   - Odpowiedz: (wynik koncowy)
5. ""pointsAvailable"" - ile punktow warte jest zadanie (1-3, w zaleznosci od trudnosci)
6. Bez polskich znakow (a,c,e,l,n,o,s,z zamiast ą,ć,ę,ł,ń,ó,ś,ź)
7. Zadania powinny wymagac obliczen numerycznych (nie pytania teoretyczne)
8. W polu ""source"" uzyj formatu: ""Matura {exampleYear} CKE""
9. Zamknij wszystkie nawiasy }}]}}
10. Zwroc TYLKO JSON bez zadnych dodatkowych tekstow, bez ``` i bez preamble

Wygeneruj teraz JSON:";
    }

    private string ParseResponse(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);

            var candidates = doc.RootElement.GetProperty("candidates");

            if (candidates.GetArrayLength() == 0)
            {
                _logger.LogWarning("⚠️ No candidates in response");
                return "{}";
            }

            var firstCandidate = candidates[0];

            if (!firstCandidate.GetProperty("content").TryGetProperty("parts", out var parts))
            {
                _logger.LogWarning("⚠️ No 'parts' in content (thinking mode?)");
                return "{}";
            }

            var text = parts[0].GetProperty("text").GetString();

            // ✅ DODAJ: Sprawdź czy JSON jest kompletny
            if (!string.IsNullOrEmpty(text))
            {
                text = text.Trim();

                // Usuń markdown backticks jeśli są
                if (text.StartsWith("```json"))
                {
                    text = text.Replace("```json", "").Replace("```", "").Trim();
                }

                // ✅ Sprawdź czy JSON się zamyka
                if (!text.EndsWith("}"))
                {
                    _logger.LogWarning($"⚠️ JSON incomplete - doesn't end with }}. Length: {text.Length}");
                    _logger.LogWarning($"Last 100 chars: {text.Substring(Math.Max(0, text.Length - 100))}");

                    // Spróbuj naprawić - dodaj brakujące nawiasy
                    var openBraces = text.Count(c => c == '{');
                    var closeBraces = text.Count(c => c == '}');
                    var missing = openBraces - closeBraces;

                    if (missing > 0)
                    {
                        text += new string('}', missing);
                        _logger.LogInformation($"✅ Auto-fixed JSON by adding {missing} closing braces");
                    }
                }
            }

            _logger.LogInformation($"📤 Generated text length: {text?.Length ?? 0} chars");

            return text ?? "{}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error parsing Gemini response");
            throw new InvalidOperationException("Failed to parse Gemini API response", ex);
        }
    }
}