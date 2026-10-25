using Api.Contracts;
using Api.Models;
using Api.Repositories.Interfaces;
using Api.Services.Interfaces;

namespace Api.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly ICompanyServiceClient _companyClient;

    public UserService(IUserRepository repository, ICompanyServiceClient companyClient)
    {
        _repository = repository;
        _companyClient = companyClient;
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

    public async Task<bool> IsInSameCompanyAsync(Guid requesterId, Guid targetId, CancellationToken cancellationToken = default)
    {
        var requesterCompany = await _companyClient.GetByUserAsync(requesterId, cancellationToken);
        if (requesterCompany.Error is not null || requesterCompany.Content is null) return false; // без компании никого не видно

        var targetCompany = await _companyClient.GetByUserAsync(targetId, cancellationToken);
        return !(targetCompany.Error is not null || targetCompany.Content is null)
            && targetCompany.Content!.Id == requesterCompany.Content.Id;
    }

    public async Task<List<UserResponse>> GetCompanyMembersAsync(Guid requesterId, CancellationToken cancellationToken = default)
    {
        var company = await _companyClient.GetByUserAsync(requesterId, cancellationToken);
        if (company.Error is not null || company.Content is null) return []; // без компании или company-service недоступен — пустой список

        // null — компания исчезла между двумя вызовами; трактуем как пусто
        var memberIds = await _companyClient.GetMemberIdsAsync(company.Content.Id, cancellationToken);
        if (memberIds is null || memberIds.Count == 0) return [];

        var members = await _repository.GetAllAsync(cancellationToken);
        return members.Where(u => memberIds.Contains(u.Id)).ToList();
    }
}
