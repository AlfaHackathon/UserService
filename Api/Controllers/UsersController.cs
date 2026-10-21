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

    /// <summary>Профиль текущего пользователя.</summary>
    [HttpGet("users/me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken = default)
    {
        var user = await _service.GetByIdAsync(UserId, cancellationToken);
        return user is null
            ? NotFound(new { error = "Пользователь не найден" })
            : Ok(user);
    }

    /// <summary>
    /// Пользователи компании, в которой состоит текущий пользователь.
    /// Без компании (ещё не вступил/не создал) — пустой список.
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetCompanyMembersAsync(UserId, cancellationToken));
    }

    /// <summary>
    /// Пользователь по id. Виден только членам одной с ним компании
    /// (403 «Пользователь не состоит в вашей компании» — иначе).
    /// </summary>
    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _service.GetByIdAsync(id, cancellationToken);
        if (user is null)
            return NotFound(new { error = $"Пользователь с id {id} не найден" });

        if (id != UserId && !await _service.IsInSameCompanyAsync(UserId, id, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Пользователь не состоит в вашей компании" });

        return Ok(user);
    }
}
