[← Runbooks](README.md)

# Публикация конфигов

Для геймдизайнера и владельца. Как изменения в Google-таблицах попадают на dev, stage и prod.

## Коротко

| Окружение | Как публиковать | Кто |
| --- | --- | --- |
| dev | меню «Lewdventure» в таблице → «Опубликовать конфиги на dev» | геймдизайнер |
| stage | меню → «Опубликовать конфиги на stage» | геймдизайнер |
| prod | GitHub Actions → `config-promote` | владелец, с одобрением |

prod никогда не читает Google Sheets: туда попадает ровно та версия, что проверена на stage.

## dev и stage из таблицы

1. Внести изменения в листы.
2. В таблице-«пульте» открыть меню «Lewdventure» → «Опубликовать конфиги на dev».
3. Сервер сам скачивает все листы, валидирует, сохраняет и активирует. В диалоге будет:
   - версия `cfg-xxxxxxxxxxxx`;
   - `stored` / `activated` — сохранилась ли новая версия и стала ли активной;
   - предупреждения валидатора (например, пропущенные колонки);
   - ошибки (версия не активируется, работает прежняя);
   - изменения по листам: добавленные, удалённые и изменённые id.
4. Если таблицы не менялись, версия будет той же и ничего не произойдёт.
5. Сервер dev/stage подхватывает новую версию сразу после публикации, другие экземпляры — в течение `PollIntervalSeconds` (30 с).

«Статус dev» / «Статус stage» в меню показывают активную версию.

Лимит: 6 публикаций в минуту на окружение.

## prod через GitHub Actions

1. Убедиться, что на stage активна нужная версия и она проверена (меню «Статус stage»).
2. GitHub → Actions → `config-promote` → Run workflow:
   - `version`: `active` (текущая на stage) или полная версия `sha256:…`;
   - `reason`: зачем, попадёт в историю активаций и Discord.
3. Job `export` выгружает снапшот со stage, проверяет формат и версию, сохраняет артефакт `config-snapshot` на 30 дней.
4. Job `publish` ждёт одобрения в окружении `prod`. Одобривший видит версию и список листов в summary.
5. После одобрения снапшот публикуется и активируется на prod, api перечитывает конфиги (`/admin/config/reload`), workflow сверяет активную версию.

## Откат конфигов

Все опубликованные версии хранятся. Вернуть прошлую:

```bash
cd /opt/lewdventure/<env>
scripts/config-transfer.sh <env> status
docker compose --env-file compose/.env -p lewdventure-<env> --project-directory compose \
  -f compose/compose.yaml -f compose/compose.vps.yaml -f compose/compose.<env>.yaml \
  --profile tools run --rm config-tool list
```

Активировать нужную версию через admin API на ops-порту (ключ `Admin__ApiKey` из `.env`):

```bash
curl -s -X POST -H "X-Admin-Key: $ADMIN_KEY" -H "Content-Type: application/json" \
  -d '{"version":"sha256:…","reason":"откат после ошибки баланса"}' \
  http://127.0.0.1:<OPS_HOST_PORT>/admin/config/activate
```

На dev/stage можно также просто откатить изменения в таблице и опубликовать заново — получится прежняя версия.

Аварийно закрепить версию независимо от активной: `GameConfig__PinnedVersion=sha256:…` в `.env` и передеплой текущего тега.

## Настройка Apps Script

Один раз, делает владелец:

1. Таблица-«пульт» → Extensions → Apps Script, вставить `tools/apps-script/ConfigPublisher.gs` и манифест `tools/apps-script/appsscript.json`.
2. Project Settings → Script Properties:

| Свойство | Значение |
| --- | --- |
| `LEWDVENTURE_DEV_URL` | `https://api-dev.example.com` |
| `LEWDVENTURE_DEV_KEY` | `ConfigPublisher__ApiKey` dev |
| `LEWDVENTURE_STAGE_URL` | `https://api-stage.example.com` |
| `LEWDVENTURE_STAGE_KEY` | `ConfigPublisher__ApiKey` stage |

3. Перезагрузить таблицу, разрешить скрипту доступ (внешние запросы).
4. Выдать геймдизайнеру права редактора таблицы. Ключи видны только редакторам проекта Apps Script.

## Частые ошибки

| Сообщение | Причина |
| --- | --- |
| `401` | неверный или устаревший ключ в Script Properties |
| `404` на `/api/config/publish` | публикация выключена в окружении или неверный URL (prod не поддерживает) |
| `429` | больше 6 публикаций в минуту |
| ошибки валидатора | структура листа сломана: нет обязательной колонки, неверный тип; версия не активирована |
| `Google credentials file not found` | на VPS нет `compose/secrets/google-credentials.json` |
