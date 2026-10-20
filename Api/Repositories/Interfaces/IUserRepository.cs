using Api.Contracts;
using Api.Models;

namespace Api.Repositories.Interfaces;

public interface IUserRepository
{
    public Task<List<UserResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    public Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    public Task<User> AddAsync(string lastName, string firstName, string? middleName, string email,
        string hashedPassword, CancellationToken cancellationToken = default);
}
