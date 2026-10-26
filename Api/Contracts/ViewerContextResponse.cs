namespace Api.Contracts;

/// <summary>Current framing context for a trusted internal caller; never a read grant.</summary>
public sealed record ViewerContextResponse(
    Guid UserId,
    Guid CompanyId,
    ViewerRoleResponse? Role,
    DateTimeOffset ObservedAt);

public sealed record ViewerRoleResponse(Guid Id, string Name, bool IsDefault);
