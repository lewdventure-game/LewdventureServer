[← Runbooks](README.md)

# Локальная разработка

## Что нужно

- .NET SDK 10.0.401 (`global.json`, rollForward `latestFeature`).
- Docker Desktop (compose, интеграционные тесты).

## Вариант 1. Весь стек в Docker

Для игры из Unity против локального сервера: поднять стек командой ниже и выбрать в Unity `RunMode / Server / Local`. Ключ Google не нужен, конфиги берутся из снимка в репозитории. Обычно же клиент играет против dev на VPS (`RunMode / Server / Dev`).

Так же, как на VPS: api + Mongo replica set + mongo-init. Конфиги при пустой базе берутся из фикстуры репозитория.

```bash
docker compose -f deploy/compose/compose.yaml -f deploy/compose/compose.local.yaml up -d --build --wait
curl -s http://127.0.0.1:9090/health/ready
```

| Что | Адрес |
| --- | --- |
| API для Unity-клиента | `http://localhost:5000` |
| Swagger | `http://localhost:5000/swagger` |
| Ops | `http://127.0.0.1:9090/health`, admin-ключ `local-admin-key` |
| Mongo | `mongodb://lewdventure_app:local-app-password@127.0.0.1:27017/?replicaSet=rs0&authSource=lewdventure_local&directConnection=true` |

ConfigTool в том же стеке:

```bash
docker compose -f deploy/compose/compose.yaml -f deploy/compose/compose.local.yaml run --rm config-tool list
```

Остановить с удалением данных: `docker compose -f deploy/compose/compose.yaml -f deploy/compose/compose.local.yaml down -v`.

## Вариант 2. dotnet run без Mongo

На замороженной фикстуре, без Google и Docker:

```bash
export ASPNETCORE_ENVIRONMENT=Local
export GameConfig__Source=File
export GameConfig__FilePath="$(pwd)/tests/Lewdventure.Server.GoldenTests/Golden/Fixtures/config-snapshot.v1.json"
dotnet run -c Release --project src/Lewdventure.Server.Api
```


PowerShell: `$env:ASPNETCORE_ENVIRONMENT = "Local"` и т.д.

## Проверки перед коммитом

```bash
dotnet build LewdventureServer.slnx -c Release
dotnet test LewdventureServer.slnx -c Release
LEWD_IT_ENABLED=1 dotnet test tests/Lewdventure.Server.IntegrationTests -c Release
dotnet format whitespace LewdventureServer.slnx --verify-no-changes
```

Пакеты — через `Directory.Packages.props`; после добавления или обновления пакета выполнить `dotnet restore LewdventureServer.slnx --force-evaluate` и закоммитить изменившиеся `packages.lock.json` (CI восстанавливает в `--locked-mode`).

## Полезное

- Подробный лог боя: `Logging__LogLevel__Server.Battles=Information` (в Local включено).
- Снять свежий снапшот из таблиц в файл: `dotnet run --project tools/Lewdventure.Server.ConfigTool -- import --out snapshot.json`.
- Сравнить с фикстурой: `dotnet run --project tools/Lewdventure.Server.ConfigTool -- diff --from tests/Lewdventure.Server.GoldenTests/Golden/Fixtures/config-snapshot.v1.json --to snapshot.json`.
- Бенчмарки: `dotnet run -c Release --project tests/Lewdventure.Server.Benchmarks`.
- В Git Bash на Windows для путей в `docker run -v` нужен `MSYS_NO_PATHCONV=1`.
