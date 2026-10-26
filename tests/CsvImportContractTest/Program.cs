using Api.CsvImport;
var manifest=CsvProtocol.Serialize(new {format=1,source_id="synthetic",version="v1",files=new {accounts=new {sha256=new string('a',64),headers=new[]{"ACCOUNT_ID","PERSON_ID","COMPANY_ID","ROLE"},rows=1}}});
var batch=new CsvBatch("synthetic","v1",CsvProtocol.Hash(manifest),manifest,"accounts",1,
    [new(1,["11111111-1111-4111-8111-111111111111","22222222-2222-4222-8222-222222222222","33333333-3333-4333-8333-333333333333",""])]);
using var valid=CsvProtocol.Validate(batch,["accounts"]);
if(CsvProtocol.Fields(valid.RootElement,batch,batch.Rows[0])["ROLE"]!="") throw new Exception("raw ROLE changed");
foreach(var invalid in new[]{batch with {ManifestHash=new string('b',64)},batch with {StartRow=2},batch with {Rows=[new(1,["private"])]}})
{
    try { using var unexpected=CsvProtocol.Validate(invalid,["accounts"]); throw new Exception("invalid admission accepted"); }
    catch(InvalidDataException) { }
}
Console.WriteLine("CSV source contract fixtures passed; no auth or database was used.");
