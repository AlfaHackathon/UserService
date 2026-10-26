using System.Text.Json.Serialization;

namespace Api.Contracts;

/// <summary>An immutable framing snapshot, not a permission grant or a company-location claim.</summary>
public sealed record ViewerContextResponse(
    Guid UserId,
    Guid CompanyId,
    string RevisionId,
    DateTimeOffset KnownAt,
    string ContentHash,
    string SourceOwner,
    ViewerRoleResponse? Role,
    ServiceGeography ServiceGeography,
    ViewerContextProvenance Provenance);

public sealed record ViewerRoleResponse(Guid Id, string Name, bool IsDefault);

/// <summary>CITY_NAME and RC_NAME describe service geography, not company registration or operations.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ServiceGeography(
    [property: JsonRequired] string? CityName,
    [property: JsonRequired] string? RcName);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ViewerContextProvenance(string SourceId, string VersionId, string ContentHash);
