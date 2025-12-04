using System.Text;
using System.Text.Json;
using ExamCreateApp.Models;

namespace ExamCreateApp.Services;

public class GeminiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiService> _logger;
    private const string GeminiApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";

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
                maxOutputTokens = 2048,
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(
                $"{GeminiApiUrl}?key={_apiKey}",
                content
            );

            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();

            return ParseResponse(responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Gemini API");
            throw;
        }
    }

    private string BuildPrompt(TaskGenerationRequest request, string context)
    {
        var prompt = $@"
You are an expert at creating Polish high school physics exam questions (matura).

CONTEXT - Example tasks from previous years:
{context}

TASK:
Generate {request.TaskCount} physics question(s) at {request.DifficultyLevel} level.
{(string.IsNullOrEmpty(request.PhysicsSubject) ? "" : $"Subject: {request.PhysicsSubject}")}
{(string.IsNullOrEmpty(request.TaskTopic) ? "" : $"Topic: {request.TaskTopic}")}

REQUIREMENTS:
1. Questions should be stylistically similar to the examples
2. Maintain the difficulty level and format of matura questions
3. Add 4 answers (A, B, C, D) if it's multiple choice
4. Provide correct answer and brief solution
5. Use Polish physics terminology

RESPONSE FORMAT (JSON):
{{
  ""tasks"": [
    {{
      ""content"": ""Question text..."",
      ""answers"": [""A) ..., ""B) ..., ""C) ..., ""D) ...""],
      ""correctAnswer"": ""A"",
      ""solution"": ""Step by step..."",
      ""source"": ""Inspired by 2023 exam""
    }}
  ]
}}

Generate the questions NOW in Polish:";

        return prompt;
    }

    private string ParseResponse(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var candidates = doc.RootElement.GetProperty("candidates");
        var content = candidates[0].GetProperty("content");
        var parts = content.GetProperty("parts");
        var text = parts[0].GetProperty("text").GetString();

        return text ?? string.Empty;
    }
}