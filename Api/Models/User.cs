namespace Api.Models;

/// <summary>
/// Пользователь системы (полная сущность, включая хэш пароля).
/// </summary>
public class User
{
    public required Guid Id { get; set; }
    public required string LastName { get; set; }
    public required string FirstName { get; set; }
    public string? MiddleName { get; set; }
    public required string Email { get; set; }
    public required string HashedPassword { get; set; }
    public required DateTime CreatedAt { get; set; }
    // Nullable canonical context. SourcePersonId is source lineage, not an account alias or an access grant.
    public Guid? SourcePersonId { get; set; }
    public string? CityName { get; set; }
    public string? RcName { get; set; }
}
