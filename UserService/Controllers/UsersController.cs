using AlfaHackaton.Controllers.Controllers;
using Microsoft.AspNetCore.Mvc;
using UserService.Repositories;

namespace UserService.Controllers;

/// <summary>
/// Ручки для работы с пользователями.
/// Префикс user-service добавляется глобально через RoutePrefixConvention.
/// </summary>
public class UsersController : BaseAuthController
{
    private readonly IUserRepository _repository;

    public UsersController(IUserRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Получить всех пользователей.
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var users = await _repository.GetAllAsync(cancellationToken);
        return Ok(users);
    }

    /// <summary>
    /// Получить пользователя по id.
    /// </summary>
    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetByIdAsync(id, cancellationToken);
        return user is null
            ? NotFound(new { error = $"Пользователь с id {id} не найден" })
            : Ok(user);
    }
}
