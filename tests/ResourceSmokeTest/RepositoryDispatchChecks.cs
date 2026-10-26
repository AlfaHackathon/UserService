using System.Reflection;
using AlfaHackaton.DapperContext.Common.Interfaces.Dapper;
using Api.Contracts;
using Api.Repositories;
using Api.Resources;
using Api.Services;

internal static class RepositoryDispatchChecks
{
    public static async Task Run()
    {
        var request = new CaptureSourceAccountRequest(Guid.NewGuid(), Guid.NewGuid(), "source-dispatch", new(null, null),
            new("synthetic-dispatch", "v1", new string('a', 64)));
        var user = Guid.NewGuid();
        var source = SourceAccountProfiles.Capture(user, request, DateTimeOffset.Parse("2026-10-27T12:00:00Z"));
        var success = new SourceRepositoryDatabase(user, "source-success");
        Check(await new SourceAccountRepository(success.Context()).TryCaptureAsync(source, SourceAccountProfiles.RequestHash(request)),
            "A source-only account must accept the always-row empty auth-lineage scalar and commit.");
        Check(success.Events.SequenceEqual(new[] { "begin", "lock", "auth-lineage", "can-bind", "last-time", "identity", "source-insert", "commit", "dispose" }),
            "Source writes must take the common lock before auth-row locking and commit only after insertion.");
        var conflict = new SourceRepositoryDatabase(user, "source-conflict");
        Check(!await new SourceAccountRepository(conflict.Context()).TryCaptureAsync(source, SourceAccountProfiles.RequestHash(request)),
            "An always-row empty source insert scalar must report conflict without QueryFirst failure.");
        Check(!conflict.Events.Contains("commit") && conflict.Events.Last() == "dispose", "A failed source insert must roll back, including the identity insert.");

        var viewerRequest = new CaptureViewerContextRequest("viewer-dispatch", request.SourcePersonId, new(null, null), request.Provenance);
        var viewer = ViewerContextSnapshots.Capture(user, request.CompanyId, viewerRequest, null, source.KnownAt);
        foreach (var mode in new[] { "viewer-deleted", "viewer-conflict" })
        {
            var database = new SourceRepositoryDatabase(user, mode);
            Check(!await new ViewerContextRepository(database.Context()).TryCaptureAsync(viewer, ViewerContextSnapshots.RequestHash(viewerRequest), request.SourcePersonId),
                "Deleted auth row and duplicate viewer insertion must return false from their empty scalar sentinels.");
            Check(!database.Events.Contains("commit") && !database.Events.Contains("update-user") && database.Events.Last() == "dispose",
                "Viewer conflict/deletion must not commit or update auth context.");
            Check(database.Events.Take(3).SequenceEqual(new[] { "begin", "lock", "auth-user" }), "Both repositories must use the same lock order.");
        }
        Console.WriteLine("Repository DispatchProxy checks passed with QueryFirst-style one-row scalar outcomes (SQL cardinality is checked separately with actual resources).");
    }

    private static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}

internal sealed class SourceRepositoryDatabase(Guid userId, string mode)
{
    public List<string> Events { get; } = [];
    public IDapperContext Context()
    {
        var result = DispatchProxy.Create<IDapperContext, SourceRepositoryProxy>();
        ((SourceRepositoryProxy)(object)result).Database = this;
        return result;
    }
    public object? Invoke(MethodInfo method, object?[] args, bool transaction)
    {
        if (!transaction)
        {
            if (method.Name != "BeginTransaction") throw new InvalidOperationException("Unexpected context call.");
            Events.Add("begin");
            var result = DispatchProxy.Create<ITransaction, SourceRepositoryProxy>();
            var proxy = (SourceRepositoryProxy)(object)result;
            proxy.Database = this;
            proxy.Transaction = true;
            return result;
        }
        if (method.Name is "Commit" or "Dispose") { Events.Add(method.Name.ToLowerInvariant()); return null; }
        var query = (IQueryObject)args[0]!;
        object? response = null;
        if (query.Sql == SourceAccountSql.Lock) Events.Add("lock");
        else if (query.Sql == SourceAccountSql.LockAuthLineage) { Events.Add("auth-lineage"); response = FirstScalar(new[] { "" }); }
        else if (query.Sql == SourceAccountSql.CanBind) { Events.Add("can-bind"); response = FirstScalar(new[] { true }); }
        else if (query.Sql == SourceAccountSql.LastTime || query.Sql == ViewerContextSql.LastTime) { Events.Add("last-time"); response = FirstScalar(new[] { 0L }); }
        else if (query.Sql == SourceAccountSql.InsertIdentity) Events.Add("identity");
        else if (query.Sql == SourceAccountSql.InsertRevision) { Events.Add("source-insert"); response = FirstScalar(new[] { mode == "source-conflict" ? "" : "source-dispatch" }); }
        else if (query.Sql == ViewerContextSql.LockUser) { Events.Add("auth-user"); response = FirstScalar(new[] { mode == "viewer-deleted" ? Guid.Empty : userId }); }
        else if (query.Sql == SourceAccountSql.ViewerCompatible) { Events.Add("viewer-compatible"); response = FirstScalar(new[] { true }); }
        else if (query.Sql == ViewerContextSql.Insert) { Events.Add("viewer-insert"); response = FirstScalar(new[] { "" }); }
        else if (query.Sql == ViewerContextSql.UpdateUser) Events.Add("update-user");
        else throw new InvalidOperationException("Unexpected repository query.");
        if (method.Name == "CommandWithResponse" && response is null) throw new InvalidOperationException("QueryFirstAsync has no scalar row.");
        if (method.ReturnType == typeof(Task)) return Task.CompletedTask;
        var resultType = method.ReturnType.GetGenericArguments()[0];
        if (response is null && resultType.IsValueType) response = Activator.CreateInstance(resultType);
        return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null, [response]);
    }
    private static T FirstScalar<T>(IEnumerable<T> rows) => rows.First();
}

public class SourceRepositoryProxy : DispatchProxy
{
    internal SourceRepositoryDatabase Database { get; set; } = null!;
    internal bool Transaction { get; set; }
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Database.Invoke(method!, args ?? [], Transaction);
}
