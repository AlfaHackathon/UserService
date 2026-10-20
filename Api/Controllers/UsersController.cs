using AlfaHackaton.Controllers.Controllers;
using Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Ручки для работы с пользователями.
/// Префикс user-service добавляется глобально через RoutePrefixConvention.
/// </summary>
public class UsersController : BaseAuthController
{
    private readonly IUserService _service;

    public UsersController(IUserService service)
    {
        _service = service;
    }

    /// <summary>
    /// Получить всех пользователей.
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var users = await _service.GetAllAsync(cancellationToken);
        return Ok(users);
    }

    /// <summary>
    /// Получить пользователя по id.
    /// </summary>
    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _service.GetByIdAsync(id, cancellationToken);
        return user is null
            ? NotFound(new { error = $"Пользователь с id {id} не найден" })
            : Ok(user);
    }
}
