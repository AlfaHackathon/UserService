using System.Text.Json;
using Api.Contracts;
using Api.Services;
using Api.Models;
using System.Reflection;
using System.Runtime.CompilerServices;

var assertions = 0;
void Check(bool condition, string message)
{
    assertions++;
    if (!condition) throw new InvalidOperationException(message);
}

var user = Guid.Parse("68aa2b0c-c285-4c33-9a83-91e2b20d8997");
var company = Guid.Parse("d70d9e6f-36a1-47a3-a62f-6c401d988809");
var sourcePerson = Guid.Parse("ddfd16dc-c62c-43e2-b00c-066ad09478fc");
var at = DateTimeOffset.Parse("2026-10-26T12:00:00Z");
var request = new CaptureViewerContextRequest("capture-1", sourcePerson, new(null, null), new("synthetic-person-context", "v1", new string('a', 64)));
Check(ViewerContextSnapshots.ValidCapture(request), "Explicit nullable geography and actual source lineage must be accepted.");
Check(ViewerContextSnapshots.ValidCapture(request with { SourcePersonId = null }), "Missing lineage remains null.");
Check(!ViewerContextSnapshots.ValidCapture(request with { SourcePersonId = Guid.Empty }), "Empty UUID is not valid lineage.");
var rawGeographies = new string?[] { null, "", "  ", " \tМосква\u00a0  ", "城市e\u0301😀", new string('x', 300), new string('x', 16000) };
var geographyHashes = new HashSet<string>();
foreach (var raw in rawGeographies)
{
    var rawRequest = request with { ServiceGeography = new(raw, raw) };
    Check(ViewerContextSnapshots.ValidCapture(rawRequest), "Raw geography must preserve null, blanks, spacing, Unicode and values longer than 256 characters.");
    var rawSnapshot = ViewerContextSnapshots.Capture(user, company, rawRequest, null, at);
    Check(rawSnapshot.ServiceGeography == rawRequest.ServiceGeography && ViewerContextSnapshots.ValidSnapshot(rawSnapshot, user, company, at),
        "Raw geography must retain its exact value in a valid snapshot.");
    var roundTrip = JsonSerializer.Deserialize<ViewerContextResponse>(JsonSerializer.Serialize(rawSnapshot, ViewerContextSnapshots.Json), ViewerContextSnapshots.Json)!;
    Check(roundTrip.ServiceGeography == rawRequest.ServiceGeography && roundTrip.ContentHash == rawSnapshot.ContentHash &&
        ViewerContextSnapshots.ContentHash(roundTrip) == rawSnapshot.ContentHash, "Exact raw geography and its hash must survive JSON roundtrip.");
    Check(geographyHashes.Add(rawSnapshot.ContentHash), "Null, empty, spacing and Unicode source values must remain distinct snapshot content.");
}
Check(!ViewerContextSnapshots.ValidCapture(request with { ServiceGeography = new("a\0b", null) }), "NUL is rejected at the PostgreSQL text storage boundary.");
Check(!ViewerContextSnapshots.ValidCapture(request with { ServiceGeography = new(null, "a\0b") }), "The NUL storage guard applies to both geography fields.");
Check(!ViewerContextSnapshots.ValidCapture(request with { ServiceGeography = new(new string('x', 16001), null) }), "Geography length is bounded only as a request resource limit.");
Check(!ViewerContextSnapshots.ValidCapture(request with { Provenance = request.Provenance with { ContentHash = "wrong" } }), "Provenance requires an exact SHA256.");

var snapshot = ViewerContextSnapshots.Capture(user, company, request, null, at);
Check(snapshot.Role is null && snapshot.ServiceGeography == new ServiceGeography(null, null), "No role or geography may be invented.");
Check(ViewerContextSnapshots.ValidSnapshot(snapshot, user, company, at), "A snapshot is available at its exact knowledge time.");
Check(!ViewerContextSnapshots.ValidSnapshot(snapshot, user, company, at.AddTicks(-1)), "Future-known snapshots are unavailable historically.");
Check(!ViewerContextSnapshots.ValidSnapshot(snapshot, Guid.NewGuid(), company, at), "A source cannot be rebound to another account.");
Check(!ViewerContextSnapshots.ValidSnapshot(snapshot, user, Guid.NewGuid(), at), "A source cannot be rebound to another company.");
Check(!ViewerContextSnapshots.ValidSnapshot(snapshot with { ServiceGeography = new("changed", null) }, user, company, at), "Geography changes invalidate the snapshot hash.");
Check(!ViewerContextSnapshots.ValidSnapshot(snapshot with { SourceOwner = "company-service" }, user, company, at), "Owner isolation is explicit.");
Check(!ViewerContextSnapshots.ValidSnapshot(snapshot with { Role = new(Guid.NewGuid(), null!, false) }, user, company, at), "Malformed roles fail closed without throwing.");
foreach (var invalidRole in new[] { "", " Финансист ", "role\tname", new string('x', 129) })
{
    var invalidRoleSnapshot = snapshot with { Role = new(Guid.NewGuid(), invalidRole, false) };
    invalidRoleSnapshot = invalidRoleSnapshot with { ContentHash = ViewerContextSnapshots.ContentHash(invalidRoleSnapshot) };
    Check(!ViewerContextSnapshots.ValidSnapshot(invalidRoleSnapshot, user, company, at), "Role-name validation remains separate and unchanged.");
}
Check(ViewerContextSnapshots.RequestHash(request) == ViewerContextSnapshots.RequestHash(request with { }), "Idempotent requests have a stable fingerprint.");
Check(ViewerContextSnapshots.RequestHash(request) != ViewerContextSnapshots.RequestHash(request with { SourcePersonId = null }), "Internal lineage changes must conflict with a reused revision.");

var wire = JsonSerializer.SerializeToElement(snapshot, ViewerContextSnapshots.Json);
Check(wire.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[]
{
    "companyId", "contentHash", "knownAt", "provenance", "revisionId", "role", "serviceGeography", "sourceOwner", "userId"
}), "The safe DTO excludes account PII and lineage identifiers.");
Check(!wire.ToString().Contains(sourcePerson.ToString(), StringComparison.Ordinal), "Source person UUID stays private.");
Check(JsonSerializer.Deserialize<ViewerContextResponse>(wire, ViewerContextSnapshots.Json) == snapshot, "Actual DTO preserves immutable fields across JSON.");

var strictRequestRejected = false;
try
{
    JsonSerializer.Deserialize<CaptureViewerContextRequest>("{\"revisionId\":\"capture-1\",\"sourcePersonId\":null,\"serviceGeography\":{\"cityName\":null,\"rcName\":null},\"provenance\":{\"sourceId\":\"fixture\",\"versionId\":\"v1\",\"contentHash\":\"" + new string('a', 64) + "\"},\"knownAt\":\"2000-01-01T00:00:00Z\"}", ViewerContextSnapshots.Json);
}
catch (JsonException) { strictRequestRejected = true; }
Check(strictRequestRejected, "Callers cannot add a backdated capture time or unrecognized fields.");
var nullability = new NullabilityInfoContext();
foreach (var name in new[] { nameof(User.FirstName), nameof(User.LastName), nameof(User.Email), nameof(User.HashedPassword) })
{
    var property = typeof(User).GetProperty(name)!;
    Check(property.GetCustomAttribute<RequiredMemberAttribute>() is not null && nullability.Create(property).WriteState == NullabilityState.NotNull,
        "Existing auth/account fields must stay required and non-nullable.");
}
Check(nullability.Create(typeof(User).GetProperty(nameof(User.SourcePersonId))!).WriteState == NullabilityState.Nullable &&
    nullability.Create(typeof(User).GetProperty(nameof(User.CityName))!).WriteState == NullabilityState.Nullable &&
    nullability.Create(typeof(User).GetProperty(nameof(User.RcName))!).WriteState == NullabilityState.Nullable,
    "New canonical source lineage/service geography must stay nullable.");
Console.WriteLine($"{assertions} actual viewer snapshot contract/policy assertions passed.");
