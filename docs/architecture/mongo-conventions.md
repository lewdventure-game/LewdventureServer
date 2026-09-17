[← Архитектура](README.md)

# Соглашения MongoDB

## Топология

- MongoDB 8, всегда replica set `rs0`, даже из одного узла: без него нет транзакций. `MongoTopologyValidator` при старте проверяет имя replica set, `Mongo:RequireReplicaSet=true` запрещает standalone.
- В compose: сервис `mongo` с keyfile (генерируется в `deploy/mongo/mongod-entrypoint.sh`), одноразовый `mongo-init` выполняет `deploy/mongo/init-replica.js`: `rs.initiate`, ожидание primary, создание пользователя приложения.
- Одна база на окружение: `lewdventure_local`, `lewdventure_dev`, `lewdventure_stage`, `lewdventure_prod`. Пользователь приложения имеет `readWrite` только на свою базу, `authSource` = эта база.
- Порт Mongo на VPS наружу не публикуется, доступ только из сети compose `backend`.

## Доступ из кода

| Тип | Назначение |
| --- | --- |
| `MongoClientFactory` | единственный `MongoClient` на процесс, `ApplicationName`, `ServerSelectionTimeout` |
| `IMongoDatabaseAccessor` | `GetCollection<T>(name)` для базы окружения |
| `MongoCollectionNames` | все имена коллекций в одном месте |
| `IMongoIndexContributor` | репозиторий объявляет свои индексы |
| `MongoIndexBootstrapper` | применяет индексы при старте (`Mongo:ApplyIndexesOnStartup`) |
| `IMongoTransactionRunner` | сессия и `WithTransactionAsync` (snapshot read, majority write, встроенный повтор транзиентных ошибок) |
| `MongoHealthCheck` | `ping` для `/health/ready` |
| `MongoConventionsRegistrar` | конвенции только для типов `Server.*` |

Репозитории — `internal sealed`, по одному на коллекцию, живут рядом с доменом (`Mongo/ConfigSnapshots/`). Сервисы не ходят в `IMongoCollection` напрямую.

## Документы

- Каждый документ реализует `IMongoDocument`: `Id` (строка), `SchemaVersion`, `CreatedAt`, `UpdatedAt` (UTC).
- `SchemaVersion` — константа `CurrentSchemaVersion` в типе документа. Меняем структуру несовместимо → поднимаем версию и пишем чтение старой версии или миграцию.
- Имена полей camelCase, enum'ы строками, лишние поля при чтении игнорируются (конвенции `lewdventure`).
- Время всегда `DateTime` UTC.
- Идентификаторы осмысленные, где возможно: версия снапшота `sha256:<hex>`, состояние `active`.

## Коллекции

| Коллекция | Id | Содержимое | Индексы |
| --- | --- | --- | --- |
| `config_snapshots` | `sha256:<hex>` | формат, источник, автор, время снапшота, домены со строками | по `createdAt` убыв. |
| `config_state` | `active` | активная версия и id активации | — |
| `config_activations` | uuid | версия, предыдущая версия, кто, причина, время | по `createdAt` убыв. |

Снапшоты неизменяемы: повторная публикация той же версии ничего не пишет. Активация — одна транзакция: запись в `config_activations` + обновление `config_state`.

## Интеграционные тесты

- Включаются только `LEWD_IT_ENABLED=1`, иначе пропускаются.
- По умолчанию поднимают Mongo через Testcontainers; `LEWD_IT_MONGO` задаёт внешний connection string.
- `IntegrationTestGuard` разрешает только локальные хосты и базы с префиксом `lewdventure_it_`, каждая фикстура создаёт свою базу и удаляет её в конце.

## Бэкапы

Обязательны до появления пользовательских данных. Рекомендуемая схема для каждого окружения:

```bash
docker compose -p lewdventure-prod exec -T mongo mongodump --archive --gzip \
  --username root --password "$MONGO_ROOT_PASSWORD" --authenticationDatabase admin \
  --db lewdventure_prod > backup-$(date -u +%Y%m%dT%H%M%SZ).archive.gz
```

Запуск по cron на VPS, копия архива за пределы VPS (объектное хранилище), периодическая проверка восстановления через `mongorestore --archive --gzip` в отдельный проект compose.
