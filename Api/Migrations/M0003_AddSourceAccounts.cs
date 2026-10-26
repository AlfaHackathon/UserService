using Api.Resources;
using FluentMigrator;

namespace Api.Migrations;

[Migration(20261027001)]
public sealed class M0003_AddSourceAccounts : Migration
{
    public override void Up() => Execute.Sql(SourceAccountSql.Schema);
    public override void Down() => Execute.Sql(SourceAccountSql.Drop);
}
