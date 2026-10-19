using AlfaHackaton.DapperContext.Common.Interfaces.Dapper;
using AlfaHackaton.DapperContext.Dapper;
using UserService.Models;

namespace UserService.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IDapperContext _context;

    public UserRepository(IDapperContext context)
    {
        _context = context;
    }

    public async Task<List<UserResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id, last_name, first_name, middle_name, email, created_at
                           FROM users
                           """;

        return await _context.ListOrEmpty<UserResponse>(new QueryObject(sql));
    }

    public async Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id, last_name, first_name, middle_name, email, created_at
                           FROM users
                           WHERE id = @id
                           """;

        return await _context.FirstOrDefault<UserResponse>(new QueryObject(sql, new { id }));
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id, last_name, first_name, middle_name, email, hashed_password, created_at
                           FROM users
                           WHERE email = @email
                           """;

        return await _context.FirstOrDefault<User>(new QueryObject(sql, new { email = email.Trim().ToLower() }));
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT EXISTS(SELECT 1 FROM users WHERE email = @email)
                           """;

        return await _context.FirstOrDefault<bool>(new QueryObject(sql, new { email = email.Trim().ToLower() }));
    }

    public async Task<User> AddAsync(string lastName, string firstName, string? middleName, string email,
        string hashedPassword, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO users (id, last_name, first_name, middle_name, email, hashed_password)
                           VALUES (@id, @last_name, @first_name, @middle_name, @email, @hashed_password)
                           RETURNING id, last_name, first_name, middle_name, email, hashed_password, created_at
                           """;

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

        return await _context.CommandWithResponse<User>(new QueryObject(sql, new
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
