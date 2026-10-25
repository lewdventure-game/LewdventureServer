# Refresh 401: почему сыпался и что изменено

Дата: 25 октября 2026. Два репозитория: клиент `Lewdventure` и сервер `LewdventureServer`.

## Симптом

В консоли Unity красная ошибка:

```text
[Server]: POST /api/auth/refresh failed, status = Unauthorized, error = Refresh token is not valid.
```

В админке по одному и тому же аккаунту `usr_e531f4d8b9984f389cfba2d81fccbbaa` пачка предупреждений:

```text
[Auth] refresh token rejected userId = usr_e531… reason = unknown
```

`reason = unknown` — присланный refresh не текущий и не предыдущий. Игра после этого не останавливалась: в `Editor.log` сразу следом `Device login ok`.

## Причина

Refresh одноразовый. Сервер хранит у устройства только текущий хеш и один предыдущий.

Каждый Play вызывал `POST /api/auth/refresh`, даже когда access ещё действовал. Отказ писался через `LogError` до того, как `LoginState` входил по устройству. Вход по устройству снова крутил слот.

По `Editor.log` за 25 октября:

| Время UTC | Что сделал этот редактор |
| --- | --- |
| 10:46 | Refresh прошёл, новый токен записан в `auth-session.json`. Access жив до 11:46 |
| 11:12 | Play отправил тот же токен. Сервер ответил `unknown`. Между этими Play редактор auth не трогал |
| 09:42 | Отказ есть в админке и нет в `Editor.log` этого редактора |

Токен выбил другой клиент с тем же device id. Этот id закоммичен в `Assets/Configs/Core/Login/LoginConfig.asset`, поэтому любой клон редактора — один слот. Сборка, которая прочитала редакторский `auth-session.json`, сидит на том же id.

Сервер менял устройство двумя шагами: удалить запись, потом вставить. `previous` брался из чтения до записи. При двух входах сразу выкинутый токен не оставался предыдущим, следующий Play получал `unknown`, а не `reused`.

## Клиент

| Файл | Изменение |
| --- | --- |
| `Assets/Scripts/Core/EntryPoints/States/LoginState.cs` | Если до `accessExpiresAt` больше 15 секунд, старт не вызывает refresh. В логе `Login, type = access`. Access протух или срока нет — как раньше: `Refresh`, на `401` один `LoginDevice` |
| `Assets/Scripts/Core/Servers/Services/ApiTransport.cs` | `401` на `POST /api/auth/refresh` пишется обычным логом, не `LogError`. Текст тот же. Остальные ошибки транспорта по-прежнему `LogError` |
| `docs/login-config.md` | Старт без refresh, пока access жив. Общий device id у редактора и сборки, которая прочитала его `auth-session.json` |
| `docs/app-startup.md` | Цепочка `LoginState`: `access` / `refresh` / `device` |
| `docs/troubleshooting.md` | Что значат `reused` и `unknown`, почему красной строки больше нет |
| `docs/api.md` | Предыдущий refresh жив ровно один обмен. Клиент на старте не обновляет сессию, пока `accessExpiresAt` впереди |

`401` на протухший access по ходу игры не изменился: один общий `Refresh`, при отказе один `LoginDevice`.

## Сервер

| Файл | Изменение |
| --- | --- |
| `src/Lewdventure.Server.Infrastructure/Players/PlayerAuthService.cs` | Обмен refresh идёт через `RotateRefreshAsync`. Новый хеш пишется только если присланный ещё текущий. Проигравший запрос получает `401` и не затирает победителя. Если запись устройства не легла, клиенту не отдаётся токен, которого нет в базе |
| `src/Lewdventure.Server.Infrastructure/Mongo/Players/UserRepository.cs` | `TryReplaceRefreshAsync` и `UpsertDeviceAsync` в транзакции. Вход по устройству кладёт в `previous` хеш, который реально заменил, а не тот, что прочитали до гонки |
| `tests/Lewdventure.Server.IntegrationTests/Mongo/PlayerStateTests.cs` | Два параллельных refresh одного токена: один победитель, предыдущий слот — старый токен. Два параллельных входа одного device id: текущий и предыдущий — эти два токена |
| `docs/server/client-api.md` | Предыдущий токен жив один обмен. Параллельный повтор не гасит токен победителя |
| `docs/server/qa-tools.md` | Когда в админке `reused` и когда `unknown` |

Сборка `Lewdventure.Server.IntegrationTests` проходит. Прогон новых тестов не запускался: Docker не поднят, Testcontainers не достучался до демона.

## После выкладки

Клиентский кусок виден на следующем Play. Серверный — только после деплоя `LewdventureServer`. Пока на dev старый сервер, гонка всё ещё может сделать токен `unknown`. Повторный Play в пределах часа access больше не красит консоль.

Две копии на закоммиченном device id по-прежнему делят один слот. Свой аккаунт: в `LoginConfig` другой id (**Random device Id**) и Play. Прогресс со старого id не переедет.
