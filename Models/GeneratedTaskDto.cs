namespace ExamCreateApp.Models;

// DTO dla listy wygenerowanych zadań (z bazy danych)
public class GeneratedTaskDto
{
    public int Id { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string GeneratedText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// Request do usuwania wielu zadań
public class DeleteMultipleRequest
{
    public List<int> Ids { get; set; } = new();
}