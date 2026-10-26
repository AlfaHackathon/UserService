namespace Api.CsvImport;

public static class CsvSql
{
    public const string Schema = """
CREATE TABLE csv_import_versions(source_id text NOT NULL, version_id text NOT NULL, manifest_hash text NOT NULL,
 manifest_json text NOT NULL, PRIMARY KEY(source_id,version_id));
CREATE TABLE csv_import_batches(source_id text NOT NULL, version_id text NOT NULL, phase text NOT NULL,
 start_row bigint NOT NULL, end_row bigint NOT NULL, request_hash text NOT NULL, receipt_json text NOT NULL,
 PRIMARY KEY(source_id,version_id,phase,start_row),
 FOREIGN KEY(source_id,version_id) REFERENCES csv_import_versions(source_id,version_id));
CREATE TABLE csv_import_rows(source_id text NOT NULL, version_id text NOT NULL, phase text NOT NULL,
 row_number bigint NOT NULL, entity_id text NOT NULL, company_id uuid, person_id uuid, group_id uuid,
 known_ticks bigint NOT NULL, raw_json jsonb NOT NULL, row_hash text NOT NULL, material_ref jsonb NOT NULL,
 PRIMARY KEY(source_id,version_id,phase,row_number),
 FOREIGN KEY(source_id,version_id) REFERENCES csv_import_versions(source_id,version_id));
CREATE INDEX ix_csv_import_entity ON csv_import_rows(source_id,version_id,phase,entity_id);
CREATE TABLE csv_import_verified(source_id text NOT NULL, version_id text NOT NULL, phase text NOT NULL,
 manifest_hash text NOT NULL, receipt_json text NOT NULL, PRIMARY KEY(source_id,version_id,phase),
 FOREIGN KEY(source_id,version_id) REFERENCES csv_import_versions(source_id,version_id));
""";
    public const string Replay = "SELECT COALESCE((SELECT receipt_json FROM csv_import_batches WHERE source_id=@source AND version_id=@version AND phase=@phase AND start_row=@start),'')";
    public const string Bind = """
INSERT INTO csv_import_versions VALUES(@source,@version,@manifestHash,@manifestJson) ON CONFLICT DO NOTHING;
""";
    public const string Bound = "SELECT manifest_hash=@manifestHash AND manifest_json=@manifestJson FROM csv_import_versions WHERE source_id=@source AND version_id=@version";
    public const string Next = "SELECT COALESCE(MAX(end_row),0)+1 FROM csv_import_batches WHERE source_id=@source AND version_id=@version AND phase=@phase";
    public const string Checkpoint = "INSERT INTO csv_import_batches VALUES(@source,@version,@phase,@start,@end,@requestHash,@receipt)";

    public const string InsertRaw = """
INSERT INTO csv_import_rows
SELECT @source,@version,@phase,(x->>'number')::bigint,x->>'userId',(x->>'companyId')::uuid,
 (x->>'personId')::uuid,NULL,(x->>'ticks')::bigint,(x->>'raw')::jsonb,x->>'hash','{}'::jsonb
FROM jsonb_array_elements(CAST(@prepared AS jsonb)) x;
""";
    public const string InsertAccounts = """
INSERT INTO user_source_accounts(user_id,source_person_id,company_id)
SELECT DISTINCT (x->>'userId')::uuid,(x->>'personId')::uuid,(x->>'companyId')::uuid
FROM jsonb_array_elements(CAST(@prepared AS jsonb)) x ON CONFLICT DO NOTHING;
INSERT INTO user_source_account_revisions(user_id,revision_id,known_at_ticks,snapshot_json,request_hash)
SELECT (x->>'userId')::uuid,x->>'revision',(x->>'ticks')::bigint,x->>'profile',x->>'requestHash'
FROM jsonb_array_elements(CAST(@prepared AS jsonb)) x;
""";
    public const string Conflict = """
SELECT EXISTS(SELECT 1 FROM jsonb_array_elements(CAST(@prepared AS jsonb)) x WHERE
 EXISTS(SELECT 1 FROM users u WHERE u.id=(x->>'userId')::uuid) OR
 (@phase='accounts' AND EXISTS(SELECT 1 FROM user_source_account_revisions r WHERE r.user_id=(x->>'userId')::uuid)) OR
 EXISTS(SELECT 1 FROM user_source_accounts a WHERE
 (a.user_id=(x->>'userId')::uuid AND (a.source_person_id<>(x->>'personId')::uuid OR a.company_id<>(x->>'companyId')::uuid))
 OR (a.source_person_id=(x->>'personId')::uuid AND a.company_id=(x->>'companyId')::uuid AND a.user_id<>(x->>'userId')::uuid)) OR
 EXISTS(SELECT 1 FROM user_source_account_revisions r WHERE r.user_id=(x->>'userId')::uuid AND r.known_at_ticks>=(x->>'ticks')::bigint));
""";
    public const string PairMap = """
SELECT COALESCE(jsonb_agg(jsonb_build_object('userId',user_id,'personId',source_person_id,'companyId',company_id)),'[]'::jsonb)::text
FROM user_source_accounts WHERE (source_person_id,company_id) IN
 (SELECT (x->>'personId')::uuid,(x->>'companyId')::uuid FROM jsonb_array_elements(CAST(@pairs AS jsonb)) x);
""";
    public const string Verified = "SELECT COALESCE((SELECT receipt_json FROM csv_import_verified WHERE source_id=@source AND version_id=@version AND phase=@phase AND manifest_hash=@manifestHash),'')";
    public const string MarkVerified = "INSERT INTO csv_import_verified VALUES(@source,@version,@phase,@manifestHash,@receipt) ON CONFLICT DO NOTHING";
}
