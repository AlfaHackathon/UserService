using AlfaHackaton.DapperContext.Common.Interfaces.Dapper;
using AlfaHackaton.DapperContext.Dapper;
using Api.Contracts;
using Api.Models;
using Api.Repositories.Interfaces;
using Api.Resources;

namespace Api.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IDapperContext _context;

    public UserRepository(IDapperContext context)
    {
        _context = context;
    }

    public async Task<List<UserResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ListOrEmpty<UserResponse>(new QueryObject(UserSql.GetAll));
    }

    public async Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.FirstOrDefault<UserResponse>(new QueryObject(UserSql.GetById, new { id }));
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.FirstOrDefault<User>(new QueryObject(UserSql.GetByEmail, new { email = email.Trim().ToLower() }));
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.FirstOrDefault<bool>(new QueryObject(UserSql.ExistsByEmail, new { email = email.Trim().ToLower() }));
    }

    public async Task<User> AddAsync(string lastName, string firstName, string? middleName, string email,
        string hashedPassword, CancellationToken cancellationToken = default)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            LastName = lastName,
            FirstName = firstName,
            MiddleName = middleName,
            Email = email.Trim().ToLower(),
            HashedPassword = hashedPassword,
            CreatedAt = DateTime.UtcNow
        };

        return await _context.CommandWithResponse<User>(new QueryObject(UserSql.Add, new
        {
            id = user.Id,
            last_name = user.LastName,
            first_name = user.FirstName,
            middle_name = user.MiddleName,
            email = user.Email,
            hashed_password = user.HashedPassword
        }));
    }
}
