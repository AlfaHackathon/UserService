using Microsoft.AspNetCore.Mvc;
using Npgsql;
using UserService.Models;
using UserService.Repositories;

namespace UserService.Controllers;

/// <summary>
/// Внутренние ручки для сервисов кластера (auth-service).
/// Защищены общим секретом X-Internal-Secret, наружу отдаётся хэш пароля.
/// Префикс user-service добавляется глобально через RoutePrefixConvention.
/// </summary>
[ApiController]
[Route("internal/users")]
public class UsersInternalController : ControllerBase
{
    private const string InternalSecretHeader = "X-Internal-Secret";

    private readonly IUserRepository _repository;
    private readonly IConfiguration _configuration;

    public UsersInternalController(IUserRepository repository, IConfiguration configuration)
    {
        _repository = repository;
        _configuration = configuration;
    }

    /// <summary>
    /// Получить пользователя по email (включая хэш пароля — только для auth-service).
    /// </summary>
    [HttpGet("by-email/{email}")]
    public async Task<IActionResult> GetByEmail(string email, CancellationToken cancellationToken = default)
    {
        if (!IsInternalRequest())
        {
            return Unauthorized(new { error = "Недоступно извне кластера" });
        }

        var user = await _repository.GetByEmailAsync(email, cancellationToken);
        return user is null
            ? NotFound(new { error = $"Пользователь с email {email} не найден" })
            : Ok(user);
    }

    /// <summary>
    /// Проверить, зарегистрирован ли email.
    /// </summary>
    [HttpGet("exists-by-email/{email}")]
    public async Task<IActionResult> ExistsByEmail(string email, CancellationToken cancellationToken = default)
    {
        if (!IsInternalRequest())
        {
            return Unauthorized(new { error = "Недоступно извне кластера" });
        }

        var exists = await _repository.ExistsByEmailAsync(email, cancellationToken);
        return Ok(new { exists });
    }

    /// <summary>
    /// Создать пользователя (email и хэш пароля приходит из auth-service).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsInternalRequest())
        {
            return Unauthorized(new { error = "Недоступно извне кластера" });
        }

        try
        {
            var user = await _repository.AddAsync(
                request.LastName, request.FirstName, request.MiddleName, request.Email,
                request.HashedPassword, cancellationToken);
            return Ok(user);
        }
        catch (PostgresException exception) when (exception.SqlState == "23505")
        {
            // Уникальный индекс по email — гонка двух register'ов
            return Conflict(new { error = $"Пользователь с email {request.Email} уже существует" });
        }
    }

    private bool IsInternalRequest()
    {
        var expected = _configuration["Internal:Secret"];
        var provided = Request.Headers[InternalSecretHeader].ToString();
        return !string.IsNullOrEmpty(expected) && expected == provided;
    }
}

/// <summary>
/// Данные для создания пользователя.
/// </summary>
public record CreateUserRequest(string LastName, string FirstName, string? MiddleName, string Email, string HashedPassword);
