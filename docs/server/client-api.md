# API для клиента: ручки, запросы, ответы

Снимок на 1 октября 2026. Все поля JSON — `camelCase`, кроме бандла конфигов (`GET /api/config/bundle`): он отдаётся в `PascalCase`, потому что его формат задаёт ядро боя. Числа с плавающей точкой приходят в той форме, в которой их печатает рантайм сервера; клиент разбирает их обычным парсером, сравнивать строками нельзя.

## Общее

- База: `https://api-dev.lewdventure.online` (dev), локально `http://localhost:5000`. Порты `5001` и `9091` на VPS слушают только loopback, снаружи трафик идёт через Caddy по HTTPS.
- Авторизация игрока: заголовок `Authorization: Bearer <accessToken>`, схема JWT.
- Ручки игрока и забега существуют только при включённой авторизации (`Auth:Enabled`). На dev она включена с 27 сентября 2026, локально включена в `compose.local.yaml`. Если авторизация выключена, ручки отвечают `404`.
- Ошибки: `{"error": "текст"}` или `{"errors": ["текст", ...]}`.
- Коды: `400` — некорректный запрос или отказ правил, `401` — нет или истёк токен, `404` — нет активного забега, `409` — конфликт версии профиля или забега (повторить запрос), `503` — конфиги не загружены.
- Идемпотентность: в мутирующих запросах есть `requestId`. Повтор с тем же `requestId` не применит операцию дважды. Генерировать на клиенте (guid), хранить до получения ответа.
- Версионность состояния: профиль отдаёт `rev`. При конфликте (`409`) нужно перечитать профиль и повторить.
- Сроки токенов: access — 60 минут, refresh — 90 дней (настраивается на сервере). Повтор по `requestId` защищает 48 часов.
- Версия протокола боя: сервер отдаёт `protocolVersion` в скрипте. Не совпало с версией клиента — не играть бой, а показать требование обновиться.
- Версия конфигов: забег пинит `configVersion` при старте, конфиги для ядра боя клиент берёт с сервера через `GET /api/config/bundle`. Если `IBattleCore.ConfigVersion` не совпал с `run.configVersion` — перекачать бандл и пересобрать ядро, бой до этого не играть; подменять конфиги посреди забега нельзя. Подробно — раздел «Конфиги: снапшот с сервера».

## Авторизация

### `POST /api/auth/device`

Анонимный вход по устройству. Создаёт аккаунт при первом обращении.

```json
{ "deviceId": "3f9c…", "clientVersion": "0.4.1" }
```

Ответ:

```json
{
  "userId": "usr_…",
  "accessToken": "eyJ…",
  "accessExpiresAt": "2026-09-27T12:00:00Z",
  "refreshToken": "rt_…",
  "refreshExpiresAt": "2026-10-27T12:00:00Z"
}
```

### `POST /api/auth/refresh`

```json
{ "userId": "usr_…", "refreshToken": "rt_…" }
```

Ответ — такой же, как у `device`. Refresh-токен одноразовый: старый становится недействительным.

## Профиль и прокачка

### `GET /api/player/profile`

```json
{
  "userId": "usr_…",
  "rev": 12,
  "resources": { "soft_money": 1500, "summon_exp": 40 },
  "characters": [ { "id": 1, "copies": 3, "upgradesApplied": 2, "unlockedScenes": [101, 102] } ],
  "summons": [ { "id": 1, "copies": 4, "level": 3, "masteryLevel": 1 } ],
  "equipment": [ { "instanceId": "eq_…", "configId": 5, "level": 2, "mergeNumber": 0 } ],
  "bonuses": [ { "id": 1, "count": 2 } ],
  "loadout": { "characterId": 1, "equipment": { "weapon": "eq_…" }, "summons": [1, 2] },
  "story": { "completedLevelIds": [1], "currentRunId": "run_…" },
  "flags": { "tutorial_done": 1 }
}
```

`bonuses` — постоянные бонусы аккаунта (`permanent` / `if_equipped` из листа `Bonuses`), выданные наградами; в бой они уезжают сами, клиенту нужны только для показа.

`loadout.equipment` — словарь «слот → `instanceId`»; имена слотов задаёт клиент и конфиг экипировки. Сервер проверяет: персонаж открыт и есть в конфигах, не более трёх саммонов, саммоны без повторов и в собственности, экипировка в собственности и не занимает два слота одновременно. Нарушение — `400` с текстом причины.

Новый профиль создаётся пустым и получает стартовый набор из константы `start_content` (синтаксис наград `тип:id:количество` через запятую, например `character:1:1,resource:soft_money:500`). Если константы в конфигах нет, профиль останется без персонажа, и `POST /api/run/start` ответит `400` с `Loadout has no character.`

### `POST /api/player/loadout`

```json
{ "requestId": "…", "characterId": 1, "equipment": { "weapon": "eq_…" }, "summons": [1, 2] }
```

Ответ — профиль целиком.

### `GET /api/player/characteristics`

Итоговые характеристики выбранного персонажа с учётом прокачек, экипировки, саммонов, перков и статусов активного забега. Считаются тем же кодом, что бой.

```json
{
  "health": 2350, "maxHealth": 8400, "damage": 6, "attackMultiplier": 1,
  "armor": 7, "defence": 0.0205, "evasion": 0.05,
  "criticalChance": 0.15, "criticalMultiplier": 1.5,
  "combo1Chance": 0.1, "combo2Chance": 0.05, "comboMultiplier": 0.5,
  "counterChance": 0.1, "counterMultiplier": 0.7,
  "energy": 0, "energyGain": 30, "maxEnergy": 100,
  "skillMultiplier": 1, "equipmentSpellMultiplier": 1, "vampyrism": 0, "healingBoost": 1
}
```

То же самое клиент может посчитать локально через ядро: `IBattleCore.BuildCharacteristics(...)`.

### `POST /api/player/summon/level/reset`

```json
{ "summonId": 1, "requestId": "…" }
```

Уровень саммона падает до первого, на аккаунт возвращается часть потраченного `summon_exp` (коэффициент `summon_level_reset_coeff`), сама операция стоит ресурс из константы `summon_level_reset_resource`. Ответ — профиль.

### `POST /api/player/equipment/level/reset`

```json
{ "instanceId": "eq_…", "requestId": "…" }
```

Уровень предмета падает до первого, возвращается часть потраченных ресурсов (`equipment_lvl_drop_proportion`). Ответ — профиль.

### `POST /api/player/equipment/merge`

```json
{ "instanceId": "eq_…", "paymentInstanceIds": ["eq_…", "eq_…"], "requestId": "…" }
```

Трансформация по редкости: сервер проверяет плату по `merge_requirements` предмета (`equipment_id:<id>` — конкретный предмет, `equipment_rarity:<редкость>` — любой предмет того же `type` и указанной редкости), удаляет предмет и плату, выдаёт предмет той же `merge_group` с `merge_number` на 1 больше и тем же уровнем. Если исходный предмет был экипирован, новый занимает его слот. Ответ — профиль.

### `POST /api/player/equipment/level`

```json
{ "instanceId": "eq_…", "requestId": "…" }
```

### `POST /api/player/summon/level`

```json
{ "summonId": 1, "requestId": "…" }
```

### `POST /api/player/summon/mastery`

```json
{ "summonId": 1, "requestId": "…" }
```

Все три отвечают профилем. Стоимость и лимиты берутся из конфигов (`Summon_levels`, `Mastery`, уровни экипировки); при недостатке ресурсов — `400` с текстом.

## Сброс прогресса и удаление аккаунта

Обе ручки работают от токена вызывающего: игрок трогает только свои данные. Ни `deviceId`, ни админ-ключ не нужны, и при переходе с устройств на платформенные аккаунты ничего не меняется — личность берётся из токена.

### `POST /api/player/reset`

Стирает прогресс: профиль, забеги, журнал выдач, записи идемпотентности. Аккаунт и токены остаются, профиль создаётся заново пустым. Ответ — свежий профиль.

Это то, чем разработчик обнуляет себя между тестами: одна кнопка в дев-меню клиента или один запрос из REST-клиента со своим токеном.

### `DELETE /api/player/account`

Удаляет всё, включая аккаунт и привязку устройства. После этого токены недействительны, а вход по тому же `deviceId` создаст новый пустой аккаунт. Ответ — что было удалено:

```json
{ "userId": "usr_…", "profiles": 1, "runs": 3, "ledger": 12, "idempotency": 4, "users": 1 }
```

Эта ручка закрывает и требование сторов на удаление данных по запросу игрока.

Удаление чужих данных остаётся админской операцией: `DELETE /admin/player/{userId}` на ops-порту с админ-ключом.

## Забег: сервер ведёт путь

### `GET /api/run/current`

Текущий забег или `404`, если активного нет.

### `POST /api/run/start`

```json
{ "storyLevelId": 1, "requestId": "…", "battleDelivery": "seed" }
```

### `POST /api/run/advance`

```json
{ "runId": "run_…", "requestId": "…", "battleDelivery": "seed" }
```

Продвигает забег на один этап: сервер решает событие, считает бой, начисляет опыт и награды.

### `POST /api/run/choose`

```json
{ "runId": "run_…", "requestId": "…", "picks": [3], "battleDelivery": "seed" }
```

Выбор из `pendingChoice` (например перк на уровне).

### `POST /api/run/abandon`

```json
{ "runId": "run_…", "requestId": "…" }
```

### Ответ забега

```json
{
  "runId": "run_…",
  "status": "active",
  "storyLevelId": 1,
  "configVersion": "sha256:d5349d…",
  "stageIndex": 2,
  "stagesTotal": 5,
  "experience": 120,
  "experienceLevel": 3,
  "currentHealth": 6200,
  "perks": [1, 4],
  "stages": [ { "stageId": 10, "eventId": 3, "isBoss": false, "resolved": true } ],
  "bonuses": [ { "bonusId": 5, "count": 1, "remainingBattles": 4 } ],
  "pendingChoice": { "kind": "perk", "options": [1, 4, 7], "choiceCount": 1 },
  "step": {
    "eventType": "fight",
    "eventId": 3,
    "stageId": 10,
    "locKey": "",
    "locKeyStart": "loc.fight.start",
    "locKeyEnd": "loc.fight.end",
    "experienceGained": 40,
    "levelUps": [3],
    "appliedRewards": [ { "type": "Resource", "id": 0, "rewardKey": "soft_money", "count": 100 } ],
    "profileRev": 13,
    "battleDigest": "sha256:bdbbd3…",
    "battleStepCount": 87,
    "battleInput": { "teamA": {}, "teamB": {}, "storyLevelId": 1, "stageId": 10, "seed": 42 },
    "battle": null,
    "runCompleted": false,
    "runFailed": false
  }
}
```

Значения строковых полей:

- `status` — `active`, `completed`, `failed`.
- `step.eventType` — `default_event`, `fork_event`, `fight`, `completed`.
- `pendingChoice.kind` — `perk` (выбрать перк из `options`) или `fork` (выбрать ветку: `picks: [1]` или `picks: [3]`).
- `appliedRewards[].type` — `Resource`, `Character`, `Summon`, `Equipment` (для `Resource` смысл несёт `rewardKey`, для остальных — `id`). Награды типов `Bonus` и `Status` в профиль не пишутся: они уходят в забег и видны в `run.bonuses`.

Важное:
- `stages` до текущего этапа раскрыты, дальше `eventId = 0` (сервер не показывает будущее).
- `appliedRewards` — что реально записано в профиль, `profileRev` — версия профиля после записи. Это источник истины для экрана награды, а не команды `GrantReward` в скрипте боя.
- `battleDelivery` управляет тем, что придёт в шаге. **Целевой режим — `"seed"`**: сервер присылает только `battleInput` (состав боя и сид), клиент переигрывает бой ядром сам и сверяет `battleDigest` через `IBattleCore.ComputeDigest`. Режим по умолчанию (пусто или `"script"`) отдаёт готовый скрипт в `battle` — он оставлен только для клиента, который ещё не подключил ядро, и уйдёт после миграции.

  Почему в режиме сида приходит не один сид: бой определяется сидом **и** составом — персонаж, уровни, экипировка, бонусы, статусы, мобы этапа. Состав собирает сервер из профиля, клиент его авторитетно не знает. Это и есть `battleInput`: около 200 байт после gzip против 1.4–3.9 КБ у скрипта.
- `currentHealth` после боя — серверное, клиент его не считает.

## Скрипт боя

В целевом режиме клиент получает его не по сети, а считает сам: `IBattleCore.Replay(step.battleInput)`. Поле `step.battle` с готовым скриптом — переходный режим для клиента без ядра. Структура в обоих случаях одна и та же.

```json
{ "protocolVersion": 1, "seed": 42, "outcomeType": 1, "steps": [ … ] }
```

`BattleStep`: `index`, `turn`, `phase`, `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `commands`.
`BattleCommand`: `commandType` и `parameters` (объект, состав зависит от типа).

`outcomeType`: `0 Unknown`, `1 TeamAWin`, `2 TeamBWin`, `3 Timeout`, `4 Draw`.

`phase`: `1 StatusTrigger`, `2 PerkTrigger`, `3 SummonSkill`, `4 SummonAttack`, `5 UnitSkill`, `6 NormalAttack`, `7 CounterAttack`, `8 Combo1Attack`, `9 Combo2Attack`, `10 EnergySkill`, `11 Death`, `12 ReturnToPosition`, `13 Approach`, `14 UnitCooldown`, `15 SummonCooldown`, `16 CounterCooldown`, `17 ComboCooldown`.

`commandType` и ключи `parameters`:

| Код | Команда | Параметры |
| --- | --- | --- |
| 1 | `Wait` | `seconds` |
| 2 | `Approach` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `isMelee` |
| 3 | `ReturnToPosition` | `actorId`, `actorSlotIndex` |
| 4 | `PlayAnimation` | `actorId`, `actorSlotIndex`, `animationKey` |
| 5 | `ShowDamage` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `damage`, `isCritical`, `isEvaded` |
| 6 | `ShowHeal` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `heal` |
| 7 | `ShowMiss` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex` |
| 8 | `ApplyStatus` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `statusId`, `stacks`, `durationTurns` (`-1` = бесконечно) |
| 9 | `RemoveStatus` | `targetId`, `targetSlotIndex`, `statusId` |
| 10 | `TickStatus` | `targetId`, `targetSlotIndex`, `statusId`, `value` |
| 11 | `CastSkill` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `skillId` |
| 12 | `TriggerPerk` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `perkId` |
| 13 | `SetHp` | `unitId`, `slotIndex`, `hp` |
| 14 | `SetEnergy` | `unitId`, `slotIndex`, `energy` |
| 15 | `SetBonus` | `unitId`, `slotIndex`, `bonusId`, `value`, `sourceId` |
| 16 | `SpawnUnit` | `unitId`, `slotIndex` |
| 17 | `DespawnUnit` | `unitId`, `slotIndex` |
| 18 | `KillUnit` | `unitId`, `slotIndex` |
| 19 | `SetBattleResult` | `outcome` |
| 20 | `GrantReward` | `rewardType`, `rewardId`, `count`, `targetId` — только для показа, профиль этим не меняется |

Клиент не решает крит, уклонение, урон, смерть, статусы и исход — только играет команды по порядку.

## Вход боя (`battleInput`, он же `BattleReplayData`)

```json
{ "teamA": { "mainUnits": [ … ], "summons": [ … ] }, "teamB": { … }, "storyLevelId": 1, "stageId": 10, "seed": 42 }
```

`UnitSnapshot`: `id`, `level`, `masteryLevel`, `equipments` (`id`, `level`), `equipmentIds`, `trainingLevel`, `artifactIds`, `aspectIds`, `activePerkIds`, `perkUsages` (`perkId`, `usedCount`), `activeSkillIds`, `activeStatusIds`, `activeBonuses` (`id`, `count`, `remainingBattles`), `slotIndex`, `currentHealth`.

`BattleScriptResponse` кроме `steps` несёт `perkUsages` (`perkId`, `usedCount`, `remainingUses`) — расход перков с лимитом использований (сейчас воскрешение). Для проигрывания боя поле не нужно, забег использует его сам.

Клиент этот объект не собирает: его строит сервер. Единственное применение на клиенте — передать в `IBattleCore.Replay(...)`.

## Бой напрямую

`POST /api/battle/simulate` и `POST /api/battle/replay` принимают `BattleSimulationData` / `BattleReplayData` и возвращают скрипт. Это инструмент разработки: при включённой авторизации они доступны только на ops-порту по админ-ключу, играть через них нельзя.

## Конфиги: снапшот с сервера

### Что такое снапшот

Геймдизайнер правит игровые таблицы в Google Sheets и публикует их на сервер. Сервер складывает сырые строки всех 17 листов в один неизменяемый **снапшот** и считает по нему версию — `sha256:<hex>` по именам листов и тексту строк. Активный снапшот один на окружение; публикация новой версии не правит старую, а подменяет активную целиком.

Эта версия и есть `configVersion`, который приходит в ответе забега. Забег пинит её на старте: все бои забега считаются по той версии конфигов, что была активна в момент старта.

### Зачем он клиенту

Клиент проигрывает бой сам: сервер отдаёт `battleInput` (вход боя и сид), а скрипт боя строит ядро `IBattleCore` на устройстве. Ядро — тот же код, что считает бой на сервере, и чтобы получить тот же результат, ему нужны **те же самые конфиги**. Не похожие, а побайтово те же: один лишний пробел в строке листа меняет урон на округлении, и `battleDigest` разойдётся.

Поэтому конфиги для ядра берутся с сервера, а не из клиентского `Assets/Configs/Core/Json/ConfigBundle.json`. Локальный бандл не подходит принципиально:

| | снапшот сервера | клиентский `ConfigBundle.json` |
| --- | --- | --- |
| листов | 17 | 16, нет `Character_promotes` |
| имена | `Story_stages`, `Perk_groups`, `Mastery`, `Exp_levels_patterns`, `Summon_levels` | `StoryStages`, `PerkGroups`, `Masteries`, `ExperienceLevelPatterns`, `SummonLevels` |
| значения | сырые строки листа: `"id":"1"`, `"skill_ids":"1;2"` | уже разобранные парсерами клиента: `"id":1`, `"skill_ids":[1,2]` |
| колонки | текущие | местами устаревшие |

Если собрать ядро из локального бандла, пять листов ядро просто не найдёт, часть значений не прочитает, а `ConfigVersion` никогда не совпадёт с `configVersion` забега. Клиентский бандл остаётся для визуала и редактора — арты, тексты, превью в инспекторе; для боя он не годится.

### `GET /api/config/bundle`

Заголовки: `Authorization: Bearer <accessToken>`, необязательный `If-None-Match: "<ETag прошлого ответа>"`.

```json
{
  "Version": "sha256:53ab01b11f8f7653bf8977e3cf23f19599391240d496b2b52031bf114ed13031",
  "ShortVersion": "cfg-53ab01b11f8f",
  "Configs": [
    { "Name": "Constants", "Content": "[{\"id\":\"health_base\",\"value\":\"600\"}]" },
    { "Name": "Characters", "Content": "[{\"id\":\"1\",\"is_melee\":\"1\"}]" }
  ]
}
```

| Поле | Что это |
| --- | --- |
| `Version` | версия снапшота, сверяется с `configVersion` забега |
| `ShortVersion` | та же версия коротко (`cfg-` + 12 символов), для логов и экрана отладки |
| `Configs[].Name` | имя листа; порядок элементов значим, менять его нельзя |
| `Configs[].Content` | строка с JSON-массивом строк листа; разбирать её клиенту не нужно |

Ответ в отличие от остальных ручек в `PascalCase`: это формат, который `BattleCoreFactory.CreateFromBundle` принимает как есть. Тело передаётся в фабрику целиком, без разбора и без правок — любое переформатирование меняет версию.

Коды: `200` — тело бандла, `304` — версия та же, тела нет, `401` — токен, `503` — конфиги на сервере не загружены (идёт публикация или перезапуск), повторить позже.

### Куда это ложится в клиенте

1. Тело сохраняется на диск как есть, рядом — `Version` (например `Application.persistentDataPath/battle-core/bundle.json` и `version.txt`).
2. Ядро создаётся один раз из текста бандла: `new BattleCoreFactory().CreateFromBundle(bundleJson, new UnityCoreLog())`, результат живёт синглтоном (пример биндинга — в `client-handover.md`).
3. `ETag` из ответа хранится рядом с кэшем и уходит в `If-None-Match` при следующей проверке.

Разбирать `Content` своими парсерами не нужно: всё, что клиенту может понадобиться из конфигов, ядро отдаёт через `battleCore.Configs` — арты, редкости, перки, статусы, сюжет.

### Когда забирать

| Момент | Что делать |
| --- | --- |
| первый запуск, кэша нет | скачать бандл на загрузочном экране сразу после входа, до меню |
| каждый следующий запуск | собрать ядро из кэша, параллельно дёрнуть ручку с `If-None-Match`: `304` — ничего не делать, `200` — сохранить новый бандл и пересобрать ядро |
| `run.configVersion` не равен `battleCore.ConfigVersion` | скачать бандл заново и пересобрать ядро; бой до этого не играть |
| активный забег | бандл не менять: версия забега зафиксирована на старте. Если версия уже разошлась — дать игроку доиграть на старом ядре либо завершить забег, но не подменять конфиги посреди него |
| `503` на ручке | повторить через несколько секунд; если кэш есть, играть на нём до успешного ответа |

Отдельного пуша «конфиги обновились» нет: публикация происходит редко и в рабочее время геймдизайнера, поэтому проверка по `If-None-Match` на загрузочном экране покрывает все случаи.

### Чего не делать

- не собирать ядро из локального `ConfigBundle.json`;
- не перезаписывать `Content`, не переупорядочивать `Configs`, не форматировать JSON — это меняет версию;
- не блокировать игру навсегда при расхождении версий: расхождение лечится перекачиванием бандла, а не запретом;
- не дёргать ручку на каждый бой: версия меняется только при публикации конфигов.

`GET /api/config/status` клиенту не нужен — он закрыт ключом публикатора и предназначен геймдизайнеру.

## Где искать данные по механикам

| Механика | Откуда клиент берёт |
| --- | --- |
| Характеристики | `GET /api/player/characteristics` или локально `IBattleCore.BuildCharacteristics` |
| Скиллы | конфиги (персонаж, моб, саммон, оружие) через `IBattleCore.Configs.Skills`; выбора скиллов игроком нет, сервер `activeSkillIds` от клиента не принимает |
| Перки | конфиги `Configs.Perks` / `Configs.PerkGroups`; активные в забеге — `run.perks`, выбор — `pendingChoice` |
| Бонусы | активные в забеге — `run.bonuses`; вне забега отдельной сущности нет, они уже учтены в характеристиках; описания — `Configs.Bonuses` |
| Статусы | конфиги `Configs.Statuses`; в бою приходят командами `ApplyStatus` / `TickStatus` / `RemoveStatus` |
| Экипировка | профиль (`equipment`, `loadout`), прокачка уровня — `POST /api/player/equipment/level`; трансформации нет |
| Саммоны | профиль (`summons`), прокачка — `/summon/level` и `/summon/mastery`; описания — `Configs.Summons`, `Configs.SummonLevels`, `Configs.Masteries` |
| Сюжет | `Configs.StoryLevels` / `StoryStages` / `StoryEvents` для текстов и визуала, прохождение — `/api/run/*` |
| Награды | `step.appliedRewards` и `profileRev`, ресурсы — в профиле |
| Опыт и уровни | `run.experience`, `run.experienceLevel`, `step.experienceGained`, `step.levelUps` |

## Чего сервер от клиента не принимает

Исход боя, скрипт боя, состав боя, список скиллов, количество наград, прогресс забега. Всё это сервер считает сам из своего сида и запиненной версии конфигов. Клиент присылает только намерение: стартовать забег, сделать шаг, выбрать перк, прокачать, сменить лоадаут.

## See Also

- [Battle API](battle-api.md) — устройство симуляции и доставка боя
- [Battle Core in Unity](battle-core-in-unity.md) — подключение DLL ядра
- [Runs](runs.md) — серверное состояние забега
- [Player State](player-state.md) — модель профиля и идемпотентность
