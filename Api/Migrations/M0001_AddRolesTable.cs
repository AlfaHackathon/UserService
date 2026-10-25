using FluentMigrator;

namespace Api.Migrations;

/// <summary>
/// Роли (в user-service, потому что роль — свойство пользователя):
/// - is_default=true, company_id=NULL — дефолтная роль, одна общая на все компании (сид «Ген директор»);
/// - is_default=false, company_id обязательный — кастомная роль конкретной компании.
/// company_id — ссылка на компании company-service без FK (разные БД), изоляцию по компаниям
/// держит сервис. У пользователя появляется role_id → roles(id), после регистрации = NULL.
/// </summary>
[Migration(20261025001)]
public class M0001_AddRolesTable : Migration
{
    public override void Up()
    {
        Create.Table("roles")
            .WithColumn("id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("name").AsString(128).NotNullable()
            .WithColumn("is_default").AsBoolean().NotNullable().WithDefaultValue(false)
            // без FK: таблица companies лежит в БД company-service, контролируем на уровне сервиса
            .WithColumn("company_id").AsGuid().Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime);

        // is_default=true → company_id NULL (общая роль); is_default=false → company_id обязателен
        Execute.Sql("ALTER TABLE roles ADD CONSTRAINT ck_roles_default_company " +
                    "CHECK ((is_default AND company_id IS NULL) OR (NOT is_default AND company_id IS NOT NULL))");

        // имя кастомной роли уникально внутри компании; имя дефолтной роли уникально глобально
        Execute.Sql("CREATE UNIQUE INDEX ux_roles_company_name ON roles (company_id, name) WHERE company_id IS NOT NULL");
        Execute.Sql("CREATE UNIQUE INDEX ux_roles_default_name ON roles (name) WHERE is_default");
        Execute.Sql("CREATE INDEX ix_roles_company ON roles (company_id)");

        // 1 пользователь — 1 роль: поле role_id на users, NULL до назначения (после регистрации)
        Alter.Table("users").AddColumn("role_id")
            .AsGuid().Nullable().ForeignKey("roles", "id").OnDelete(System.Data.Rule.SetNull);
        Execute.Sql("CREATE INDEX ix_users_role ON users (role_id)");

        SeedDefaults();
    }

    public override void Down()
    {
        Execute.Sql("ALTER TABLE users DROP COLUMN role_id");
        Delete.Table("roles");
    }

    /// <summary>Сид дефолтных ролей: одна общая «Ген директор» на все компании.</summary>
    private void SeedDefaults()
    {
        Insert.IntoTable("roles").Row(new
        {
            id = Guid.NewGuid(),
            name = "Ген директор",
            is_default = true,
            company_id = (Guid?)null,
            created_at = DateTime.UtcNow
        });
    }
}
