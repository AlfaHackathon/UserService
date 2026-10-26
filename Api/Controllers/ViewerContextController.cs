using AlfaHackaton.Controllers.Controllers;
using Api.Contracts;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Api.Controllers;

/// <summary>Internal current viewer framing, scoped by verified company membership.</summary>
[ApiController]
[Route("internal/users")]
public sealed class ViewerContextController(ViewerContextService viewers, IConfiguration configuration) : BaseController
{
    /// <summary>Read a trusted user's assigned role without granting permissions or returning identity details.</summary>
    [HttpGet("{userId:guid}/viewer-context")]
    [ProducesResponseType<ViewerContextResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetAsync(
        Guid userId,
        [FromQuery, BindRequired] Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var expected = configuration["Internal:Secret"];
        var provided = Request.Headers["X-Internal-Secret"].ToString();
        if (string.IsNullOrEmpty(expected) || expected != provided)
            return Unauthorized(new { error = "Недоступно извне кластера" });

        return OkOrProblem(await viewers.GetAsync(userId, companyId, cancellationToken));
    }
}
