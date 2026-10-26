using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.Contracts;
using Api.Models;

namespace Api.Services;

public static class SourceAccountProfiles
{
    public static bool ValidCapture(Guid userId, CaptureSourceAccountRequest? request) => userId != Guid.Empty && request is not null &&
        request.SourcePersonId != Guid.Empty && request.CompanyId != Guid.Empty && ViewerContextSnapshots.ValidIdentifier(request.RevisionId) &&
        ViewerContextSnapshots.ValidGeography(request.ServiceGeography) && ViewerContextSnapshots.ValidProvenance(request.Provenance);

    public static bool CanBind(Guid userId, CaptureSourceAccountRequest request, UserSourceAccountIdentity? account, UserSourceAccountIdentity? pair) =>
        (account is null || account == new UserSourceAccountIdentity(userId, request.SourcePersonId, request.CompanyId)) &&
        (pair is null || pair == new UserSourceAccountIdentity(userId, request.SourcePersonId, request.CompanyId));

    public static bool ViewerCompatible(UserSourceAccountIdentity? identity, Guid userId, Guid companyId, Guid? sourcePersonId) =>
        identity is null || identity.UserId == userId && identity.CompanyId == companyId && identity.SourcePersonId == sourcePersonId;

    public static string RequestHash(CaptureSourceAccountRequest request) => Hash(request);

    public static UserSourceAccountProfile Capture(Guid userId, CaptureSourceAccountRequest request, DateTimeOffset knownAt)
    {
        var profile = new UserSourceAccountProfile(userId, request.SourcePersonId, request.CompanyId, request.RevisionId,
            knownAt.ToUniversalTime(), "", ViewerContextSnapshots.SourceOwner, request.ServiceGeography, request.Provenance);
        return profile with { ContentHash = ContentHash(profile) };
    }

    public static string ContentHash(UserSourceAccountProfile profile) => Hash(new
    {
        profile.UserId,
        profile.SourcePersonId,
        profile.CompanyId,
        profile.RevisionId,
        profile.KnownAt,
        profile.SourceOwner,
        profile.ServiceGeography,
        profile.Provenance
    });

    public static bool ValidProfile(UserSourceAccountProfile profile, Guid userId, Guid sourcePersonId, Guid companyId, DateTimeOffset asOf) =>
        profile.UserId == userId && userId != Guid.Empty && profile.SourcePersonId == sourcePersonId && sourcePersonId != Guid.Empty &&
        profile.CompanyId == companyId && companyId != Guid.Empty && profile.KnownAt != default && profile.KnownAt <= asOf &&
        profile.SourceOwner == ViewerContextSnapshots.SourceOwner && ViewerContextSnapshots.ValidIdentifier(profile.RevisionId) &&
        ViewerContextSnapshots.ValidGeography(profile.ServiceGeography) && ViewerContextSnapshots.ValidProvenance(profile.Provenance) &&
        profile.ContentHash == ContentHash(profile);

    private static string Hash<T>(T value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, ViewerContextSnapshots.Json))));
}
