using FluentMigrator;
namespace Api.CsvImport;
[Migration(20261027002)]
public sealed class CsvMigration : Migration
{
    public override void Up() => Execute.Sql(CsvSql.Schema);
    public override void Down() => throw new NotSupportedException("Source lineage is durable; explicit preservation is required.");
}
