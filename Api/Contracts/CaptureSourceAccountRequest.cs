using System.Text.Json.Serialization;

namespace Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CaptureSourceAccountRequest(
    Guid SourcePersonId, Guid CompanyId, string RevisionId,
    ServiceGeography ServiceGeography, ViewerContextProvenance Provenance);
