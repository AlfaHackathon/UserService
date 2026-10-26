using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Contracts;

namespace Api.Services;

/// <summary>Defines the immutable snapshot and its exact hash without persistence or authorization side effects.</summary>
public static class ViewerContextSnapshots
{
    public const string SourceOwner = "user-service";
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    static ViewerContextSnapshots() => Json.MakeReadOnly(populateMissingResolver: true);

    public static bool ValidIdentifier(string? value) =>
        value is { Length: > 0 and <= 200 } && value.Trim() == value && !value.Any(char.IsControl);

    public static bool ValidGeography(ServiceGeography? geography) => geography is not null &&
        ValidRawGeography(geography.CityName) && ValidRawGeography(geography.RcName);

    public static bool ValidProvenance(ViewerContextProvenance? provenance) => provenance is not null &&
        ValidIdentifier(provenance.SourceId) && ValidIdentifier(provenance.VersionId) &&
        provenance.ContentHash is not null && Regex.IsMatch(provenance.ContentHash, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant);

    public static bool ValidCapture(CaptureViewerContextRequest? request) => request is not null &&
        ValidIdentifier(request.RevisionId) && request.SourcePersonId != Guid.Empty &&
        ValidGeography(request.ServiceGeography) && ValidProvenance(request.Provenance);

    public static string RequestHash(CaptureViewerContextRequest request) => Hash(request);

    public static ViewerContextResponse Capture(
        Guid userId, Guid companyId, CaptureViewerContextRequest request, ViewerRoleResponse? role, DateTimeOffset knownAt)
    {
        var snapshot = new ViewerContextResponse(userId, companyId, request.RevisionId, knownAt.ToUniversalTime(), "",
            SourceOwner, role, request.ServiceGeography, request.Provenance);
        return snapshot with { ContentHash = ContentHash(snapshot) };
    }

    public static string ContentHash(ViewerContextResponse context) => Hash(new
    {
        context.UserId,
        context.CompanyId,
        context.RevisionId,
        context.KnownAt,
        context.SourceOwner,
        context.Role,
        context.ServiceGeography,
        context.Provenance
    });

    public static bool ValidSnapshot(ViewerContextResponse context, Guid userId, Guid companyId, DateTimeOffset asOf) =>
        context.UserId == userId && context.CompanyId == companyId && context.KnownAt != default && context.KnownAt <= asOf &&
        context.SourceOwner == SourceOwner && ValidIdentifier(context.RevisionId) && ValidGeography(context.ServiceGeography) &&
        ValidProvenance(context.Provenance) &&
        (context.Role is null || context.Role.Id != Guid.Empty && ValidRoleName(context.Role.Name)) &&
        context.ContentHash == ContentHash(context);

    // A request resource bound and PostgreSQL text boundary, not a source-field normalization rule.
    private static bool ValidRawGeography(string? value) => value is null ||
        value.Length <= 16000 && !value.Contains('\0');

    private static bool ValidRoleName(string? value) =>
        value is { Length: > 0 and <= 128 } && value.Trim() == value && !value.Any(char.IsControl);

    private static string Hash<T>(T value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
}
