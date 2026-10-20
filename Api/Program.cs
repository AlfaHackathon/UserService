using System.Reflection;
using AlfaHackaton.Controllers;
using AlfaHackaton.DapperContext;
using Dapper;
using Api.Common;
using Api.Repositories;
using Api.Repositories.Interfaces;
using Api.Services.Interfaces;
using Api.Settings;

var builder = WebApplication.CreateBuilder(args);

var dapperSettings = new DapperSettings(
    builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Не задана строка подключения к Postgres (ConnectionStrings:Postgres)"));

// DapperContext (запросы к БД) и миграции FluentMigrator
builder.Services.AddDapper(dapperSettings);
builder.Services.AddMigrations(dapperSettings, Assembly.GetExecutingAssembly());
builder.Services.AddSingleton<IUserRepository, UserRepository>();
// Слой бизнес-логики: контроллеры работают только с сервисом
builder.Services.AddSingleton<IUserService, Api.Services.UserService>();

// Контроллеры. Токен уже провалидирован auth-gateway, поэтому без повторной валидации.
// Глобальный префикс user-service для всех контроллеров
builder.Services.AddControllers(options =>
{
    options.Conventions.Add(new RoutePrefixConvention("user-service"));
});
builder.Services.AddAuthenticationWithoutValidation();

// Swagger с авторизацией через Bearer-токен
builder.Services.AddSwaggerAuth();

var app = builder.Build();

// Применяем миграции при старте сервиса
app.Services.UseMigrations();

// Сопоставление snake_case-колонок со свойствами модели (created_at -> CreatedAt)
DefaultTypeMap.MatchNamesWithUnderscores = true;

// Swagger: доступен на /user-service/swagger, включается флагом Swagger:Enabled (из .env)
if (app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger(options => options.RouteTemplate = "user-service/swagger/{documentName}/swagger.json");
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/user-service/swagger/v1/swagger.json", "User Service API");
        options.RoutePrefix = "user-service/swagger";
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
