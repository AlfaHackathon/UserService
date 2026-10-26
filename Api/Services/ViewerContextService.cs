using System.Net;
using AlfaHackaton.Result;
using AlfaHackaton.Result.Errors;
using Api.Contracts;
using Api.Repositories.Interfaces;
using Api.Services.Interfaces;

namespace Api.Services;

/// <summary>Reads assigned role context without modifying membership, roles or permissions.</summary>
public sealed class ViewerContextService(
    IUserRepository users,
    IRoleRepository roles,
    ICompanyServiceClient companies)
{
    public async Task<Result<ViewerContextResponse>> GetAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || companyId == Guid.Empty)
            return new(Fail("Explicit user and company identifiers are required.", HttpStatusCode.BadRequest));

        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return new(Fail("Viewer is unavailable.", HttpStatusCode.NotFound));
        if (user.Id != userId)
            return new(Unavailable());

        var membership = await companies.GetByUserAsync(userId, cancellationToken);
        if (membership.Error is not null)
            return new(Unavailable());
        if (membership.Content is null || membership.Content.Id != companyId)
            return new(Fail("Current membership in the requested company is required.", HttpStatusCode.Forbidden));

        ViewerRoleResponse? viewerRole = null;
        if (user.RoleId is { } roleId)
        {
            var role = await roles.GetByIdInCompanyAsync(roleId, companyId, cancellationToken);
            if (role is null || role.Id != roleId ||
                (role.IsDefault ? role.CompanyId is not null : role.CompanyId != companyId) ||
                string.IsNullOrWhiteSpace(role.Name) || role.Name.Length > 128)
                return new(Unavailable());

            viewerRole = new ViewerRoleResponse(role.Id, role.Name, role.IsDefault);
        }

        // This is an observation time, not the time the role was assigned.
        return new(new ViewerContextResponse(userId, companyId, viewerRole, DateTimeOffset.UtcNow));
    }

    private static Error Fail(string message, HttpStatusCode status) =>
        new(message, ErrorSource.Client, status switch
        {
            HttpStatusCode.NotFound => ErrorType.NotFound,
            HttpStatusCode.BadRequest => ErrorType.CorruptedInfo,
            _ => ErrorType.NoRights
        })
        { HttpStatusCode = status };

    private static Error Unavailable() =>
        new("Viewer context could not be verified.", ErrorSource.External, ErrorType.Undefined)
        { HttpStatusCode = HttpStatusCode.BadGateway };
}
