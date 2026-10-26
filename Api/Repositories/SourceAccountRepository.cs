using System.Text.Json;
using AlfaHackaton.DapperContext.Common.Interfaces.Dapper;
using AlfaHackaton.DapperContext.Dapper;
using Api.Contracts;
using Api.Models;
using Api.Repositories.Interfaces;
using Api.Resources;
using Api.Services;

namespace Api.Repositories;

public sealed class SourceAccountRepository(IDapperContext database) : ISourceAccountRepository
{
    public async Task<UserSourceAccountIdentity?> IdentityAsync(Guid userId) =>
        Identity(await database.FirstOrDefault<IdentityRow>(new QueryObject(SourceAccountSql.Identity, new { userId })));

    public async Task<UserSourceAccountIdentity?> PairAsync(Guid sourcePersonId, Guid companyId) =>
        Identity(await database.FirstOrDefault<IdentityRow>(new QueryObject(SourceAccountSql.Pair, new { sourcePersonId, companyId })));

    public async Task<SourceAccountRevision?> RevisionAsync(Guid userId, string revisionId) =>
        Decode(await database.FirstOrDefault<Row>(new QueryObject(SourceAccountSql.Revision, new { userId, revisionId })));

    public async Task<SourceAccountRevision?> LatestAsync(Guid userId, DateTimeOffset asOf) =>
        Decode(await database.FirstOrDefault<Row>(new QueryObject(SourceAccountSql.Latest, new { userId, asOfTicks = asOf.UtcTicks })));

    public async Task<SourceAccountRevision?> LatestByPairAsync(Guid sourcePersonId, Guid companyId, DateTimeOffset asOf) =>
        Decode(await database.FirstOrDefault<Row>(new QueryObject(SourceAccountSql.LatestByPair, new { sourcePersonId, companyId, asOfTicks = asOf.UtcTicks })));

    public async Task<bool> TryCaptureAsync(UserSourceAccountProfile profile, string requestHash)
    {
        using var transaction = database.BeginTransaction();
        // All source-account and authenticated viewer writes take this lock before any auth row lock.
        await transaction.Command(new QueryObject(SourceAccountSql.Lock));
        var parameters = new { userId = profile.UserId, sourcePersonId = profile.SourcePersonId, companyId = profile.CompanyId };
        var lineage = await transaction.CommandWithResponse<string>(new QueryObject(SourceAccountSql.LockAuthLineage, parameters));
        if (!string.IsNullOrEmpty(lineage) && (!Guid.TryParse(lineage, out var existingSource) || existingSource != profile.SourcePersonId)) return false;
        if (!await transaction.CommandWithResponse<bool>(new QueryObject(SourceAccountSql.CanBind, parameters))) return false;
        var lastTime = await transaction.CommandWithResponse<long>(new QueryObject(SourceAccountSql.LastTime, parameters));
        if (lastTime >= profile.KnownAt.UtcTicks) return false;
        await transaction.Command(new QueryObject(SourceAccountSql.InsertIdentity, parameters));

        var inserted = await transaction.CommandWithResponse<string>(new QueryObject(SourceAccountSql.InsertRevision, new
        {
            userId = profile.UserId,
            revisionId = profile.RevisionId,
            knownAtTicks = profile.KnownAt.UtcTicks,
            snapshotJson = JsonSerializer.Serialize(profile, ViewerContextSnapshots.Json),
            requestHash
        }));
        if (string.IsNullOrEmpty(inserted)) return false;
        transaction.Commit();
        return true;
    }

    private static SourceAccountRevision? Decode(Row? row) => row is null ? null : new(
        JsonSerializer.Deserialize<UserSourceAccountProfile>(row.SnapshotJson, ViewerContextSnapshots.Json)
            ?? throw new JsonException("Invalid source account snapshot."), row.RequestHash);

    private static UserSourceAccountIdentity? Identity(IdentityRow? row) => row is null ? null : new(row.UserId, row.SourcePersonId, row.CompanyId);

    private sealed class IdentityRow
    {
        public Guid UserId { get; set; }
        public Guid SourcePersonId { get; set; }
        public Guid CompanyId { get; set; }
    }

    private sealed class Row
    {
        public string SnapshotJson { get; set; } = string.Empty;
        public string RequestHash { get; set; } = string.Empty;
    }
}
