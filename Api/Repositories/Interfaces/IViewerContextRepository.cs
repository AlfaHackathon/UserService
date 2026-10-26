using Api.Contracts;
using Api.Models;

namespace Api.Repositories.Interfaces;

public interface IViewerContextRepository
{
    Task<ViewerContextRevision?> FindAsync(Guid userId, Guid companyId, string revisionId);
    Task<ViewerContextRevision?> LatestAsync(Guid userId, Guid companyId, DateTimeOffset asOf);
    Task<bool> TryCaptureAsync(ViewerContextResponse context, string requestHash, Guid? sourcePersonId);
}
