namespace Api.Contracts;

/// <summary>Canonical source part of the logical User account. No login credentials, role or grant is inferred.</summary>
public sealed record UserSourceAccountProfile(
    Guid UserId, Guid SourcePersonId, Guid CompanyId, string RevisionId,
    DateTimeOffset KnownAt, string ContentHash, string SourceOwner,
    ServiceGeography ServiceGeography, ViewerContextProvenance Provenance);
