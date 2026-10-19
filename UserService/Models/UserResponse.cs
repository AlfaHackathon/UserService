namespace UserService.Models;

/// <summary>
/// Пользователь без чувствительных данных (для выдачи наружу).
/// </summary>
public class UserResponse
{
    public required Guid Id { get; set; }
    public required string LastName { get; set; }
    public required string FirstName { get; set; }
    public string? MiddleName { get; set; }
    public required string Email { get; set; }
    public required DateTime CreatedAt { get; set; }
}
