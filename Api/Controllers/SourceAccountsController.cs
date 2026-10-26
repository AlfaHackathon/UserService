using AlfaHackaton.Controllers.Controllers;
using Api.Contracts;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Api.Controllers;

[ApiController]
[Route("internal/source-accounts")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status502BadGateway)]
public sealed class SourceAccountsController(SourceAccountService accounts, IConfiguration configuration) : BaseController
{
    /// <summary>Read an immutable source-only account context; never creates login access or a role.</summary>
    [HttpGet("{userId:guid}/context")]
    [ProducesResponseType<UserSourceAccountProfile>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(Guid userId, [FromQuery, BindRequired] DateTimeOffset asOf)
    {
        if (!InternalRequest()) return Unauthorized();
        return OkOrProblem(await accounts.GetAsync(userId, asOf));
    }

    /// <summary>Resolve an exact UUID F/U pair through its latest source snapshot known at asOf.</summary>
    [HttpGet("by-pair")]
    [ProducesResponseType<UserSourceAccountProfile>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ByPairAsync([FromQuery, BindRequired] Guid sourcePersonId,
        [FromQuery, BindRequired] Guid companyId, [FromQuery, BindRequired] DateTimeOffset asOf)
    {
        if (!InternalRequest()) return Unauthorized();
        return OkOrProblem(await accounts.ByPairAsync(sourcePersonId, companyId, asOf));
    }

    /// <summary>Capture the four source fields without provisioning auth, membership, roles or grants.</summary>
    [HttpPost("{userId:guid}/context")]
    [RequestSizeLimit(64 * 1024)]
    [ProducesResponseType<UserSourceAccountProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> CaptureAsync(Guid userId, [FromBody] CaptureSourceAccountRequest request, CancellationToken cancellationToken = default)
    {
        if (!InternalRequest()) return Unauthorized();
        return OkOrProblem(await accounts.CaptureAsync(userId, request, cancellationToken));
    }

    private bool InternalRequest()
    {
        var expected = configuration["Internal:Secret"];
        return !string.IsNullOrEmpty(expected) && expected == Request.Headers["X-Internal-Secret"].ToString();
    }
}
