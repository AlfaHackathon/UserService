using Api.Contracts;

namespace Api.Models;

public sealed record ViewerContextRevision(ViewerContextResponse Context, string RequestHash);
