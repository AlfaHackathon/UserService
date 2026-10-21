using Api.Contracts;
using Api.Models;

namespace Api.Services.Interfaces;

/// <summary>
/// Бизнес-операции над пользователями.
/// Контроллеры работают только через сервис, напрямую в репозиторий — нельзя.
/// </summary>
public interface IUserService
{
    Task<List<UserResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User> AddAsync(string lastName, string firstName, string? middleName, string email,
        string hashedPassword, CancellationToken cancellationToken = default);

    /// <summary>Состоят ли оба пользователя в одной компании.</summary>
    Task<bool> IsInSameCompanyAsync(Guid requesterId, Guid targetId, CancellationToken cancellationToken = default);

    /// <summary>Пользователи компании, в которой состоит requester. Пусто — если он без компании.</summary>
    Task<List<UserResponse>> GetCompanyMembersAsync(Guid requesterId, CancellationToken cancellationToken = default);
}
