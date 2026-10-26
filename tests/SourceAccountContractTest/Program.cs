using System.Text.Json;
using Api.Contracts;
using Api.Models;
using Api.Services;

var count = 0;
void Check(bool valid, string message) { count++; if (!valid) throw new InvalidOperationException(message); }
var user = Guid.NewGuid();
var source = Guid.NewGuid();
var company = Guid.NewGuid();
var identity = new UserSourceAccountIdentity(user, source, company);
var at = DateTimeOffset.Parse("2026-10-27T12:00:00Z");
var request = new CaptureSourceAccountRequest(source, company, "source-1", new(null, null), new("synthetic-account-original", "v1", new string('a', 64)));
Check(SourceAccountProfiles.ValidCapture(user, request), "Explicit UUID pair and nullable raw geography are valid without auth fields.");
Check(!SourceAccountProfiles.ValidCapture(Guid.Empty, request), "An empty logical account is rejected.");
Check(!SourceAccountProfiles.ValidCapture(user, request with { SourcePersonId = Guid.Empty }), "F lineage is required.");
Check(!SourceAccountProfiles.ValidCapture(user, request with { CompanyId = Guid.Empty }), "U identity is required.");
foreach (var suffix in new[] { "\n", "\r\n" })
{
    Check(!SourceAccountProfiles.ValidCapture(user, request with { Provenance = request.Provenance with { ContentHash = new string('a', 64) + suffix } }),
        "Exact SHA256 metadata must reject trailing LF/CRLF.");
}
Check(SourceAccountProfiles.CanBind(user, request, identity, identity), "The exact account/pair can be replayed.");
Check(!SourceAccountProfiles.CanBind(Guid.NewGuid(), request, null, identity), "The same F/U pair cannot claim a second account UUID.");
Check(!SourceAccountProfiles.CanBind(user, request with { SourcePersonId = Guid.NewGuid() }, identity, null), "An account UUID cannot be rebound to another F.");
Check(!SourceAccountProfiles.CanBind(user, request with { CompanyId = Guid.NewGuid() }, identity, null), "An account UUID cannot be rebound to another U.");
Check(SourceAccountProfiles.ViewerCompatible(identity, user, company, source), "Exact canonical/auth capture linkage is compatible.");
Check(!SourceAccountProfiles.ViewerCompatible(identity, user, company, null), "A canonical pair cannot be cleared through viewer capture.");
Check(!SourceAccountProfiles.ViewerCompatible(identity, user, Guid.NewGuid(), source), "Viewer company cannot contradict the canonical pair.");
Check(!SourceAccountProfiles.ViewerCompatible(identity, user, company, Guid.NewGuid()), "Viewer lineage cannot contradict the canonical pair.");
Check(SourceAccountProfiles.ViewerCompatible(null, user, company, null), "Legacy unbound auth accounts retain nullable viewer semantics.");
var hashes = new HashSet<string>();
foreach (var raw in new string?[] { null, "", " \t ", "  Москва\u00a0 ", "城市e\u0301😀", new string('x', 300), new string('x', 16000) })
{
    var input = request with { ServiceGeography = new(raw, raw) };
    Check(SourceAccountProfiles.ValidCapture(user, input), "Raw source values are accepted without trimming or guessed widths.");
    var profile = SourceAccountProfiles.Capture(user, input, at);
    Check(SourceAccountProfiles.ValidProfile(profile, user, source, company, at), "Exact immutable source profile validates.");
    Check(!SourceAccountProfiles.ValidProfile(profile, user, source, company, at.AddTicks(-1)), "Future source identity is unavailable historically.");
    var json = JsonSerializer.Serialize(profile, ViewerContextSnapshots.Json);
    var roundTrip = JsonSerializer.Deserialize<UserSourceAccountProfile>(json, ViewerContextSnapshots.Json)!;
    Check(roundTrip == profile && roundTrip.ServiceGeography == input.ServiceGeography, "Raw nullable source fields and hashes survive JSON roundtrip.");
    Check(hashes.Add(profile.ContentHash), "Raw empty, spacing and null remain distinct immutable content.");
    Check(!json.Contains("\"role\"", StringComparison.Ordinal) && !json.Contains("email", StringComparison.OrdinalIgnoreCase) && !json.Contains("password", StringComparison.OrdinalIgnoreCase), "Source-only profile contains no inferred role or auth identity data.");
}
Check(!SourceAccountProfiles.ValidCapture(user, request with { ServiceGeography = new("a\0b", null) }), "PostgreSQL text cannot store NUL.");
Check(!SourceAccountProfiles.ValidCapture(user, request with { ServiceGeography = new(null, new string('x', 16001)) }), "Source request resource bounds remain explicit.");
var original = SourceAccountProfiles.Capture(user, request, at);
Check(!SourceAccountProfiles.ValidProfile(original with { CompanyId = Guid.NewGuid() }, user, source, company, at), "Snapshot scope/hash cannot be rebound.");
Check(!SourceAccountProfiles.ValidProfile(original with { ServiceGeography = new("changed", null) }, user, source, company, at), "Geography revisions require a new immutable hash.");
Check(SourceAccountProfiles.RequestHash(request) != SourceAccountProfiles.RequestHash(request with { SourcePersonId = Guid.NewGuid() }), "Pair changes invalidate request replay fingerprints.");
Console.WriteLine($"{count} actual source-account contract/policy assertions passed.");
