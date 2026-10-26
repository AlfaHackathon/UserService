using System.Net;
using AlfaHackaton.Result;
using AlfaHackaton.Result.Errors;
using Api.Contracts;
using Api.Models;
using Api.Repositories.Interfaces;
using Api.Services.Interfaces;

namespace Api.Services;

public sealed class ViewerContextService(
    IUserRepository users,
    IRoleRepository roles,
    ICompanyServiceClient companies,
    IViewerContextRepository snapshots,
    TimeProvider clock)
{
    public async Task<Result<ViewerContextResponse>> GetAsync(
        Guid userId, Guid companyId, DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        if (asOf == default) return new(Fail("An explicit asOf is required.", HttpStatusCode.BadRequest));
        var scope = await ScopeAsync(userId, companyId, cancellationToken);
        if (scope.Error is { } error) return new(error);

        var revision = await snapshots.LatestAsync(userId, companyId, asOf);
        if (revision is null) return new(Fail("No viewer snapshot is known at asOf.", HttpStatusCode.NotFound));
        return ViewerContextSnapshots.ValidSnapshot(revision.Context, userId, companyId, asOf)
            ? new(revision.Context)
            : new(Unavailable());
    }

    public async Task<Result<ViewerContextResponse>> CaptureAsync(
        Guid userId, Guid companyId, CaptureViewerContextRequest request, CancellationToken cancellationToken = default)
    {
        if (!ViewerContextSnapshots.ValidCapture(request))
            return new(Fail("A bounded revision, nullable source lineage and geography, and exact source provenance are required.", HttpStatusCode.BadRequest));
        var scope = await ScopeAsync(userId, companyId, cancellationToken);
        if (scope.Error is { } error) return new(error);
        var requestHash = ViewerContextSnapshots.RequestHash(request);
        var existing = await snapshots.FindAsync(userId, companyId, request.RevisionId);
        if (existing is not null) return Replay(existing, requestHash, userId, companyId, request.RevisionId);

        ViewerRoleResponse? viewerRole = null;
        if (scope.User!.RoleId is { } roleId)
        {
            var role = await roles.GetByIdInCompanyAsync(roleId, companyId, cancellationToken);
            if (role is null || role.Id != roleId || role.Id == Guid.Empty ||
                (role.IsDefault ? role.CompanyId is not null : role.CompanyId != companyId) ||
                string.IsNullOrWhiteSpace(role.Name) || role.Name.Length > 128 || role.Name.Trim() != role.Name || role.Name.Any(char.IsControl))
                return new(Unavailable());
            viewerRole = new(role.Id, role.Name, role.IsDefault);
        }

        var snapshot = ViewerContextSnapshots.Capture(userId, companyId, request, viewerRole, clock.GetUtcNow());
        if (await snapshots.TryCaptureAsync(snapshot, requestHash, request.SourcePersonId)) return new(snapshot);

        // A concurrent identical capture can win; never update canonical fields on a replay or a conflict.
        existing = await snapshots.FindAsync(userId, companyId, request.RevisionId);
        return existing is not null
            ? Replay(existing, requestHash, userId, companyId, request.RevisionId)
            : new(Fail("Viewer capture conflicted or the account is no longer available.", HttpStatusCode.Conflict));
    }

    private Result<ViewerContextResponse> Replay(ViewerContextRevision revision, string requestHash, Guid userId, Guid companyId, string revisionId)
    {
        if (revision.Context.RevisionId != revisionId) return new(Unavailable());
        if (revision.RequestHash != requestHash)
            return new(Fail("Viewer revisions are immutable; use a new revision for changed context.", HttpStatusCode.Conflict));
        return ViewerContextSnapshots.ValidSnapshot(revision.Context, userId, companyId, clock.GetUtcNow())
            ? new(revision.Context)
            : new(Unavailable());
    }

    private async Task<(UserResponse? User, Error? Error)> ScopeAsync(Guid userId, Guid companyId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || companyId == Guid.Empty)
            return (null, Fail("Explicit existing account and company identifiers are required.", HttpStatusCode.BadRequest));
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return (null, Fail("Viewer is unavailable.", HttpStatusCode.NotFound));
        if (user.Id != userId) return (null, Unavailable());
        var membership = await companies.GetByUserAsync(userId, cancellationToken);
        if (membership.Error is not null) return (null, Unavailable());
        if (membership.Content?.Id != companyId)
            return (null, Fail("Current membership in the requested company is required.", HttpStatusCode.Forbidden));
        return (user, null);
    }

    private static Error Fail(string message, HttpStatusCode status) => new(message, ErrorSource.Client, status switch
    {
        HttpStatusCode.NotFound => ErrorType.NotFound,
        HttpStatusCode.BadRequest => ErrorType.CorruptedInfo,
        HttpStatusCode.Conflict => ErrorType.CorruptedInfo,
        _ => ErrorType.NoRights
    })
    { HttpStatusCode = status };

    private static Error Unavailable() => new("Viewer context could not be verified.", ErrorSource.External, ErrorType.Undefined)
    { HttpStatusCode = HttpStatusCode.BadGateway };
}
