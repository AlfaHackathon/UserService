using Api.Contracts;
using Api.Models;
using Api.Repositories.Interfaces;
using Api.Services.Interfaces;

namespace Api.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;

    public UserService(IUserRepository repository)
    {
        _repository = repository;
    }

    public Task<List<UserResponse>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => _repository.GetByEmailAsync(email, cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
        => _repository.ExistsByEmailAsync(email, cancellationToken);

    public Task<User> AddAsync(string lastName, string firstName, string? middleName, string email,
        string hashedPassword, CancellationToken cancellationToken = default)
        => _repository.AddAsync(lastName, firstName, middleName, email, hashedPassword, cancellationToken);
}
