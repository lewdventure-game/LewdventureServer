# Клиент и сервер: что есть и что делать

Один документ для клиентской команды. Сервер считает бой и ведёт прогресс, клиент показывает. Проверено на dev 27 сентября 2026: полный забег из пяти этапов с тремя боями проходит без участия клиента в расчётах.

## 1. Что мы передали и как это встроить

В поставке (`out/unity-core`) четыре файла плюс справка:

| Файл | Что это |
| --- | --- |
| `Lewdventure.Server.Contracts.dll` | модели протокола боя и настройки сериализации |
| `Lewdventure.Server.GameConfig.dll` | чтение конфигов и enum'ы механик |
| `Lewdventure.Server.Battle.dll` | симулятор боя и точка входа |
| `link.xml` | защита от стриппинга IL2CPP |
| `UnityCoreLog.cs` | адаптер логов ядра на `Debug.Log` |
| `VERSION.txt` | коммит сборки, `protocolVersion`, версия Newtonsoft |

Зависимость одна — пакет `com.unity.nuget.newtonsoft-json` (уже в проекте). Api Compatibility Level — `.NET Standard 2.1` (уже стоит). Больше ничего в Player Settings менять не нужно.

### Шаг 1. Положить DLL

Три `.dll` и `link.xml` — в `Assets/Plugins/BattleCore/`. Настройки импорта дефолтные: Any Platform, Auto Reference включён. `link.xml` обязателен: без него IL2CPP вырезает рефлексию Newtonsoft и разбор `BonusType`, и данные перестают читаться уже в собранном билде.

### Шаг 2. Положить логгер

`UnityCoreLog.cs` — **внутрь той сборки, где вы будете создавать ядро**, например `Assets/Scripts/Core/Common/`. В `Assets/Plugins` его класть нельзя: файлы оттуда попадают в отдельную сборку, которую asmdef'ы `Core` и `Game` не видят. В файле шесть методов поверх `Debug.Log`; в релизной сборке можно передавать `new SilentCoreLog()` из DLL, потому что бой пишет много отладочных строк.

### Шаг 3. Создать ядро один раз

Ядру нужен **бандл конфигов с сервера**: `GET /api/config/bundle` (под игровым токеном) отдаёт активный снапшот ровно в том виде, из которого сервер сам собирает бой. Локальный `Assets/Configs/Core/Json/ConfigBundle.json` для ядра не подходит — у него другие имена доменов, значения уже типизированы парсерами клиента и состав колонок свой, поэтому `ConfigVersion` такого ядра никогда не совпадёт с `configVersion` забега, а половина доменов не прочитается. Подробнее — «Бандл конфигов» ниже.

```csharp
Container.Bind<Server.Shared.ISharedCore>().FromMethod(CreateSharedCore).AsSingle();

private Server.Shared.ISharedCore CreateSharedCore(InjectContext context)
{
    var bundleJson = context.Container.Resolve<IServerConfigBundleCache>().Load();
    var result = new Server.Shared.SharedCoreFactory().CreateFromBundle(bundleJson, new UnityCoreLog());

    if (result.Succeeded == false)
        throw new InvalidOperationException($"[Battle]: shared core not created: {string.Join("; ", result.Errors)}");

    for (int i = 0; i < result.Warnings.Count; i++)
        UnityEngine.Debug.LogWarning($"[Battle]: {result.Warnings[i]}");

    return result.SharedCore;
}
```

Важные детали:

- `FromMethod().AsSingle()` без `NonLazy()` — ядро собирается при первом обращении, когда конфиги уже загружены. С `NonLazy()` Zenject попытается создать его при инициализации контейнера, до загрузки бандла.
- `result.Warnings` стоит логировать: там те же предупреждения, что видит геймдизайнер при публикации конфигов (незнакомый тип перка, опечатка в параметрах, отсутствующая колонка).
- Если удобнее создавать ядро в `Game`, достаточно сделать `IConfigSource` публичным — тогда биндинг переезжает в `GameInstaller`.
- Сборка ядра из бандла занимает десятки миллисекунд. Если не хочется даже такого фриза на первом бою, дёрните `sharedCore.ConfigVersion` на загрузочном экране — ядро создастся там.
- Бандл надо успеть скачать до создания ядра: загрузочный экран качает его сразу после входа, кладёт в кэш и только потом пускает в меню.

### Бандл конфигов

`GET /api/config/bundle` — игровой токен в `Authorization`, ответ:

```json
{
  "Version": "sha256:53ab01b11f8f…",
  "ShortVersion": "cfg-53ab01b11f8f",
  "Configs": [ { "Name": "Constants", "Content": "[{…}]" }, { "Name": "Characters", "Content": "[{…}]" } ]
}
```

`Content` — строка с сырыми строками листа; `SharedCoreFactory.CreateFromBundle` принимает это тело целиком, лишние поля игнорирует и считает `ConfigVersion` по тем же байтам, что сервер, поэтому версии совпадают по построению.

Правила работы с ним:

- в ответе есть `ETag`; присылайте его в `If-None-Match` — если версия не менялась, придёт `304` без тела;
- сохраняйте тело на диск (`Application.persistentDataPath`) вместе с версией: при старте можно сразу собрать ядро из кэша и уже потом проверить `304`;
- если `sharedCore.ConfigVersion` не совпал с `run.configVersion` — скачайте бандл заново и пересоберите ядро; активный забег в этот момент начинать нельзя;
- `503` — конфиги на сервере ещё не загружены, повторить позже.

### Шаг 4. Перейти на модели из DLL

Свои модели протокола в `Game/Battles/Models` удаляются, код переводится на типы из DLL — иначе получается два набора одних и тех же классов, которые можно рассинхронизировать руками.

| Удалить | Заменяется на |
| --- | --- |
| `CommandType`, `OutcomeType`, `BattlePhaseType`, `BattleSide` | одноимённые из `Server.Battles` |
| `BattleCommand`, `IBattleCommand` | `Server.Battles.BattleCommand` |
| `BattleStep`, `IBattleStep` | `Server.Battles.BattleStep` |
| `BattleScriptResponse`, `IBattleScriptResponse` | `Server.Battles.BattleScriptResponse` / `IBattleScriptResponse` |
| `BattleReplayData`, `IBattleReplayData` | `Server.Battles.BattleReplayData` / `IBattleReplayData` |
| `BattleSimulationData`, `IBattleSimulationData` | `Server.Battles.BattleSimulationData` / `IBattleSimulationData` |
| `UnitSnapshot`, `ITeamSnapshot`, `TeamSnapshot`, `IUnitSnapshot` | одноимённые из `Server.Battles` |
| `EquipmentSnapshot`, `IEquipmentSnapshot`, `BonusGrantSnapshot`, `IBonusGrantSnapshot` | одноимённые из `Server.Battles` |

Две детали, на которые уйдёт время:

- **шаг и команда в DLL — классы, а не интерфейсы.** Код проигрывателя, типизированный на `IBattleStep` и `IBattleCommand`, переводится на `BattleStep` и `BattleCommand`;
- **`PreloadManifest` остаётся клиентским.** В DLL такого понятия нет, и не должно быть: это предзагрузка ассетов. Строится он по скрипту, поэтому просто считается отдельно от модели ответа.

Остальное в `Game/Battles/Models` тоже остаётся: `BattleFlags`, `BattleLeaveType`, `TurnSide`, `FlytextStyle`, `FlytextType`, `BattlePreloadKeys`, `BattlePreloadManifest` и их интерфейсы.

Enum'ы механик берутся оттуда же и свои удаляются: `PerkType` → `Server.Perks`, `StatusType` → `Server.Statuses`, `BonusType` и `BonusOperatorType` → `Server.Bonuses`, `RarityType` → `Server.Common`. Это место, где расхождение чисел опаснее всего: они должны совпадать с сервером бит в бит.

Сериализация — только `new Server.Battles.BattleJsonSettingsFactory().Create()`; свои настройки для протокола не нужны, разойдутся с сервером.

### Шаг 5. Проверить, что встало

- В консоли при первом обращении не должно быть ошибок создания ядра.
- `sharedCore.ConfigVersion` сравнить с `configVersion` из ответа забега: не совпало — ядро собрано не из серверного бандла или бандл устарел, надо перекачать `GET /api/config/bundle` и пересобрать ядро.
- Пересобрать проект с IL2CPP хотя бы раз: именно там проявляется забытый `link.xml`.

### Обновление DLL

Руками ничего собирать и передавать не нужно. На каждый пуш в `master`, который задел `Lewdventure.Server.Contracts`, `GameConfig`, `Battle` или саму поставку, workflow `unity-core` собирает комплект и публикует его релизом `unity-core-latest` (ассет `unity-core.zip`, plus артефакт прогона на 90 дней).

На клиенте обновление — одна команда из корня проекта:

```
powershell -ExecutionPolicy Bypass -File Tools\update-battle-core.ps1
```

(или `pwsh Tools/update-battle-core.ps1`, если стоит PowerShell 7)

Скрипт качает последний релиз через `gh`, печатает установленную и новую версию, раскладывает три DLL, `link.xml` и `VERSION.txt` в `Assets/Plugins/BattleCore/`, `UnityCoreLog.cs` в `Assets/Scripts/Core/Common/` и показывает, что изменилось в git. С ключом `-Stage` сразу стейджит. Нужен GitHub CLI с доступом к серверному репозиторию (`winget install GitHub.cli`, затем `gh auth login`).

Когда обновляться:

- **обязательно** — если в `VERSION.txt` вырос `protocolVersion`: протокол изменился, старый клиент с новым сервером не совместим;
- **по необходимости** — если нужна новая механика боя или новые поля конфигов; версия боя на сервере и в ядре должна совпадать, иначе `battleDigest` разойдётся;
- **не нужно** — если менялись только API, Mongo, инфраструктура или документация: сборка ядра детерминированная, и на тех же исходниках DLL получаются байт-в-байт теми же, git просто не увидит изменений. Поэтому workflow и не запускается на такие коммиты.

Локальная сборка (`bash deploy/scripts/build-unity-core.sh` → `out/unity-core`) остаётся для случая, когда нужно проверить поставку до пуша, но в репозиторий клиента DLL кладутся только из релиза: собранные локально и собранные в CI сборки побайтово разные (в них зашиты пути исходников и идентификатор модуля), код в них одинаковый, но git увидит изменения на каждом переключении между источниками.

### Что изменилось в протоколе 2026-09-27

Изменения добавочные, `protocolVersion` остался `1`: старый клиент не ломается, но если вы пока держите свои копии моделей, их нужно догнать.

| Что | Где | Зачем клиенту |
| --- | --- | --- |
| `UnitSnapshot.perkUsages` (`perkId`, `usedCount`) | запрос боя | забег шлёт израсходованные использования перков с лимитом; клиент это поле не заполняет |
| `BattleScriptResponse.perkUsages` (+ `remainingUses`) | ответ боя | служебное для забега, на проигрывание не влияет |
| `BonusType` 56–58 — `equip_spell_multiplier_local/perk/global` | enum механик | если enum свой, добавить три значения **в конец**, иначе числа разъедутся |
| `equipmentSpellMultiplier` | `GET /api/player/characteristics` и `IBattleCore.BuildCharacteristics` | новая характеристика СНАР_СПЕЛЛ_МН в HUD/попапе |
| `SetHp 0` + `KillUnit` по персонажу игрока при исходе `Timeout` | скрипт боя | при исчерпании лимита ходов смерть теперь приходит командами, придумывать её не надо |

## 2. Как пользоваться ядром

Всё, что нужно, доступно через `ISharedCore`, который вы забиндили на шаге 3:

| Член | Зачем |
| --- | --- |
| `Battle.Replay(battleInput)` | посчитать бой и получить скрипт команд |
| `Battle.ComputeDigest(script)` | контрольная сумма своего расчёта |
| `Battle.TryValidate(data, out error)` | проверить входные данные боя |
| `Battle.BuildCharacteristics(unit, side, isSummon, storyLevelId, stageId)` | 21 характеристика для UI, те же числа, что в бою |
| `Configs` | все мапперы конфигов: персонажи, мобы, саммоны, экипировка, перки, статусы, скиллы, сюжет, константы |
| `ConfigVersion` | версия бандла, сверяется с серверной |

Сервисам, которым нужны только таблицы, достаточно `Configs`; бой и характеристики живут в `Battle` (`IBattleCore`).

Сериализация только через `new BattleJsonSettingsFactory().Create()` — свои настройки для протокола писать нельзя, разойдутся с сервером.

## 3. Ручки сервера

База dev: `https://api-dev.lewdventure.online`. Локально (compose.local) — `http://localhost:5000`. Заголовок `Authorization: Bearer <accessToken>` на всех запросах кроме `/api/auth/*`.

| Метод и путь | Зачем |
| --- | --- |
| `POST /api/auth/device` | вход по `deviceId`, отдаёт `userId`, access и refresh токены |
| `POST /api/auth/refresh` | обновить токены (refresh одноразовый) |
| `GET /api/player/profile` | всё состояние: ресурсы, персонажи, саммоны, экипировка, лоадаут, сюжет, флаги, `rev` |
| `POST /api/player/loadout` | сменить персонажа, экипировку, саммонов |
| `GET /api/player/characteristics` | итоговые характеристики (то же даёт ядро локально) |
| `POST /api/player/equipment/level` | прокачка уровня экипировки |
| `POST /api/player/summon/level` | прокачка уровня саммона |
| `POST /api/player/summon/mastery` | прокачка мастерства саммона |
| `POST /api/player/summon/skill/level` | прокачка скилла саммона |
| `POST /api/player/reset` | стереть свой прогресс, аккаунт остаётся, стартовый набор выдаётся заново |
| `DELETE /api/player/account` | удалить аккаунт и данные |
| `GET /api/run/current` | текущий забег или `404` |
| `POST /api/run/start` | начать забег: `storyLevelId`, `requestId`, `battleDelivery` |
| `POST /api/run/advance` | шаг забега |
| `POST /api/run/choose` | выбор из `pendingChoice` (перк или ветка) |
| `POST /api/run/abandon` | бросить забег |

Правила:

- у каждого мутирующего запроса свой `requestId` (guid), повтор с тем же не применит операцию дважды;
- `401` — один `refresh` и повтор, если снова `401` — заново вход по устройству;
- `409` — состояние изменилось: перечитать профиль или забег и повторить;
- `503` — конфиги на сервере не загружены, повторить позже;
- `protocolVersion` из скрипта боя должен совпадать с клиентским, иначе требовать обновления;
- `ConfigVersion` ядра должен совпадать с `run.configVersion`: бандл конфигов нельзя менять посреди забега.

## 4. Что приходит в шаге забега

```json
{
  "runId": "run_…", "status": "active", "stageIndex": 3, "stagesTotal": 5,
  "experience": 120, "experienceLevel": 2, "currentHealth": 5529,
  "perks": [1], "bonuses": [{ "bonusId": 1, "count": 4, "remainingBattles": 0 }],
  "pendingChoice": { "kind": "perk", "options": [1, 7, 2], "choiceCount": 1 },
  "step": {
    "eventType": "fight",
    "locKeyStart": "…", "locKeyEnd": "…",
    "experienceGained": 40, "levelUps": [2],
    "appliedRewards": [{ "type": "Resource", "id": 0, "rewardKey": "soft_money", "count": 100 }],
    "profileRev": 13,
    "battleInput": { "teamA": {}, "teamB": {}, "storyLevelId": 1, "stageId": 10, "seed": 2057051268108185200 },
    "battleDigest": "sha256:2c466360…",
    "battleStepCount": 101,
    "battle": null
  }
}
```

- `eventType` — `default_event`, `fork_event`, `fight`, `completed`; ложится на клиентский `StoryEventType` один в один.
- `pendingChoice.kind` — `perk` (выбрать перк из `options`) или `fork` (ветка: `picks: [1]` или `[3]`).
- `appliedRewards` и `profileRev` — что реально записано в профиль. Источник истины для экрана награды, а не команды `GrantReward` в скрипте боя.
- `battleInput` — состав боя и сид: из чего считать бой. Статы не приходят, они выводятся из конфигов тем же кодом.
- `battleDigest` — контрольная сумма серверного расчёта; клиент сверяет свою и только потом показывает бой.
- `battle` — готовый скрипт. Приходит только если не просить `battleDelivery: "seed"`; это переходный режим для клиента без ядра.
- `runCompleted` и `runFailed` — забег закончился победой или провалом: показать итоговый экран и перестать звать `advance` (дальше придёт `No active run.`).

Бой в режиме сида: 236 байт входа против 3.9 КБ скрипта после gzip.

```csharp
var script = battleCore.Replay(step.battleInput);

if (battleCore.ComputeDigest(script) != step.battleDigest)
{
    Debug.LogError("[Battle] расчёт разошёлся с сервером");

    return;
}

Play(script);
```

## 5. Порядок миграции

**Этап 1. Авторизация.** `Game/Servers/Client.cs`: вход по `deviceId` при старте, токены в `PersistentDataStorage`, заголовок `Bearer` во всех запросах, один `refresh` на `401` с повтором.

**Этап 2. Профиль как источник правды.** `IPlayerData` заполняется из `GET /api/player/profile`; мутаторы (`CompleteStoryLevel`, `IncrementRunCount`, `AddPermanentBonus`) больше не пишут состояние, вместо них серверные ручки и перечитывание профиля. Локальный сейв остаётся кэшем: расходится с сервером — прав сервер. Стартовый набор выдаёт сервер, клиенту выдавать нечего.

**Этап 3. Забег ведёт сервер.** Меняются три сервиса:

| Файл | Что с ним |
| --- | --- |
| `Game/Stories/Factories/StoryFactory.cs` | раскладка этапов уходит, этапы приходят в `run.stages` (будущие — с `eventId: 0`) |
| `Game/Stories/Services/StoryProgressService.cs` | опыт, уровни, очередь перков, `ApplyEventRewards` заменяются отображением `run.*` и `step.*` |
| `Game/Stories/Services/EventLifecycleService.cs` | событие приходит из `step`, а не выбирается локально |

Остальные 27 файлов в `Game/Stories/Services` (движение, локации, нарратив, экраны) остаются: они не решают числа.

**Этап 4. Бой по сиду.** В запросы забега добавить `"battleDelivery": "seed"`, в `Game/Battles/Services/BattleService.cs` считать бой ядром из `step.battleInput`, сверять дайджест. `BattleSimulationDataBuilder` удаляется: состав боя собирает сервер. Вызовы `api/battle/simulate` и `api/battle/replay` убрать — они стали инструментом разработки и закрыты админ-ключом.

**Этап 5. Убрать свой слой чтения конфигов (по желанию).** `sharedCore.Configs` отдаёт все мапперы: персонажи, мобы, саммоны и их уровни, мастерство, экипировка, перки и группы перков, статусы, скиллы, сюжетные уровни, этапы и события, константы, бонусы, паттерны опыта. Клиентские мапперы и менеджеры конфигов после этого дублируют DLL. Шаг самый объёмный и самый механический — делать по одному домену за раз.

Порядок обязателен: 1 → 2 → 3 → 4; пятый в любой момент после четвёртого. Модели протокола переводятся на типы DLL на шаге встройки (раздел 1, шаг 4), отдельным этапом это не выносится.

## 6. Что остаётся клиентским

Презентация целиком: `BattlePreloadKeys` и `BattlePreloadManifest`, `BattleFlags`, `BattleLeaveType`, `TurnSide`, `FlytextStyle`, `FlytextType`, вьюхи юнитов, снаряды, VFX статусов, флайтексты, иконки, движение персонажа, локации, экраны, ECS. Модули `Game/Perks`, `Game/Statuses`, `Game/Skills`, `Game/Bonuses` — тоже презентация, расчётов в них нет.

`Game/Common/Services/RandomService` остаётся: он для визуала, в расчёте боя не участвует. `BattleProtocol.Version` остаётся, но сверяется с `protocolVersion` из скрипта.

Читы (`Assets/Scripts/Cheats/Battles/*`) продолжают работать только против `api/battle/simulate`, то есть остаются инструментом разработки и на серверный забег не влияют.

## 7. Ограничения, о которых лучше знать сразу

- **Офлайна нет.** Забег серверный: без сети его не начать. Незакрытый забег восстанавливается через `GET /api/run/current`.
- **Аккаунт привязан к устройству.** Переустановка или другое устройство — новый прогресс. Привязка к платформе появится позже.
- **Токен живёт час**, обновляется по refresh. После удаления аккаунта или бана любой запрос сразу `401`.
- **Конфиги не менять посреди забега:** он запинен на версию, иначе бой разойдётся с серверным.
- **Механики без данных:** трансформация экипировки, пресеты скилла персонажа, прокачка скилла саммона — в таблицах нет колонок, на сервере их нет тоже.

## Детали, если понадобятся

- [Client API](client-api.md) — полные модели запросов и ответов, значения всех enum'ов, таблица команд боя с параметрами.
- [Client Migration](client-migration.md) — план миграции клиента подробно
