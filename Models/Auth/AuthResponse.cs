namespace ExamCreateApp.Models.Auth;

/// <summary>
/// DTO for authentication response (login/register)
/// </summary>
public class AuthResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public DateTime? TokenExpiry { get; set; }
    public UserDto? User { get; set; }
    public string? Message { get; set; }
}