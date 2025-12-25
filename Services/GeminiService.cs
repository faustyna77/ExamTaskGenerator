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
        var prompt = BuildPrompt(request, context);

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
                maxOutputTokens = 2048,  // ✅ Zwiększone dla dłuższych odpowiedzi
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            // ✅ POPRAWKA 1: URL BEZ klucza API
            var url = $"{GeminiApiBaseUrl}/models/{ModelName}:generateContent";

            _logger.LogInformation($"🔵 Calling Gemini API: {ModelName}");

            // ✅ POPRAWKA 2: Dodaj nagłówek x-goog-api-key
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)  // ← ZMIENIONE z "request" na "httpRequest"
            {
                Content = content
            };
            httpRequest.Headers.Add("x-goog-api-key", _apiKey);

            var response = await _httpClient.SendAsync(httpRequest);  // ← ZMIENIONE

           

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

    private string BuildPrompt(TaskGenerationRequest request, string context)
    {
        var taskCount = Math.Min(request.TaskCount, 3);

        // ✅ Wyciągnij rok z kontekstu
        var yearMatch = Regex.Match(context, @"\b(20\d{2})\b");
        var exampleYear = yearMatch.Success ? yearMatch.Value : "2024";
        var prompt = $@"Wygeneruj {taskCount} zadanie/zadania z fizyki jako JSON.

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
6. Jesli w kontekscie jest inny rok niz {exampleYear}, uzyj tego roku!
7. Zamknij wszystkie nawiasy }}]}}
8. Zwroc TYLKO JSON bez zadnych dodatkowych tekstow, bez ``` i bez preamble

Wygeneruj teraz JSON:";

        return prompt;
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

            // ✅ Sprawdź czy content ma property "parts"
            if (!firstCandidate.GetProperty("content").TryGetProperty("parts", out var parts))
            {
                _logger.LogWarning("⚠️ No 'parts' in content (thinking mode?)");
                return "{}";
            }

            var text = parts[0].GetProperty("text").GetString();

            _logger.LogInformation($"📤 Generated text: {text?.Substring(0, Math.Min(200, text?.Length ?? 0))}...");

            return text ?? "{}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error parsing Gemini response");
            throw new InvalidOperationException("Failed to parse Gemini API response", ex);
        }
    }
}