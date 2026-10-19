using FluentMigrator;

namespace UserService.Migrations;

/// <summary>
/// Создаёт таблицу users и наполняет её тестовыми пользователями.
/// Пароли в сидах: qwerty123, password123, 12345678 (bcrypt).
/// </summary>
[Migration(20261019001)]
public class AddUsersTable : Migration
{
    public override void Up()
    {
        Create.Table("users")
            .WithColumn("id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("last_name").AsString(100).NotNullable()
            .WithColumn("first_name").AsString(100).NotNullable()
            .WithColumn("middle_name").AsString(100).Nullable()
            .WithColumn("email").AsString(320).NotNullable().Unique()
            .WithColumn("hashed_password").AsString(200).NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime);

        Insert.IntoTable("users")
            .Row(new
            {
                id = Guid.Parse("3f2b7c1a-9e4d-4c1b-8a77-2f6d0c5b1e90"),
                last_name = "Иванов",
                first_name = "Иван",
                middle_name = "Иванович",
                email = "ivan.ivanov@example.com",
                hashed_password = "$2a$11$1SDXRWg/WgvVoOj2GFdm/u0yK/jCpEZ/ItpVew5zXFYdVdW915Fg6" // qwerty123
            })
            .Row(new
            {
                id = Guid.Parse("8d4a2f6e-1c3b-4e5f-9a0b-7c6d5e4f3a2b"),
                last_name = "Петрова",
                first_name = "Мария",
                middle_name = "Сергеевна",
                email = "maria.petrova@example.com",
                hashed_password = "$2a$11$CLUkCXmwUvMNFdNOgp31Z.zgr0/WGO1wQmttR.xR.lftdpjiimw0u" // password123
            })
            .Row(new
            {
                id = Guid.Parse("b1c2d3e4-f5a6-4b7c-8d9e-0a1b2c3d4e5f"),
                last_name = "Сидоров",
                first_name = "Пётр",
                middle_name = (string?)null,
                email = "petr.sidorov@example.com",
                hashed_password = "$2a$11$6CtN7xMKq3Stqr1KOpWj1ej5shhTlqYKjbnWtorIItBRXUMkCRX0K" // 12345678
            });
    }

    public override void Down()
    {
        Delete.Table("users");
    }
}
