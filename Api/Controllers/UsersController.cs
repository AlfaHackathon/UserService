using AlfaHackaton.Controllers.Controllers;
using Api.Contracts;
using Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Ручки для работы с пользователями и ролями (роль — свойство пользователя,
/// создание/удаление ролей и смены ролей — только создатель компании).
/// Result-pattern: ошибки из сервиса мапит OkOrProblem.
/// Префикс user-service добавляется глобально через RoutePrefixConvention.
/// </summary>
public class UsersController : BaseAuthController
{
    private readonly IUserService _service;
    private readonly IRoleService _roles;

    public UsersController(IUserService service, IRoleService roles)
    {
        _service = service;
        _roles = roles;
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

    /// <summary>
    /// Роли, видимые моим глазам: все дефолтные + кастомные моей компании.
    /// Без компании — только дефолтные.
    /// </summary>
    [HttpGet("users/me/roles")]
    public async Task<IActionResult> GetMyRoles(CancellationToken cancellationToken = default)
        => OkOrProblem(await _roles.GetForUserAsync(UserId, cancellationToken));

    /// <summary>Создать кастомную роль в своей компании. Только создатель компании. 409 — имя занято.</summary>
    [HttpPost("users/me/roles")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request, CancellationToken cancellationToken = default)
        => OkOrProblem(await _roles.CreateAsync(UserId, request.Name, cancellationToken));

    /// <summary>Удалить кастомную роль своей компании. Только создатель компании. 409 — роль занята.</summary>
    [HttpDelete("users/me/roles/{roleId:guid}")]
    public async Task<IActionResult> DeleteRole(Guid roleId, CancellationToken cancellationToken = default)
        => OkOrProblem<object>(await _roles.DeleteAsync(UserId, roleId, cancellationToken));

    /// <summary>
    /// Назначить/сменить/снять роль пользователю своей компании: {"roleId": "..."} или {"roleId": null}.
    /// Только создатель компании. После регистрации у пользователя роль null.
    /// </summary>
    [HttpPatch("users/{userId:guid}/role")]
    public async Task<IActionResult> SetUserRole(Guid userId, [FromBody] UpdateUserRoleRequest request, CancellationToken cancellationToken = default)
        => OkOrProblem<object>(await _roles.SetUserRoleAsync(UserId, userId, request.RoleId, cancellationToken));
}
