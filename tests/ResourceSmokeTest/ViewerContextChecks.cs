using System.Net;
using System.Text.Json;
using AlfaHackaton.Result;
using AlfaHackaton.Result.Errors;
using Api.Contracts;
using Api.Controllers;
using Api.Models;
using Api.Repositories.Interfaces;
using Api.Services;
using Api.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Configuration;

internal static class ViewerContextChecks
{
    private static int assertions;

    public static async Task Run()
    {
        var captureLimit = (IRequestSizeLimitMetadata)typeof(ViewerContextController).GetMethod(nameof(ViewerContextController.CaptureAsync))!
            .GetCustomAttributes(typeof(RequestSizeLimitAttribute), false).Single();
        Check(captureLimit.MaxRequestBodySize == 64 * 1024, "The HTTP capture boundary must limit bodies to 64 KiB.");
        var f = new Fixture();
        await Status(f.Read(), HttpStatusCode.NotFound);
        Check(f.Snapshots.Captures == 0 && f.Roles.Reads == 0, "GET must not capture or consult the live role.");
        var captured = await f.Capture();
        Check(captured.Error is null && captured.Content!.Role is null, "Unassigned role must remain null.");
        Check(captured.Content!.KnownAt == f.Clock.Now && captured.Content.ServiceGeography == new ServiceGeography(null, null),
            "Server knowledge time and nullable service geography must be preserved.");
        Check(f.Snapshots.Lineage == f.Request.SourcePersonId && f.Companies.LastUserId == f.UserId,
            "Internal lineage and requested account scope must be explicit.");
        var wire = JsonSerializer.SerializeToElement(captured.Content, ViewerContextSnapshots.Json);
        Check(wire.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[]
        {
            "companyId", "contentHash", "knownAt", "provenance", "revisionId", "role", "serviceGeography", "sourceOwner", "userId"
        }), "Safe viewer DTO must exclude identity PII, grants and lineage.");
        Check(!wire.ToString().Contains("private", StringComparison.OrdinalIgnoreCase), "Account names and email must not escape.");

        await Status(f.Read(f.Clock.Now.AddTicks(-1)), HttpStatusCode.NotFound);
        Check((await f.Read()).Content == captured.Content, "Snapshot must be readable at exact knownAt.");
        f.Assign("Финансист", false);
        f.Clock.Now = f.Clock.Now.AddHours(1);
        Check((await f.Read()).Content == captured.Content && f.Roles.Reads == 0,
            "Live role changes must not replace a historical/null captured role.");
        var replay = await f.Capture();
        Check(replay.Content == captured.Content && f.Snapshots.Captures == 1 && f.Roles.Reads == 0,
            "Identical revision replay must preserve its old role, geography and knowledge time without writing.");
        await Status(f.Service.CaptureAsync(f.UserId, f.CompanyId, f.Request with { ServiceGeography = new("changed", null) }), HttpStatusCode.Conflict);
        Check(f.Snapshots.Captures == 1, "Changed payload must not update canonical fields under a reused revision.");

        f.Request = f.Request with { RevisionId = "revision-2", ServiceGeography = new("service-city", "service-rc") };
        var second = await f.Capture();
        Check(second.Content!.Role?.Name == "Финансист" && !second.Content.Role.IsDefault &&
            second.Content.ServiceGeography == f.Request.ServiceGeography, "A new explicit capture must retain role and service geography.");
        Check(f.Roles.LastCompanyId == f.CompanyId && f.Roles.LastRoleId == f.Users.Value!.RoleId,
            "Assigned role lookup must use the exact account role and verified company.");
        Check((await f.Read(captured.Content.KnownAt)).Content == captured.Content, "New captures must preserve old historical state.");

        f.Assign("Ген директор", true);
        f.Clock.Now = f.Clock.Now.AddHours(1);
        f.Request = f.Request with { RevisionId = "revision-3", SourcePersonId = null, ServiceGeography = new(null, null) };
        var director = await f.Capture();
        Check(director.Content?.Role is { IsDefault: true, Name: "Ген директор" } && f.Snapshots.Lineage is null,
            "Global default roles are captured without inventing source lineage.");

        foreach (var badRole in new[] { "foreign", "missing", "wrong-id", "bad-default" })
        {
            f = new Fixture();
            f.Assign("Финансист", false);
            if (badRole == "foreign") f.Roles.Value!.CompanyId = Guid.NewGuid();
            if (badRole == "missing") f.Roles.Value = null;
            if (badRole == "wrong-id") f.Roles.Value!.Id = Guid.NewGuid();
            if (badRole == "bad-default") f.Roles.Value!.IsDefault = true;
            await Status(f.Capture(), HttpStatusCode.BadGateway);
            Check(f.Snapshots.Captures == 0, "An unverifiable assigned role must not become a snapshot or null success.");
        }

        f = new Fixture();
        f.Users.Value = null;
        await Status(f.Capture(), HttpStatusCode.NotFound);
        Check(f.Companies.Reads == 0 && f.Snapshots.Captures == 0, "Capture may not create an account.");
        f = new Fixture();
        f.Companies.Value = null;
        await Status(f.Capture(), HttpStatusCode.Forbidden);
        await Status(f.Read(), HttpStatusCode.Forbidden);
        Check(f.Snapshots.Reads == 0 && f.Snapshots.Captures == 0, "No membership must prevent snapshot reads and writes.");
        f = new Fixture();
        f.Companies.Value!.Id = Guid.NewGuid();
        await Status(f.Capture(), HttpStatusCode.Forbidden);
        await Status(f.Read(), HttpStatusCode.Forbidden);
        f = new Fixture();
        f.Companies.Error = new("Raw upstream details must not escape.", ErrorSource.External, ErrorType.Undefined)
        { HttpStatusCode = HttpStatusCode.ServiceUnavailable };
        await Status(f.Read(), HttpStatusCode.BadGateway);
        await Status(f.Capture(), HttpStatusCode.BadGateway);
        Check(f.Snapshots.Reads == 0, "Unavailable membership must not imply an empty or null-role result.");

        f = new Fixture();
        await f.Capture();
        var original = f.Snapshots.Revisions.Single();
        f.Snapshots.OverrideRead = original with { Context = original.Context with { ContentHash = new string('b', 64) } };
        await Status(f.Read(), HttpStatusCode.BadGateway);
        f.Snapshots.OverrideRead = original with { Context = original.Context with { CompanyId = Guid.NewGuid() } };
        await Status(f.Read(), HttpStatusCode.BadGateway);
        f.Snapshots.OverrideRead = original with { Context = original.Context with { KnownAt = f.Clock.Now.AddDays(1) } };
        await Status(f.Read(), HttpStatusCode.BadGateway);

        f = new Fixture();
        await Status(f.Service.GetAsync(f.UserId, f.CompanyId, default), HttpStatusCode.BadRequest);
        await Status(f.Service.GetAsync(Guid.Empty, f.CompanyId, f.Clock.Now), HttpStatusCode.BadRequest);
        await Status(f.Service.CaptureAsync(f.UserId, f.CompanyId, f.Request with { SourcePersonId = Guid.Empty }), HttpStatusCode.BadRequest);
        Check(f.Users.Reads == 0, "Malformed requests must fail before account access.");

        foreach (var raw in new string?[] { null, "", " \t ", " \tМосква\u00a0  ", "城市e\u0301😀", new string('x', 300), new string('x', 16000) })
        {
            f = new Fixture();
            f.Request = f.Request with { ServiceGeography = new(raw, raw) };
            var rawCapture = await f.Capture();
            Check(rawCapture.Error is null && rawCapture.Content!.ServiceGeography == f.Request.ServiceGeography,
                "Capture must preserve raw nullable/blank/spacing/Unicode/long geography without trimming.");
            var rawRead = await f.Read();
            var roundTrip = JsonSerializer.Deserialize<ViewerContextResponse>(
                JsonSerializer.Serialize(rawRead.Content, ViewerContextSnapshots.Json), ViewerContextSnapshots.Json)!;
            Check(roundTrip.ServiceGeography == f.Request.ServiceGeography && roundTrip.ContentHash == rawCapture.Content!.ContentHash,
                "Historical read and JSON must retain the exact captured raw geography/hash.");
        }
        f = new Fixture();
        await Status(f.Service.CaptureAsync(f.UserId, f.CompanyId, f.Request with { ServiceGeography = new("a\0b", null) }), HttpStatusCode.BadRequest);
        await Status(f.Service.CaptureAsync(f.UserId, f.CompanyId, f.Request with { ServiceGeography = new(null, new string('x', 16001)) }), HttpStatusCode.BadRequest);
        Check(f.Users.Reads == 0, "Storage/resource-invalid geography must fail before account access.");

        f = new Fixture();
        f.Snapshots.Conflict = true;
        await Status(f.Capture(), HttpStatusCode.Conflict);
        Check(f.Snapshots.Revisions.Count == 0, "Failed inserts must not claim a captured snapshot.");

        f = new Fixture();
        f.Sources.Identities.Add(new(f.UserId, f.Request.SourcePersonId!.Value, f.CompanyId));
        Check((await f.Capture()).Error is null, "Exact canonical identity remains compatible with authenticated capture.");
        f = new Fixture();
        f.Sources.Identities.Add(new(f.UserId, Guid.NewGuid(), f.CompanyId));
        await Status(f.Capture(), HttpStatusCode.Conflict);
        Check(f.Snapshots.Captures == 0, "Canonical identity conflicts must fail in viewer preflight.");
        f = new Fixture();
        f.Sources.Identities.Add(new(f.UserId, f.Request.SourcePersonId!.Value, f.CompanyId));
        await Status(f.Service.CaptureAsync(f.UserId, f.CompanyId, f.Request with { SourcePersonId = null }), HttpStatusCode.Conflict);

        f = new Fixture();
        f.Request = f.Request with { SourcePersonId = null };
        var legacy = await f.Capture();
        f.Sources.Identities.Add(new(f.UserId, Guid.NewGuid(), f.CompanyId));
        Check((await f.Capture()).Content == legacy.Content && f.Snapshots.Captures == 1,
            "An exact legacy revision replay must survive later canonical identity creation without a new write.");
        await Status(f.Service.CaptureAsync(f.UserId, f.CompanyId, f.Request with { RevisionId = "new-null-lineage" }), HttpStatusCode.Conflict);
        Check(f.Snapshots.Captures == 1, "A new null-lineage capture must still respect the present canonical pair.");
        f.Companies.Value = null;
        await Status(f.Capture(), HttpStatusCode.Forbidden);

        foreach (var secret in new[] { "", "wrong", "fixture-internal" })
        {
            f = new Fixture();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Internal:Secret"] = "fixture-internal" }).Build();
            var controller = new ViewerContextController(f.Service, config)
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
            if (secret.Length > 0) controller.Request.Headers["X-Internal-Secret"] = secret;
            var response = await controller.CaptureAsync(f.UserId, f.CompanyId, f.Request);
            Check(secret == "fixture-internal" ? response is OkObjectResult : response is UnauthorizedObjectResult,
                "Internal capture requires the existing configured secret.");
            var read = await controller.GetAsync(f.UserId, f.CompanyId, f.Clock.Now);
            Check(secret == "fixture-internal" ? read is OkObjectResult : read is UnauthorizedObjectResult,
                "Internal historical read requires the configured secret.");
            Check(secret == "fixture-internal" || f.Users.Reads == 0, "Unauthorized calls must not touch accounts.");
        }
        f = new Fixture();
        var unconfigured = new ViewerContextController(f.Service, new ConfigurationBuilder().Build())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        unconfigured.Request.Headers["X-Internal-Secret"] = "fixture-internal";
        Check(await unconfigured.CaptureAsync(f.UserId, f.CompanyId, f.Request) is UnauthorizedObjectResult && f.Users.Reads == 0,
            "Unconfigured internal authentication must fail closed.");
        Console.WriteLine($"{assertions} viewer-context service/controller fixture assertions passed without a database or model.");
    }

    private static async Task Status(Task<Result<ViewerContextResponse>> task, HttpStatusCode status)
    {
        var result = await task;
        Check(result.Error?.HttpStatusCode == status && result.Content is null, $"Expected {(int)status} without usable viewer data.");
    }

    private static void Check(bool valid, string message)
    {
        assertions++;
        if (!valid) throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public FakeUsers Users { get; }
        public FakeCompanies Companies { get; }
        public FakeRoles Roles { get; } = new();
        public FakeSnapshots Snapshots { get; } = new();
        public FakeSourceAccounts Sources { get; } = new();
        public Clock Clock { get; } = new();
        public CaptureViewerContextRequest Request { get; set; } = new("revision-1", Guid.NewGuid(), new(null, null), new("synthetic-person-context", "v1", new string('a', 64)));
        public ViewerContextService Service { get; }
        public Fixture()
        {
            Users = new() { Value = new UserResponse { Id = UserId, FirstName = "private-first", LastName = "private-last", Email = "private@example.invalid", CreatedAt = DateTime.UtcNow } };
            Companies = new() { Value = new CompanyInfoResponse { Id = CompanyId, Name = "private-company" } };
            Service = new(Users, Roles, Companies, Snapshots, Sources, Clock);
        }
        public void Assign(string name, bool isDefault)
        {
            var id = Guid.NewGuid();
            Users.Value!.RoleId = id;
            Users.Value.RoleName = "stale-label";
            Roles.Value = new Role { Id = id, Name = name, IsDefault = isDefault, CompanyId = isDefault ? null : CompanyId, CreatedAt = DateTime.UtcNow };
        }
        public Task<Result<ViewerContextResponse>> Capture() => Service.CaptureAsync(UserId, CompanyId, Request);
        public Task<Result<ViewerContextResponse>> Read(DateTimeOffset? at = null) => Service.GetAsync(UserId, CompanyId, at ?? Clock.Now);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-10-26T12:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class FakeSnapshots : IViewerContextRepository
    {
        public List<ViewerContextRevision> Revisions { get; } = [];
        public ViewerContextRevision? OverrideRead { get; set; }
        public int Reads { get; private set; }
        public int Captures { get; private set; }
        public Guid? Lineage { get; private set; }
        public bool Conflict { get; set; }
        public Task<ViewerContextRevision?> FindAsync(Guid userId, Guid companyId, string revisionId)
        {
            Reads++;
            return Task.FromResult(Revisions.SingleOrDefault(r => r.Context.UserId == userId && r.Context.CompanyId == companyId && r.Context.RevisionId == revisionId));
        }
        public Task<ViewerContextRevision?> LatestAsync(Guid userId, Guid companyId, DateTimeOffset asOf)
        {
            Reads++;
            return Task.FromResult(OverrideRead ?? Revisions.Where(r => r.Context.UserId == userId && r.Context.CompanyId == companyId && r.Context.KnownAt <= asOf).OrderByDescending(r => r.Context.KnownAt).FirstOrDefault());
        }
        public Task<bool> TryCaptureAsync(ViewerContextResponse context, string requestHash, Guid? sourcePersonId)
        {
            Captures++;
            if (Conflict) return Task.FromResult(false);
            Revisions.Add(new(context, requestHash));
            Lineage = sourcePersonId;
            return Task.FromResult(true);
        }
    }
    private sealed class FakeUsers : IUserRepository
    {
        public UserResponse? Value { get; set; }
        public int Reads { get; private set; }
        public Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) { Reads++; return Task.FromResult(Value); }
        public Task<List<UserResponse>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User> AddAsync(string lastName, string firstName, string? middleName, string email, string hashedPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class FakeCompanies : ICompanyServiceClient
    {
        public CompanyInfoResponse? Value { get; set; }
        public Error? Error { get; set; }
        public int Reads { get; private set; }
        public Guid LastUserId { get; private set; }
        public Task<Result<CompanyInfoResponse?>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            Reads++;
            LastUserId = userId;
            return Task.FromResult(Error is { } error ? new Result<CompanyInfoResponse?>(error) : new Result<CompanyInfoResponse?>(Value));
        }
        public Task<List<Guid>?> GetMemberIdsAsync(Guid companyId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class FakeRoles : IRoleRepository
    {
        public Role? Value { get; set; }
        public int Reads { get; private set; }
        public Guid LastRoleId { get; private set; }
        public Guid LastCompanyId { get; private set; }
        public Task<Role?> GetByIdInCompanyAsync(Guid roleId, Guid companyId, CancellationToken cancellationToken = default)
        {
            Reads++;
            LastRoleId = roleId;
            LastCompanyId = companyId;
            return Task.FromResult(Value);
        }
        public Task<List<Role>> GetVisibleAsync(Guid? companyId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Role> CreateCustomAsync(Role role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteCustomAsync(Guid roleId, Guid companyId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AssignToUserAsync(Guid userId, Guid? roleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> CountUsersWithRoleAsync(Guid roleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> NameExistsAsync(Guid companyId, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
