# UserService

Сервис пользователей кластера AlfaHackathon: .NET 10, PostgreSQL + [Dapper](https://github.com/DapperLib/Dapper) через пакет `AlfaHackaton.DapperContext`, миграции на FluentMigrator. Контроллеры наследуются от `BaseAuthController` (пакет `AlfaHackaton.Controllers`) — все публичные ручки требуют валидированный auth-gateway токен (`X-User-Id` / `X-User-Role`).

## Ручки

| Метод | Путь | Описание |
|---|---|---|
| GET | `/user-service/users/me` | профиль текущего пользователя |
| GET | `/user-service/users` | пользователи своей компании; без компании — пустой список |
| GET | `/user-service/users/{id:guid}` | пользователь по id; 403 — если он из другой компании |
| GET | `/user-service/ping` | health-check |
| GET | `/user-service/internal/users/by-email/{email}` | пользователь по email с хэшем (только для auth-service) |
| GET | `/user-service/internal/users/exists-by-email/{email}` | `{ "exists": bool }` (только для auth-service) |
| POST | `/user-service/internal/users` | создать пользователя (только для auth-service) |

Internal-ручки закрыты заголовком **`X-Internal-Secret`** (общий секрет кластера) — там отдаётся хэш пароля.

## Миграции

Одна миграция `20261019001` (FluentMigrator, Postgres): таблица `users` — `id uuid PK`, `last_name`, `first_name`, `middle_name`, `email unique`, `hashed_password`, `created_at`. Применяются при старте сервиса (`UseMigrations()`).

## Свагер / конфигурация

- Swagger UI: `/user-service/swagger` (включается флагом `Swagger__Enabled`).
- Строка подключения: `ConnectionStrings__Postgres`.
- Запуск — из папки `../cluster`: `docker compose up -d --build`.
