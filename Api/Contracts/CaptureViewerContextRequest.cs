using System.Text.Json.Serialization;

namespace Api.Contracts;

/// <summary>Updates nullable declared context for an existing account and captures its assigned role.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CaptureViewerContextRequest(
    string RevisionId,
    [property: JsonRequired] Guid? SourcePersonId,
    ServiceGeography ServiceGeography,
    ViewerContextProvenance Provenance);
