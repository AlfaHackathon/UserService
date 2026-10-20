namespace Api.Contracts;

/// <summary>
/// Данные для создания пользователя.
/// </summary>
public record CreateUserRequest(string LastName, string FirstName, string? MiddleName, string Email, string HashedPassword);
