using System.Text.Json;
using AlfaHackaton.DapperContext.Common.Interfaces.Dapper;
using AlfaHackaton.DapperContext.Dapper;
using Api.Contracts;
using Api.Services;
using Api.Resources;
using Microsoft.AspNetCore.Mvc;

namespace Api.CsvImport;

[ApiController, Route("internal/csv-import")]
public sealed class CsvController(IDapperContext db, IConfiguration config) : ControllerBase
{
    private bool Access => !string.IsNullOrEmpty(config["Internal:Secret"]) && config["Internal:Secret"]==Request.Headers["X-Internal-Secret"].ToString();
    private bool Enabled => config.GetValue<bool>("CsvImport:Enabled");
    private IActionResult Fail(string code,int status=409) => StatusCode(status,new {status="blocked",code,skipped=0,conflicts=status==409 ? 1 : 0,complete=false});
    private static object P(CsvBatch b,string hash,string receipt="",string prepared="[]") => new {
        source=b.SourceId,version=b.VersionId,phase=b.Phase,manifestHash=b.ManifestHash,manifestJson=b.ManifestJson,
        start=b.StartRow,end=b.StartRow+b.Rows.Length-1,requestHash=hash,receipt,prepared };

    [HttpGet("readiness")]
    public IActionResult Ready() => !Access ? Unauthorized() : !Enabled ? Fail("import_disabled",503) : Ok(new {contract=1,owner="user",enabled=true,sourceOnly=true});

    [HttpGet("verified")]
    public async Task<IActionResult> Verified([FromQuery]string sourceId,[FromQuery]string versionId,[FromQuery]string manifestHash,[FromQuery]string phase)
    {
        if(!Access) return Unauthorized();
        var raw=await db.FirstOrDefault<string>(new QueryObject(CsvSql.Verified,new {source=sourceId,version=versionId,manifestHash,phase}));
        return string.IsNullOrEmpty(raw) ? Fail("phase_not_verified",412) : Content(raw,"application/json");
    }

    [HttpPost("batches"),RequestSizeLimit(1024*1024)]
    public async Task<IActionResult> Batch([FromBody]CsvBatch b,CancellationToken ct)
    {
        if(!Access) return Unauthorized();
        if(!Enabled) return Fail("import_disabled",503);
        try
        {
            using var manifest=CsvProtocol.Validate(b,["accounts","links"]);
            var hash=CsvProtocol.Hash(CsvProtocol.Serialize(b));
            var p=P(b,hash);
            var replay=await db.FirstOrDefault<string>(new QueryObject(CsvSql.Replay,p));
            if(!string.IsNullOrEmpty(replay)) return Replay(replay,hash);
            if(!await Prerequisite(b,ct)) return Fail("company_or_account_phase_not_verified",412);
            var root=manifest.RootElement;
            using var tx=db.BeginTransaction();
            // Same source-before-auth lock order as PR6. Batch source-only admission
            // rejects auth-backed IDs instead of bypassing membership validation.
            await tx.Command(new QueryObject("SET LOCAL lock_timeout='5s'"));
            await tx.Command(new QueryObject(SourceAccountSql.Lock));
            await tx.Command(new QueryObject("LOCK TABLE users IN SHARE ROW EXCLUSIVE MODE; SET LOCAL lock_timeout='5s'"));
            replay=await tx.CommandWithResponse<string>(new QueryObject(CsvSql.Replay,p));
            if(replay.Length>0) {tx.Commit(); return Replay(replay,hash);}
            await tx.Command(new QueryObject(CsvSql.Bind,p));
            if(!await tx.CommandWithResponse<bool>(new QueryObject(CsvSql.Bound,p))) return Fail("source_version_hash_conflict");
            if(await tx.CommandWithResponse<long>(new QueryObject(CsvSql.Next,p))!=b.StartRow) return Fail("checkpoint_gap_or_overlap");
            var pairs=b.Rows.Select(row=> {
                var f=CsvProtocol.Fields(root,b,row);
                return new {personId=CsvProtocol.Uuid(CsvProtocol.Cell(root,b,f,"person_id")),companyId=CsvProtocol.Uuid(CsvProtocol.Cell(root,b,f,"company_id"))};
            }).ToArray();
            var mapRaw=await tx.CommandWithResponse<string>(new QueryObject(CsvSql.PairMap,new {pairs=CsvProtocol.Serialize(pairs)}));
            using var map=JsonDocument.Parse(mapRaw);
            var accountMap=map.RootElement.EnumerateArray().ToDictionary(x=>(x.GetProperty("personId").GetGuid(),x.GetProperty("companyId").GetGuid()),x=>x.GetProperty("userId").GetGuid());
            var prepared=new List<object>();
            var identities=new HashSet<Guid>();
            var uniquePairs=new HashSet<(Guid,Guid)>();
            var now=DateTimeOffset.UtcNow.UtcTicks;
            foreach(var row in b.Rows)
            {
                var fields=CsvProtocol.Fields(root,b,row);
                string Cell(string key)=>CsvProtocol.Cell(root,b,fields,key);
                var person=CsvProtocol.Uuid(Cell("person_id")); var company=CsvProtocol.Uuid(Cell("company_id"));
                var user=b.Phase=="accounts" ? CsvProtocol.Uuid(Cell("account_id")) : accountMap.GetValueOrDefault((person,company));
                // Multiple source rows for one account must be in separate batches, so knowledge
                // time remains ordered. Exact row identity is retained even for repeated pairs.
                if(user==Guid.Empty || !identities.Add(user) || !uniquePairs.Add((person,company))) return Fail("missing_or_repeated_account_in_batch");
                var geography=b.Phase=="accounts" ? new ServiceGeography(null,null) : new ServiceGeography(Cell("city_name"),Cell("rc_name"));
                var request=new CaptureSourceAccountRequest(person,company,"csv-"+b.ManifestHash+"-"+b.Phase+"-"+row.Number,
                    geography,new(b.SourceId,b.VersionId,b.ManifestHash));
                if(!SourceAccountProfiles.ValidCapture(user,request)) return Fail("invalid_source_profile",400);
                var ticks=checked(now+prepared.Count);
                var profile=SourceAccountProfiles.Capture(user,request,new DateTimeOffset(ticks,TimeSpan.Zero));
                var raw=CsvProtocol.Serialize(fields);
                prepared.Add(new {number=row.Number,userId=user,personId=person,companyId=company,ticks,
                    revision=request.RevisionId,requestHash=SourceAccountProfiles.RequestHash(request),profile=CsvProtocol.Serialize(profile),raw,hash=CsvProtocol.Hash(raw)});
            }
            var receipt=CsvProtocol.Serialize(CsvProtocol.Receipt(b,hash));
            p=P(b,hash,receipt,CsvProtocol.Serialize(prepared));
            if(await tx.CommandWithResponse<bool>(new QueryObject(CsvSql.Conflict,p))) return Fail("auth_identity_pair_or_time_conflict_use_individual_owner_contract");
            await tx.Command(new QueryObject(CsvSql.InsertAccounts,p));
            await tx.Command(new QueryObject(CsvSql.InsertRaw,p));
            await tx.Command(new QueryObject(CsvSql.Checkpoint,p));
            tx.Commit(); return Content(receipt,"application/json");
        }
        catch(Exception e) when(e is InvalidDataException or JsonException or KeyNotFoundException or FormatException or OverflowException)
        {return Fail("invalid_source_batch",400);}
        catch(Exception) {return Fail("owner_outcome_uncertain_retry_identical_batch",503);}
    }
    private IActionResult Replay(string raw,string hash)=>JsonSerializer.Deserialize<CsvReceipt>(raw,CsvProtocol.Json)?.RequestHash==hash ? Content(raw,"application/json") : Fail("batch_content_conflict");

    [HttpGet("checkpoint")]
    public async Task<IActionResult> Checkpoint([FromQuery]string sourceId,[FromQuery]string versionId,[FromQuery]string manifestHash,[FromQuery]string phase)
    {
        if(!Access) return Unauthorized();
        if(!Enabled || phase is not ("accounts" or "links")) return Fail("import_not_ready",503);
        var p=new {source=sourceId,version=versionId,manifestHash,phase};
        var bound=await db.FirstOrDefault<string>(new QueryObject("SELECT COALESCE((SELECT manifest_hash FROM csv_import_versions WHERE source_id=@source AND version_id=@version),'')",p));
        if(!string.IsNullOrEmpty(bound) && bound!=manifestHash) return Fail("source_version_hash_conflict");
        return Ok(new {nextRow=await db.FirstOrDefault<long>(new QueryObject(CsvSql.Next,p)),manifestHash,phase});
    }

    private async Task<bool> Prerequisite(CsvBatch b,CancellationToken ct)
    {
        if(b.Phase=="links") return !string.IsNullOrEmpty(await db.FirstOrDefault<string>(new QueryObject(CsvSql.Verified,
            new {source=b.SourceId,version=b.VersionId,manifestHash=b.ManifestHash,phase="accounts"})));
        var address=config["CsvImport:CompanyServiceBaseUrl"];
        if(!Uri.TryCreate(address?.TrimEnd('/')+"/",UriKind.Absolute,out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return false;
        using var client=new HttpClient(new SocketsHttpHandler {AllowAutoRedirect=false}) {Timeout=TimeSpan.FromSeconds(10)};
        using var request=new HttpRequestMessage(HttpMethod.Get,new Uri(uri,$"company-service/internal/csv-import/verified?sourceId={Uri.EscapeDataString(b.SourceId)}&versionId={Uri.EscapeDataString(b.VersionId)}&manifestHash={b.ManifestHash}&phase=persons"));
        request.Headers.Add("X-Internal-Secret",config["Internal:Secret"]);
        using var response=await client.SendAsync(request,ct);
        if(!response.IsSuccessStatusCode) return false;
        using var doc=JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(ct));
        return doc.RootElement.GetProperty("verified").GetBoolean();
    }

    [HttpPost("verify"),RequestSizeLimit(200000)]
    public async Task<IActionResult> Verify([FromBody]CsvBatch b)
    {
        if(!Access) return Unauthorized();
        if(!Enabled || b.Phase is not ("accounts" or "links") || !CsvProtocol.Sha(b.ManifestHash) || CsvProtocol.Hash(b.ManifestJson)!=b.ManifestHash) return Fail("invalid_verification",400);
        try
        {
            using var manifest=JsonDocument.Parse(b.ManifestJson);
            using var tx=db.BeginTransaction();
            await tx.Command(new QueryObject(SourceAccountSql.Lock));
            var p=P(b,"");
            if(!await tx.CommandWithResponse<bool>(new QueryObject(CsvSql.Bound,p))) return Fail("source_version_hash_conflict");
            var count=await tx.CommandWithResponse<long>(new QueryObject("SELECT COUNT(*) FROM csv_import_rows WHERE source_id=@source AND version_id=@version AND phase=@phase",p));
            var canonical=await tx.CommandWithResponse<long>(new QueryObject("""
SELECT COUNT(*) FROM csv_import_rows r JOIN user_source_accounts a ON a.user_id=r.entity_id::uuid
 AND a.company_id=r.company_id AND a.source_person_id=r.person_id
JOIN user_source_account_revisions v ON v.user_id=a.user_id
 AND v.revision_id='csv-'||@manifestHash||'-'||@phase||'-'||r.row_number::text
WHERE r.source_id=@source AND r.version_id=@version AND r.phase=@phase
""",p));
            if(count!=canonical || count!=manifest.RootElement.GetProperty("files").GetProperty(b.Phase).GetProperty("rows").GetInt64() || await tx.CommandWithResponse<long>(new QueryObject(CsvSql.Next,p))!=count+1) return Fail("canonical_row_verification_failed");
            var receipt=CsvProtocol.Serialize(new {verified=true,b.SourceId,b.VersionId,b.ManifestHash,b.Phase,rows=count,skipped=0,conflicts=0});
            await tx.Command(new QueryObject(CsvSql.MarkVerified,P(b,"",receipt)));
            tx.Commit(); return Content(receipt,"application/json");
        }
        catch(Exception) {return Fail("verification_not_confirmed",503);}
    }
}
