using System.Net;
using AlfaHackaton.Result;
using AlfaHackaton.Result.Errors;
using Api.Contracts;
using Api.Models;
using Api.Repositories.Interfaces;
using Api.Services.Interfaces;

namespace Api.Services;

public sealed class SourceAccountService(ISourceAccountRepository accounts, IUserRepository users, ICompanyServiceClient companies, TimeProvider clock)
{
    public async Task<Result<UserSourceAccountProfile>> GetAsync(Guid userId, DateTimeOffset asOf)
    {
        if (userId == Guid.Empty || asOf == default) return new(Fail("Explicit account and asOf are required.", HttpStatusCode.BadRequest));
        var revision = await accounts.LatestAsync(userId, asOf);
        return Read(revision, userId, null, null, asOf);
    }

    public async Task<Result<UserSourceAccountProfile>> ByPairAsync(Guid sourcePersonId, Guid companyId, DateTimeOffset asOf)
    {
        if (sourcePersonId == Guid.Empty || companyId == Guid.Empty || asOf == default)
            return new(Fail("An exact UUID source pair and asOf are required.", HttpStatusCode.BadRequest));
        var revision = await accounts.LatestByPairAsync(sourcePersonId, companyId, asOf);
        return Read(revision, null, sourcePersonId, companyId, asOf);
    }

    public async Task<Result<UserSourceAccountProfile>> CaptureAsync(Guid userId, CaptureSourceAccountRequest request, CancellationToken cancellationToken = default)
    {
        if (!SourceAccountProfiles.ValidCapture(userId, request))
            return new(Fail("Exact UUID identities, revision, raw geography and source provenance are required.", HttpStatusCode.BadRequest));
        // Source-only accounts have no auth row and never call CompanyService or create membership.
        var auth = await users.GetByIdAsync(userId, cancellationToken);
        if (auth is not null)
        {
            if (auth.Id != userId) return new(Unavailable());
            var membership = await companies.GetByUserAsync(userId, cancellationToken);
            if (membership.Error is not null) return new(Unavailable());
            if (membership.Content?.Id != request.CompanyId)
                return new(Fail("The existing auth account must currently belong to the source company.", HttpStatusCode.Forbidden));
        }
        if (!SourceAccountProfiles.CanBind(userId, request, await accounts.IdentityAsync(userId), await accounts.PairAsync(request.SourcePersonId, request.CompanyId)))
            return new(Fail("The logical account and source pair are immutable and unique.", HttpStatusCode.Conflict));
        var hash = SourceAccountProfiles.RequestHash(request);
        var old = await accounts.RevisionAsync(userId, request.RevisionId);
        if (old is not null) return Replay(old, userId, request, hash);
        var profile = SourceAccountProfiles.Capture(userId, request, clock.GetUtcNow());
        if (await accounts.TryCaptureAsync(profile, hash)) return new(profile);
        old = await accounts.RevisionAsync(userId, request.RevisionId);
        return old is not null ? Replay(old, userId, request, hash)
            : new(Fail("Source capture conflicted with an immutable identity, lineage or knowledge time.", HttpStatusCode.Conflict));
    }

    private Result<UserSourceAccountProfile> Replay(SourceAccountRevision revision, Guid userId, CaptureSourceAccountRequest request, string hash)
    {
        if (revision.RequestHash != hash) return new(Fail("Source revisions are immutable; use a new revision for changed geography or provenance.", HttpStatusCode.Conflict));
        if (revision.Profile.RevisionId != request.RevisionId) return new(Unavailable());
        return Read(revision, userId, request.SourcePersonId, request.CompanyId, clock.GetUtcNow());
    }

    private static Result<UserSourceAccountProfile> Read(SourceAccountRevision? revision, Guid? userId, Guid? sourcePersonId, Guid? companyId, DateTimeOffset asOf)
    {
        if (revision is null) return new(Fail("No source account profile is known at asOf.", HttpStatusCode.NotFound));
        var p = revision.Profile;
        return SourceAccountProfiles.ValidProfile(p, userId ?? p.UserId, sourcePersonId ?? p.SourcePersonId, companyId ?? p.CompanyId, asOf)
            ? new(p) : new(Unavailable());
    }

    private static Error Fail(string message, HttpStatusCode status) => new(message, ErrorSource.Client,
        status == HttpStatusCode.NotFound ? ErrorType.NotFound : status == HttpStatusCode.Forbidden ? ErrorType.NoRights : ErrorType.CorruptedInfo)
    { HttpStatusCode = status };
    private static Error Unavailable() => new("Source account context could not be verified.", ErrorSource.External, ErrorType.Undefined)
    { HttpStatusCode = HttpStatusCode.BadGateway };
}
