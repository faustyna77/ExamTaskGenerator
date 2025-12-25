namespace ExamCreateApp.Models
{
    public class SubmitAnswerRequest
    {
        public int GeneratedTaskId { get; set; }
        public string UserAnswer { get; set; } = string.Empty; // A, B, C, D
    }
}
