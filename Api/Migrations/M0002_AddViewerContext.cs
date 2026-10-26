using FluentMigrator;

namespace Api.Migrations;

[Migration(20261026001)]
public sealed class M0002_AddViewerContext : Migration
{
    public override void Up()
    {
        Alter.Table("users").AddColumn("source_person_id").AsGuid().Nullable();
        Alter.Table("users").AddColumn("city_name").AsString(256).Nullable();
        Alter.Table("users").AddColumn("rc_name").AsString(256).Nullable();
        Create.Table("user_context_revisions")
            .WithColumn("user_id").AsGuid().NotNullable().ForeignKey("users", "id")
            .WithColumn("company_id").AsGuid().NotNullable()
            .WithColumn("revision_id").AsString(200).NotNullable()
            .WithColumn("known_at_ticks").AsInt64().NotNullable()
            .WithColumn("snapshot_json").AsString(int.MaxValue).NotNullable()
            .WithColumn("request_hash").AsString(64).NotNullable();
        Create.PrimaryKey("pk_user_context_revisions").OnTable("user_context_revisions").Columns("user_id", "company_id", "revision_id");
        Create.UniqueConstraint("uq_user_context_known_at").OnTable("user_context_revisions").Columns("user_id", "company_id", "known_at_ticks");
    }

    public override void Down()
    {
        Delete.Table("user_context_revisions");
        Delete.Column("source_person_id").FromTable("users");
        Delete.Column("city_name").FromTable("users");
        Delete.Column("rc_name").FromTable("users");
    }
}
