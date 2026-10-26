using AlfaHackaton.Controllers.Controllers;
using Api.Contracts;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Api.Controllers;

[ApiController]
[Route("internal/users")]
public sealed class ViewerContextController(ViewerContextService viewers, IConfiguration configuration) : BaseController
{
    /// <summary>Read an immutable role/service-geography snapshot for a trusted account and company at asOf.</summary>
    [HttpGet("{userId:guid}/viewer-context")]
    [ProducesResponseType<ViewerContextResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetAsync(Guid userId, [FromQuery, BindRequired] Guid companyId,
        [FromQuery, BindRequired] DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        if (!InternalRequest()) return Unauthorized(new { error = "Недоступно извне кластера" });
        return OkOrProblem(await viewers.GetAsync(userId, companyId, asOf, cancellationToken));
    }

    /// <summary>Explicitly capture declared nullable context and the existing account role; never creates an account or a grant.</summary>
    [HttpPost("{userId:guid}/viewer-context")]
    [ProducesResponseType<ViewerContextResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> CaptureAsync(Guid userId, [FromQuery, BindRequired] Guid companyId,
        [FromBody] CaptureViewerContextRequest request, CancellationToken cancellationToken = default)
    {
        if (!InternalRequest()) return Unauthorized(new { error = "Недоступно извне кластера" });
        return OkOrProblem(await viewers.CaptureAsync(userId, companyId, request, cancellationToken));
    }

    private bool InternalRequest()
    {
        var expected = configuration["Internal:Secret"];
        return !string.IsNullOrEmpty(expected) && expected == Request.Headers["X-Internal-Secret"].ToString();
    }
}
