using System.Net;
using System.Text.Json;
using AlfaHackaton.Result;
using AlfaHackaton.Result.Errors;
using Api.Contracts;
using Api.Controllers;
using Api.Models;
using Api.Repositories.Interfaces;
using Api.Resources;
using Api.Services;
using Api.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

internal static class SourceAccountChecks
{
    private static int assertions;
    public static async Task Run()
    {
        string[] queries = [SourceAccountSql.Schema, SourceAccountSql.Drop, SourceAccountSql.Lock, SourceAccountSql.Identity,
            SourceAccountSql.Pair, SourceAccountSql.CanBind, SourceAccountSql.ViewerCompatible, SourceAccountSql.LockAuthLineage, SourceAccountSql.InsertIdentity,
            SourceAccountSql.Revision, SourceAccountSql.Latest, SourceAccountSql.LatestByPair, SourceAccountSql.LastTime, SourceAccountSql.InsertRevision];
        Check(queries.All(q => !string.IsNullOrWhiteSpace(q)), "Actual source-account SQL resources must resolve.");
        var f = new Fixture();
        await Status(f.Service.GetAsync(f.UserId, f.Clock.Now), HttpStatusCode.NotFound);
        Check(f.Store.Writes == 0, "GET must not create a canonical identity or revision.");
        var first = await f.Capture();
        Check(first.Error is null && first.Content!.UserId == f.UserId && f.Companies.Reads == 0,
            "Canonical-only capture must work without auth rows or CompanyService access.");
        var json = JsonSerializer.SerializeToElement(first.Content, ViewerContextSnapshots.Json);
        Check(!json.TryGetProperty("role", out _) && !json.TryGetProperty("email", out _) && !json.TryGetProperty("hashedPassword", out _),
            "Source account profile must not invent or expose auth data or roles.");
        await Status(f.Service.ByPairAsync(f.Request.SourcePersonId, f.Request.CompanyId, f.Clock.Now.AddTicks(-1)), HttpStatusCode.NotFound);
        Check((await f.Service.ByPairAsync(f.Request.SourcePersonId, f.Request.CompanyId, f.Clock.Now)).Content == first.Content,
            "Exact pair mapping must obey historical asOf and preserve its logical account.");
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        Check((await f.Capture()).Content == first.Content && f.Store.Writes == 1, "Identical source replay must return its original snapshot without writing.");
        await Status(f.Service.CaptureAsync(f.UserId, f.Request with { ServiceGeography = new("changed", null) }), HttpStatusCode.Conflict);
        await Status(f.Service.CaptureAsync(f.UserId, f.Request with { SourcePersonId = Guid.NewGuid() }), HttpStatusCode.Conflict);
        await Status(f.Service.CaptureAsync(f.UserId, f.Request with { CompanyId = Guid.NewGuid() }), HttpStatusCode.Conflict);
        await Status(f.Service.CaptureAsync(Guid.NewGuid(), f.Request), HttpStatusCode.Conflict);
        Check(f.Store.Identities.Count == 1 && f.Store.Revisions.Count == 1, "Neither immutable identity nor pair uniqueness can be rewritten.");
        f.Request = f.Request with { RevisionId = "source-2", ServiceGeography = new(" \tМосква\u00a0 ", "") };
        var second = await f.Capture();
        Check(second.Error is null && second.Content!.ServiceGeography == f.Request.ServiceGeography, "A later revision keeps raw geography exact.");
        Check((await f.Service.GetAsync(f.UserId, first.Content!.KnownAt)).Content == first.Content, "Later geography must not replace historical state.");
        Check((await f.Service.ByPairAsync(f.Request.SourcePersonId, f.Request.CompanyId, f.Clock.Now)).Content == second.Content, "Pair read returns the latest known immutable revision.");

        foreach (var race in new[] { "account", "pair", "same-revision" })
        {
            f = new Fixture();
            var winnerUser = race == "pair" ? Guid.NewGuid() : f.UserId;
            var winnerRequest = race == "account" ? f.Request with { SourcePersonId = Guid.NewGuid() } : f.Request;
            var winner = SourceAccountProfiles.Capture(winnerUser, winnerRequest, f.Clock.Now.AddTicks(-1));
            f.Store.BeforeWrite = () =>
            {
                f.Store.Identities.Add(new(winner.UserId, winner.SourcePersonId, winner.CompanyId));
                if (race == "same-revision") f.Store.Revisions.Add(new(winner, SourceAccountProfiles.RequestHash(f.Request)));
            };
            var result = await f.Capture();
            if (race == "same-revision") Check(result.Error is null && result.Content == winner, "A concurrent identical revision is safely replayed.");
            else Check(result.Error?.HttpStatusCode == HttpStatusCode.Conflict && f.Store.Identities.Single().UserId == winnerUser,
                "An identity/pair race must preserve the winner and report conflict.");
        }

        f = new Fixture();
        f.Users.Value = new UserResponse { Id = f.UserId, FirstName = "private", LastName = "private", Email = "private@example.invalid", CreatedAt = DateTime.UtcNow, RoleId = Guid.NewGuid() };
        f.Companies.Company = new() { Id = f.Request.CompanyId };
        Check((await f.Capture()).Error is null && f.Companies.Reads == 1, "An auth-backed UUID requires current matching company membership.");
        f = new Fixture();
        f.Users.Value = new UserResponse { Id = f.UserId, FirstName = "private", LastName = "private", Email = "private@example.invalid", CreatedAt = DateTime.UtcNow };
        await Status(f.Capture(), HttpStatusCode.Forbidden);
        Check(f.Store.Writes == 0, "Auth-backed source capture cannot create a membership.");
        f.Companies.Failure = new("upstream", ErrorSource.External, ErrorType.Undefined) { HttpStatusCode = HttpStatusCode.ServiceUnavailable };
        await Status(f.Capture(), HttpStatusCode.BadGateway);
        f = new Fixture();
        f.Store.AuthLineage = Guid.NewGuid();
        await Status(f.Capture(), HttpStatusCode.Conflict);
        Check(f.Store.Identities.Count == 0, "Reverse auth-lineage conflict must roll back identity creation.");

        f = new Fixture();
        await Status(f.Service.CaptureAsync(f.UserId, f.Request with { SourcePersonId = Guid.Empty }), HttpStatusCode.BadRequest);
        await Status(f.Service.ByPairAsync(f.Request.SourcePersonId, f.Request.CompanyId, default), HttpStatusCode.BadRequest);
        Check(f.Store.Writes == 0, "Invalid identity or historical scope cannot persist state.");
        var controller = new SourceAccountsController(f.Service, new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Internal:Secret"] = "fixture-secret" }).Build())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        Check(await controller.CaptureAsync(f.UserId, f.Request) is UnauthorizedResult &&
            await controller.GetAsync(f.UserId, f.Clock.Now) is UnauthorizedResult &&
            await controller.ByPairAsync(f.Request.SourcePersonId, f.Request.CompanyId, f.Clock.Now) is UnauthorizedResult,
            "Every source owner route requires the configured internal secret.");
        Check(f.Store.Writes == 0 && f.Users.Reads == 0, "Unauthenticated calls must not access account data.");
        controller.Request.Headers["X-Internal-Secret"] = "fixture-secret";
        Check(await controller.CaptureAsync(f.UserId, f.Request) is OkObjectResult, "An authorized explicit capture uses the real source service.");
        Console.WriteLine($"{assertions} source-account service/controller fixture assertions passed without a database or model.");
    }
    private static async Task Status(Task<Result<UserSourceAccountProfile>> task, HttpStatusCode status)
    {
        var result = await task;
        Check(result.Error?.HttpStatusCode == status && result.Content is null, $"Expected {(int)status} without a source profile.");
    }
    private static void Check(bool valid, string message) { assertions++; if (!valid) throw new InvalidOperationException(message); }
    private sealed class Fixture
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public CaptureSourceAccountRequest Request { get; set; } = new(Guid.NewGuid(), Guid.NewGuid(), "source-1", new(null, null), new("synthetic-original", "v1", new string('a', 64)));
        public FakeSourceAccounts Store { get; } = new();
        public Users Users { get; } = new();
        public Companies Companies { get; } = new();
        public Clock Clock { get; } = new();
        public SourceAccountService Service { get; }
        public Fixture() => Service = new(Store, Users, Companies, Clock);
        public Task<Result<UserSourceAccountProfile>> Capture() => Service.CaptureAsync(UserId, Request);
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-10-27T12:00:00Z"); public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Users : IUserRepository
    {
        public UserResponse? Value { get; set; }
        public int Reads { get; private set; }
        public Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) { Reads++; return Task.FromResult(Value); }
        public Task<List<UserResponse>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User> AddAsync(string lastName, string firstName, string? middleName, string email, string hashedPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Companies : ICompanyServiceClient
    {
        public CompanyInfoResponse? Company { get; set; }
        public Error? Failure { get; set; }
        public int Reads { get; private set; }
        public Task<Result<CompanyInfoResponse?>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        { Reads++; return Task.FromResult(Failure is { } e ? new Result<CompanyInfoResponse?>(e) : new Result<CompanyInfoResponse?>(Company)); }
        public Task<List<Guid>?> GetMemberIdsAsync(Guid companyId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

internal sealed class FakeSourceAccounts : ISourceAccountRepository
{
    public List<UserSourceAccountIdentity> Identities { get; } = [];
    public List<SourceAccountRevision> Revisions { get; } = [];
    public int Writes { get; private set; }
    public Guid? AuthLineage { get; set; }
    public Action? BeforeWrite { get; set; }
    public Task<UserSourceAccountIdentity?> IdentityAsync(Guid id) => Task.FromResult(Identities.SingleOrDefault(i => i.UserId == id));
    public Task<UserSourceAccountIdentity?> PairAsync(Guid source, Guid company) => Task.FromResult(Identities.SingleOrDefault(i => i.SourcePersonId == source && i.CompanyId == company));
    public Task<SourceAccountRevision?> RevisionAsync(Guid id, string revision) => Task.FromResult(Revisions.SingleOrDefault(r => r.Profile.UserId == id && r.Profile.RevisionId == revision));
    public Task<SourceAccountRevision?> LatestAsync(Guid id, DateTimeOffset at) => Task.FromResult(Revisions.Where(r => r.Profile.UserId == id && r.Profile.KnownAt <= at).OrderByDescending(r => r.Profile.KnownAt).FirstOrDefault());
    public Task<SourceAccountRevision?> LatestByPairAsync(Guid source, Guid company, DateTimeOffset at) => Task.FromResult(Revisions.Where(r => r.Profile.SourcePersonId == source && r.Profile.CompanyId == company && r.Profile.KnownAt <= at).OrderByDescending(r => r.Profile.KnownAt).FirstOrDefault());
    public Task<bool> TryCaptureAsync(UserSourceAccountProfile profile, string hash)
    {
        Writes++;
        BeforeWrite?.Invoke();
        BeforeWrite = null;
        var expected = new UserSourceAccountIdentity(profile.UserId, profile.SourcePersonId, profile.CompanyId);
        if (AuthLineage is { } lineage && lineage != profile.SourcePersonId ||
            Identities.Any(i => (i.UserId == profile.UserId || i.SourcePersonId == profile.SourcePersonId && i.CompanyId == profile.CompanyId) && i != expected) ||
            Revisions.Any(r => r.Profile.UserId == profile.UserId && (r.Profile.RevisionId == profile.RevisionId || r.Profile.KnownAt >= profile.KnownAt))) return Task.FromResult(false);
        if (!Identities.Contains(expected)) Identities.Add(expected);
        Revisions.Add(new(profile, hash));
        return Task.FromResult(true);
    }
}
