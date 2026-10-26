using Api.Contracts;

namespace Api.Models;

public sealed record UserSourceAccountIdentity(Guid UserId, Guid SourcePersonId, Guid CompanyId);
public sealed record SourceAccountRevision(UserSourceAccountProfile Profile, string RequestHash);
