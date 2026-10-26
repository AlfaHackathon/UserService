using Api.Contracts;
using Api.Models;

namespace Api.Repositories.Interfaces;

public interface ISourceAccountRepository
{
    Task<UserSourceAccountIdentity?> IdentityAsync(Guid userId);
    Task<UserSourceAccountIdentity?> PairAsync(Guid sourcePersonId, Guid companyId);
    Task<SourceAccountRevision?> RevisionAsync(Guid userId, string revisionId);
    Task<SourceAccountRevision?> LatestAsync(Guid userId, DateTimeOffset asOf);
    Task<SourceAccountRevision?> LatestByPairAsync(Guid sourcePersonId, Guid companyId, DateTimeOffset asOf);
    Task<bool> TryCaptureAsync(UserSourceAccountProfile profile, string requestHash);
}
