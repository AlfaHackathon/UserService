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
using Microsoft.Extensions.Configuration;

internal static class ViewerContextChecks
{
    public static async Task Run()
    {
        var f = new Fixture();
        var before = DateTimeOffset.UtcNow;
        var result = await f.Read();
        Check(result.Error is null && result.Content?.Role is null, "Unassigned role must remain null.");
        Check(result.Content!.ObservedAt >= before && result.Content.ObservedAt <= DateTimeOffset.UtcNow,
            "observedAt must describe the current read.");
        Check(f.Companies.RequestedUserId == f.UserId && f.Roles.Reads == 0,
            "Membership must use the requested viewer; a null role needs no role lookup.");

        foreach (var (name, isDefault) in new[] { ("Финансист", false), ("Ген директор", true) })
        {
            f = new Fixture();
            f.Assign(name, isDefault);
            result = await f.Read();
            Check(result.Error is null && result.Content?.Role is { } role &&
                role.Name == name && role.Id == f.Users.Value!.RoleId && role.IsDefault == isDefault,
                "Custom and default roles must retain their actual assignment.");
            Check(f.Roles.RequestedCompanyId == f.CompanyId && f.Roles.RequestedRoleId == f.Users.Value!.RoleId,
                "Role lookup must be scoped to the verified company and exact assignment.");
            var json = JsonSerializer.SerializeToElement(result.Content, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Check(json.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(
                new[] { "companyId", "observedAt", "role", "userId" }), "Viewer DTO must have an explicit allowlist.");
            Check(json.GetProperty("role").EnumerateObject().Select(p => p.Name).Order().SequenceEqual(
                new[] { "id", "isDefault", "name" }), "Role DTO must not expose extra properties.");
            Check(!json.ToString().Contains("private", StringComparison.OrdinalIgnoreCase),
                "Names, email and unrelated user fields must not reach viewer context.");
        }

        f = new Fixture();
        f.Users.Value = null;
        await ExpectStatus(f, HttpStatusCode.NotFound);
        Check(f.Companies.Reads == 0, "Unknown viewer must not trigger a company lookup.");

        f = new Fixture();
        f.Companies.Value = null;
        await ExpectStatus(f, HttpStatusCode.Forbidden);
        f = new Fixture();
        f.Companies.Value!.Id = Guid.NewGuid();
        await ExpectStatus(f, HttpStatusCode.Forbidden);

        f = new Fixture();
        f.Companies.Failure = new Error("Do not expose raw upstream error.", ErrorSource.External, ErrorType.Undefined)
        { HttpStatusCode = HttpStatusCode.ServiceUnavailable };
        await ExpectStatus(f, HttpStatusCode.BadGateway);
        Check(f.Roles.Reads == 0, "Unavailable membership must not resolve a role or imply role=null.");

        f = new Fixture();
        f.Assign("Финансист", false);
        f.Roles.Value = null;
        await ExpectStatus(f, HttpStatusCode.BadGateway);
        f = new Fixture();
        f.Assign("Финансист", false);
        f.Roles.Value!.CompanyId = Guid.NewGuid();
        await ExpectStatus(f, HttpStatusCode.BadGateway);
        f = new Fixture();
        f.Assign("Ген директор", true);
        f.Roles.Value!.CompanyId = f.CompanyId;
        await ExpectStatus(f, HttpStatusCode.BadGateway);
        f = new Fixture();
        f.Assign("Финансист", false);
        f.Roles.Value!.Id = Guid.NewGuid();
        await ExpectStatus(f, HttpStatusCode.BadGateway);
        f = new Fixture();
        f.Assign(" ", false);
        await ExpectStatus(f, HttpStatusCode.BadGateway);

        f = new Fixture();
        Check((await f.Service.GetAsync(Guid.Empty, f.CompanyId)).Error?.HttpStatusCode == HttpStatusCode.BadRequest,
            "Empty viewer identity must fail.");
        Check((await f.Service.GetAsync(f.UserId, Guid.Empty)).Error?.HttpStatusCode == HttpStatusCode.BadRequest,
            "Empty company identity must fail.");

        foreach (var provided in new[] { "", "wrong", "internal-fixture" })
        {
            f = new Fixture();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["Internal:Secret"] = "internal-fixture" }).Build();
            var controller = new ViewerContextController(f.Service, configuration)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            if (provided.Length > 0)
                controller.Request.Headers["X-Internal-Secret"] = provided;
            var response = await controller.GetAsync(f.UserId, f.CompanyId);
            Check(provided == "internal-fixture" ? response is OkObjectResult : response is UnauthorizedObjectResult,
                "The internal route must require the configured internal secret.");
            Check(provided == "internal-fixture" || f.Users.Reads == 0,
                "Unauthenticated requests must not read a viewer.");
        }

        f = new Fixture();
        var unconfigured = new ViewerContextController(f.Service, new ConfigurationBuilder().Build())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        unconfigured.Request.Headers["X-Internal-Secret"] = "internal-fixture";
        Check(await unconfigured.GetAsync(f.UserId, f.CompanyId) is UnauthorizedObjectResult && f.Users.Reads == 0,
            "An unconfigured internal secret must fail closed.");
        Console.WriteLine("Viewer-context fixtures passed (membership, assigned/null/default/custom role, internal secret, exact DTO and failures).");
    }

    private static async Task ExpectStatus(Fixture f, HttpStatusCode status)
    {
        var result = await f.Read();
        Check(result.Error?.HttpStatusCode == status && result.Content is null,
            $"Expected HTTP {(int)status} without a usable viewer context.");
    }

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public FakeUsers Users { get; }
        public FakeRoles Roles { get; } = new();
        public FakeCompanies Companies { get; }
        public ViewerContextService Service { get; }

        public Fixture()
        {
            Users = new FakeUsers
            {
                Value = new UserResponse
                {
                    Id = UserId,
                    FirstName = "private-first",
                    LastName = "private-last",
                    MiddleName = "private-middle",
                    Email = "private@example.invalid",
                    CreatedAt = DateTime.UtcNow
                }
            };
            Companies = new FakeCompanies { Value = new CompanyInfoResponse { Id = CompanyId, Name = "private-company" } };
            Service = new ViewerContextService(Users, Roles, Companies);
        }

        public void Assign(string name, bool isDefault)
        {
            var id = Guid.NewGuid();
            Users.Value!.RoleId = id;
            Users.Value.RoleName = "stale-role-label";
            Roles.Value = new Role
            {
                Id = id,
                Name = name,
                IsDefault = isDefault,
                CompanyId = isDefault ? null : CompanyId,
                CreatedAt = DateTime.UtcNow
            };
        }

        public Task<Result<ViewerContextResponse>> Read() => Service.GetAsync(UserId, CompanyId);
    }

    private sealed class FakeUsers : IUserRepository
    {
        public UserResponse? Value { get; set; }
        public int Reads { get; private set; }
        public Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult(Value);
        }
        public Task<List<UserResponse>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User> AddAsync(string lastName, string firstName, string? middleName, string email, string hashedPassword,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeCompanies : ICompanyServiceClient
    {
        public CompanyInfoResponse? Value { get; set; }
        public Error? Failure { get; set; }
        public int Reads { get; private set; }
        public Guid RequestedUserId { get; private set; }
        public Task<Result<CompanyInfoResponse?>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            Reads++;
            RequestedUserId = userId;
            return Task.FromResult(Failure is { } error ? new Result<CompanyInfoResponse?>(error) : new Result<CompanyInfoResponse?>(Value));
        }
        public Task<List<Guid>?> GetMemberIdsAsync(Guid companyId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeRoles : IRoleRepository
    {
        public Role? Value { get; set; }
        public int Reads { get; private set; }
        public Guid RequestedRoleId { get; private set; }
        public Guid RequestedCompanyId { get; private set; }
        public Task<Role?> GetByIdInCompanyAsync(Guid roleId, Guid companyId, CancellationToken cancellationToken = default)
        {
            Reads++;
            RequestedRoleId = roleId;
            RequestedCompanyId = companyId;
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
