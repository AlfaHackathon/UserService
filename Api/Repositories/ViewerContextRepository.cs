using System.Text.Json;
using AlfaHackaton.DapperContext.Common.Interfaces.Dapper;
using AlfaHackaton.DapperContext.Dapper;
using Api.Contracts;
using Api.Models;
using Api.Repositories.Interfaces;
using Api.Resources;
using Api.Services;

namespace Api.Repositories;

public sealed class ViewerContextRepository(IDapperContext database) : IViewerContextRepository
{
    public async Task<ViewerContextRevision?> FindAsync(Guid userId, Guid companyId, string revisionId) =>
        Decode(await database.FirstOrDefault<StoredRow>(new QueryObject(ViewerContextSql.Find, new { userId, companyId, revisionId })));

    public async Task<ViewerContextRevision?> LatestAsync(Guid userId, Guid companyId, DateTimeOffset asOf) =>
        Decode(await database.FirstOrDefault<StoredRow>(new QueryObject(ViewerContextSql.Latest,
            new { userId, companyId, asOfTicks = asOf.UtcTicks })));

    public async Task<bool> TryCaptureAsync(ViewerContextResponse context, string requestHash, Guid? sourcePersonId)
    {
        using var transaction = database.BeginTransaction();
        await transaction.Command(new QueryObject(SourceAccountSql.Lock));
        var user = await transaction.CommandWithResponse<Guid>(new QueryObject(ViewerContextSql.LockUser, new { userId = context.UserId }));
        if (user != context.UserId) return false;
        if (!await transaction.CommandWithResponse<bool>(new QueryObject(SourceAccountSql.ViewerCompatible,
            new { userId = context.UserId, companyId = context.CompanyId, sourcePersonId }))) return false;
        var previousTime = await transaction.CommandWithResponse<long>(new QueryObject(ViewerContextSql.LastTime, new { userId = context.UserId }));
        if (previousTime >= context.KnownAt.UtcTicks) return false;

        var inserted = await transaction.CommandWithResponse<string>(new QueryObject(ViewerContextSql.Insert, new
        {
            userId = context.UserId,
            companyId = context.CompanyId,
            revisionId = context.RevisionId,
            knownAtTicks = context.KnownAt.UtcTicks,
            snapshotJson = JsonSerializer.Serialize(context, ViewerContextSnapshots.Json),
            requestHash
        }));
        if (string.IsNullOrEmpty(inserted)) return false;

        await transaction.Command(new QueryObject(ViewerContextSql.UpdateUser, new
        {
            userId = context.UserId,
            sourcePersonId,
            cityName = context.ServiceGeography.CityName,
            rcName = context.ServiceGeography.RcName
        }));
        transaction.Commit();
        return true;
    }

    private static ViewerContextRevision? Decode(StoredRow? row) => row is null ? null : new(
        JsonSerializer.Deserialize<ViewerContextResponse>(row.SnapshotJson, ViewerContextSnapshots.Json)
            ?? throw new JsonException("Invalid viewer snapshot."), row.RequestHash);

    private sealed class StoredRow
    {
        public string SnapshotJson { get; set; } = string.Empty;
        public string RequestHash { get; set; } = string.Empty;
    }
}
