# Миграция клиента на клиент-серверную схему

План перевода Unity-клиента с локальной логики на серверную. Составлен по коду клиента на 27 сентября 2026. Пять этапов, каждый заканчивается работающей игрой — останавливаться между этапами можно.

## Что уже сделано на стороне сервера и в клиенте

- Сервер ведёт профиль игрока, забег, бой, награды, опыт, уровни и выбор перков; ручки описаны в [Client API](client-api.md).
- Клиенту передана поставка ядра: три DLL, `link.xml`, `UnityCoreLog.cs` и `VERSION.txt`. Встройка — на клиентской стороне, порядок описан в [Client Handover](client-handover.md), раздел 1.
- На каждом бою в редакторе и dev-сборке клиент сверяет свой расчёт с серверным (`[Battle]: parity ok` в консоли).
- Паритет расчёта подтверждён в Mono и IL2CPP.

## Что у клиента считается локально и что с этим будет

Хорошая новость: формул боя в клиенте нет. Модули `Game/Perks`, `Game/Statuses`, `Game/Skills`, `Game/Bonuses` — это презентация (компоненты, системы, UI, парсеры визуала), они остаются как есть.

| Локальное сейчас | Что с ним |
| --- | --- |
| `StoryFactory` — раскладка этапов и событий | удаляется, этапы приходят в `run.stages` |
| `StoryProgressService` — опыт, уровни забега, очередь перков, `ApplyEventRewards`, `CompleteCurrentStoryLevel` | превращается в отображение серверных чисел |
| локальное здоровье забега | `run.currentHealth` |
| `IPlayerData` + `SaveLoadService` как источник правды | становятся кэшем серверного профиля |
| персонаж подразумевается неявно: `CharacterLevel = 1`, владения нет | владение приходит от сервера: `profile.characters[]` и `profile.loadout.characterId`, стартовый набор выдаёт сервер по `start_content` |
| путь боя через `api/battle/simulate` с составом от клиента | заменяется на `step.battleInput` из забега |
| свои копии протокола в `Game/Battles/Models` | удаляются в пользу типов из DLL |
| `BattleProtocol.Version` | сверять с `protocolVersion` из скрипта, расхождение — требование обновиться |
| `Game/Common/Services/RandomService` (в том числе `SeededRandomService`) | остаётся: он для визуала (`BattleFlytextService`), в расчёте боя не участвует |
| `Assets/Scripts/Cheats/Battles/*` — сборка боёв руками (`CheatBattleDraft`, `CheatBattleEditor`, `CreateCurrentSimulationData`) | после миграции работает только против `api/battle/simulate`, который при включённой авторизации доступен лишь по админ-ключу: читы остаются инструментом разработки, на серверный забег не влияют |
| `Game/Characteristics/CharacteristicHudService.RefreshFromServer` | либо `GET /api/player/characteristics`, либо локальный `IBattleCore.BuildCharacteristics` — числа совпадают |

Прогресс тестировщиков не переносится: серверный профиль начинается с нуля по `deviceId`. Реального прогресса сейчас нет, так что миграция данных не нужна.

## Предварительное условие

Авторизация включена: на dev с 27 сентября 2026 (`Auth__Enabled=true` в `compose.dev.yaml`, ключ подписи в `.env` на хосте), локально — в `compose.local.yaml`. Признак, что всё на месте: `GET /api/player/profile` без токена отвечает `401`, а не `404`.

Стартовый набор нового аккаунта задан константой `start_content` в листе `Constants` (синтаксис `тип:id:количество` через запятую, например `character:1:1`). Сервер выдаёт его один раз при создании профиля и сам ставит персонажа в лоадаут, поэтому клиенту ничего выдавать не нужно.

## Этап 1. Авторизация в клиенте

Файлы: `Assets/Scripts/Game/Servers/Client.cs`, `IClient.cs`, новый `Game/Servers/AuthService.cs`, хранилище — существующий `Core/Data/PersistentDataStorage`.

1. При старте игры: `POST /api/auth/device` с `deviceId` (стабильный идентификатор устройства) и `clientVersion`.
2. `accessToken`, `refreshToken` и их сроки сохранить в персистентное хранилище.
3. В `Client.Get/Post` добавлять заголовок `Authorization: Bearer <accessToken>`.
4. На `401`: один раз вызвать `POST /api/auth/refresh`, обновить токены, повторить запрос. Если и он `401` — заново `device`.

Готово, когда: `GET /api/player/profile` возвращает профиль, а перезапуск игры не теряет аккаунт.

## Этап 2. Профиль игрока как источник правды

Файлы: `Core/Data/PlayerData/IPlayerData.cs` и его реализация, `Core/Data/SaveLoadService`, экраны прокачки.

Соответствие данных:

| Сервер | Клиент сейчас |
| --- | --- |
| `profile.story.completedLevelIds` | `IPlayerData.CompletedStoryLevelIds` |
| `profile.characters[].promoteLevel` (с 1) | `IPlayerData.CharacterLevel` |
| `profile.equipment` + `profile.loadout.equipment` | `EquippedEquipmentIds`, `EquippedEquipmentLevels` |
| `profile.resources` | локальные счётчики ресурсов |
| `profile.flags` | `HasStartPerkChoiceReward`, `RunCount` и подобные флаги |
| — | `LanguageCode` остаётся локальным |

1. После авторизации читать `GET /api/player/profile` и заполнять `IPlayerData` из ответа.
2. Методы-мутаторы (`CompleteStoryLevel`, `IncrementRunCount`, `AddPermanentBonus`, `GrantStartPerkChoiceReward`) больше не пишут состояние: их место занимают серверные ручки и перечитывание профиля.
3. Прокачку перевести на эндпоинты: `POST /api/player/summon/level`, `POST /api/player/summon/mastery`, `POST /api/player/equipment/level`, `POST /api/player/loadout`. В каждом запросе свой `requestId` (guid), на `409` перечитать профиль и повторить.
4. Локальный сейв остаётся, но как кэш для показа до первого ответа сервера. Правило: расходится с сервером — прав сервер.

Готово, когда: прокачка и смена лоадаута видны в профиле на сервере, после переустановки прогресс подтягивается по тому же `deviceId`.

## Этап 3. Забег ведёт сервер

Самый большой этап. Файлы: `Game/Stories/Factories/StoryFactory.cs`, `Game/Stories/Services/StoryProgressService.cs`, `EventLifecycleService.cs`, `Game/Stories/Data/StoryRunState.cs`, `Game/Battles/States/BattleInitState.cs`, экраны истории.

Ключ: типы событий уже совпадают — клиентский `StoryEventType` (`DefaultEvent`, `Fight`, `ForkEvent`) один в один ложится на серверный `step.eventType` (`default_event`, `fight`, `fork_event`).

| Сервер | Что было на клиенте | Что делать |
| --- | --- | --- |
| `POST /api/run/start` | `StoryFactory` раскладывал уровень | запрос вместо локальной раскладки |
| `run.stages` | локальный список этапов | строить представление из ответа; будущие этапы приходят с `eventId = 0` |
| `POST /api/run/advance` | локальный переход к следующему событию | один запрос на шаг |
| `step.eventType = default_event` | `StoryDefaultEvent` | показать текст по `locKey` |
| `step.eventType = fork_event` + `pendingChoice.kind = fork` | `StoryForkEvent` | выбор ветки → `POST /api/run/choose` с `picks: [1]` или `[3]` |
| `pendingChoice.kind = perk` | `StoryProgressService.EnqueuePerkSelection` | экран перков из `options`, ответ → `/api/run/choose` |
| `step.eventType = fight` | `BattleInitState` → `CreateBattle()` | бой из `step` (этап 4) |
| `run.experience`, `run.experienceLevel` | `CurrentExperience`, `CurrentRunLevel` | только отображение |
| `step.experienceGained`, `step.levelUps` | локальный подсчёт | только отображение |
| `step.appliedRewards` | `ApplyEventRewards` | показать награды, профиль перечитать |
| `run.currentHealth` | локальное здоровье забега | из ответа |
| `run.bonuses` | локальные бонусы забега | из ответа, там же `remainingBattles` |
| `run.status = completed/failed`, `step.runCompleted/runFailed` | `CompleteCurrentStoryLevel` | реакция на ответ |
| `GET /api/run/current` | — | восстановление незакрытого забега после перезапуска |
| `POST /api/run/abandon` | локальный выход из забега | игрок бросает забег, сервер закрывает его |

Остаётся локальным: тексты и локализация, движение персонажа между этапами, экраны, префабы, спайны, звук, ECS-презентация — всё, что не влияет на числа.

Удаляется: локальная раскладка этапов, локальный подсчёт опыта и уровней, локальное применение наград, локальная очередь выбора перков, локальное здоровье забега.

Готово, когда: забег проходится от старта до завершения без локальных решений, а перезапуск игры в середине забега восстанавливает состояние через `GET /api/run/current`.

## Этап 4. Бой по сиду

Файлы: `Game/Battles/States/BattleInitState.cs`, `Game/Battles/Services/BattleService.cs`.

1. В запросы забега добавить `"battleDelivery": "seed"`.
2. На шаге с боем брать `step.battleInput` и считать бой ядром: `battleCore.Replay(step.battleInput)`.
3. Сверять `IBattleCore.ComputeDigest(script)` с `step.battleDigest`; расхождение — ошибка, бой не показывать.
4. Убрать вызовы `api/battle/simulate` и `api/battle/replay`: эти ручки остаются инструментом разработки и при включённой авторизации доступны только по админ-ключу.

Готово, когда: бой играется без запроса скрипта, дайджест совпадает. Выигрыш: на длинном бое 236 байт вместо 3.9 КБ после gzip.

## Этап 5. Убрать дубли: что удаляется и чем заменяется

После подключения DLL часть клиентских типов дублирует наши. Пока живут обе копии, числа enum'ов и поля DTO можно рассинхронизировать руками — поэтому дубли удаляются, а код переводится на типы из DLL.

### Модели протокола — удаляются

Файлы в `Assets/Scripts/Game/Battles/Models/`:

| Удалить | Заменяется на |
| --- | --- |
| `CommandType.cs` | `Server.Battles.CommandType` |
| `OutcomeType.cs` | `Server.Battles.OutcomeType` |
| `BattlePhaseType.cs` | `Server.Battles.BattlePhaseType` |
| `BattleSide.cs` | `Server.Battles.BattleSide` |
| `BattleCommand.cs`, `IBattleCommand.cs` | `Server.Battles.BattleCommand` (в DLL это класс, интерфейса нет) |
| `BattleStep.cs`, `IBattleStep.cs` | `Server.Battles.BattleStep` (тоже класс) |
| `BattleScriptResponse.cs`, `IBattleScriptResponse.cs` | `Server.Battles.BattleScriptResponse` / `IBattleScriptResponse` |
| `BattleReplayData.cs`, `IBattleReplayData.cs` | `Server.Battles.BattleReplayData` / `IBattleReplayData` |
| `BattleSimulationData.cs`, `IBattleSimulationData.cs` | `Server.Battles.BattleSimulationData` / `IBattleSimulationData` |
| `UnitSnapshot.cs`, `IUnitSnapshot.cs` | `Server.Battles.UnitSnapshot` / `IUnitSnapshot` |
| `TeamSnapshot.cs`, `ITeamSnapshot.cs` | `Server.Battles.TeamSnapshot` / `ITeamSnapshot` |
| `EquipmentSnapshot.cs`, `IEquipmentSnapshot.cs` | `Server.Battles.EquipmentSnapshot` / `IEquipmentSnapshot` |
| `BonusGrantSnapshot.cs`, `IBonusGrantSnapshot.cs` | `Server.Battles.BonusGrantSnapshot` / `IBonusGrantSnapshot` |

Важная деталь: шаг и команду в DLL представляют **классы**, а не интерфейсы. Код проигрывателя, типизированный на `IBattleStep` и `IBattleCommand`, переводится на `BattleStep` и `BattleCommand`.

Настройки сериализации тоже берутся из DLL — `Server.Battles.BattleJsonSettingsFactory`; свои `JsonSerializerSettings` для протокола не нужны.

### Enum'ы механик — удаляются

| Удалить | Заменяется на |
| --- | --- |
| `Game/Perks/Models/PerkType.cs` | `Server.Perks.PerkType` |
| `Game/Statuses/Models/StatusType.cs` | `Server.Statuses.StatusType` |
| `Game/Bonuses/Models/BonusType.cs` | `Server.Bonuses.BonusType` |
| `Game/Bonuses/Models/BonusOperatorType.cs` | `Server.Bonuses.BonusOperatorType` |
| `Common/RarityType.cs` | `Server.Common.RarityType` |

Это то место, где расхождение чисел опаснее всего: enum'ы протокола и механик должны совпадать с сервером бит в бит.

### Чтение конфигов — можно удалить свой слой

`IConfigDistributor` из DLL отдаёт все мапперы: персонажи, мобы, саммоны и их уровни, мастерство, экипировка, перки и группы перков, статусы, скиллы, сюжетные уровни, этапы и события, константы, бонусы, паттерны опыта. Клиентские мапперы и менеджеры конфигов (`Game/*/Configs`, `Game/*/Managers`) после этого дублируют DLL и удаляются — это самый большой по объёму, но и самый механический шаг. Делать его стоит последним и по одному домену за раз.

### Сервисы боя и истории: что трогаем, что нет

В `Game/Battles/Services` 24 файла, меняются два:

| Файл | Что с ним |
| --- | --- |
| `BattleService` | путь получения скрипта: вместо запроса к серверу — `IBattleCore.Replay(step.battleInput)` |
| `BattleSimulationDataBuilder` | **удаляется**: состав боя больше не собирает клиент, его строит сервер и присылает в `step.battleInput` |

Остальные 22 остаются как есть — это презентация: `BattleUnitViewService`, `BattleUnitRegistry`, `BattleProjectileService`, `BattleStatusVfxService`, `BattleFlytextService`, иконки бонусов, перков и статусов, `BattlePreloadKeysBuilder`.

В `Game/Stories/Services` 30 файлов, меняются три: `StoryFactory` (раскладка уровня уходит), `StoryProgressService` (становится отображением серверных чисел), `EventLifecycleService` (событие приходит из `step`). Остальные — движение персонажа, локации, нарратив, экраны, вьюхи — остаются: они не решают числа.

Инсталлеры: `ISharedCore` биндится там, где доступен источник конфигов, — в `Core/Installers/ConfigInstaller.cs` (`IConfigSource` внутренний для сборки `Core`).

### Остаётся клиентским

| Файл или модуль | Почему остаётся |
| --- | --- |
| `BattlePreloadKeys`, `BattlePreloadManifest`, `IBattlePreloadKeys`, `IBattlePreloadManifest` | предзагрузка ассетов, к протоколу не относится |
| `BattleFlags`, `BattleLeaveType`, `TurnSide`, `FlytextStyle`, `FlytextType` | клиентская презентация, в DLL таких понятий нет |
| `BattleProtocol.Version` | остаётся, но сверяется с `protocolVersion` из скрипта: расхождение — требование обновить клиент |
| `Game/Perks`, `Game/Statuses`, `Game/Skills`, `Game/Bonuses` (компоненты, системы, UI, парсеры визуала) | презентация механик, расчётов там нет |
| `Game/Common/Services/RandomService` | случайность для визуала, в бою не участвует |
| `PreloadManifest` и его построение по скрипту | предзагрузка ассетов, в DLL такого понятия нет |

### Локальные расчёты, которые можно выключить

`Game/Characteristics/Services/CharacteristicHudService.RefreshFromServer` ходит на сервер за характеристиками. Их можно считать локально: `IBattleCore.BuildCharacteristics(...)` даёт те же 21 значение, что и `GET /api/player/characteristics`, — числа совпадают до бита, потому что считает один и тот же код. Сетевой вызов при этом исчезает.

## Как разработчику обнулить себя

`POST /api/player/reset` со своим токеном — профиль, забеги и журнал выдач стираются, аккаунт остаётся, профиль создаётся заново и сразу получает стартовый набор из `start_content`, то есть аккаунт после сброса играбельный. Удобное место для кнопки — дев-меню клиента (`Assets/Scripts/Cheats`), рядом с остальными читами: нажал, перечитал профиль, играешь с нуля.

Полное удаление аккаунта — `DELETE /api/player/account`, тоже своим токеном; после него вход по тому же `deviceId` создаёт новый аккаунт. Это же требование сторов на удаление данных.

Оба вызова не зависят от того, чем идентифицируется игрок: сейчас `deviceId`, потом платформенный аккаунт — личность берётся из токена, ручки не меняются.

## Сетевые режимы и ошибки

Забег и профиль требуют сети, поэтому клиенту нужны понятные реакции:

| Ответ | Что делать |
| --- | --- |
| `401` | один refresh, затем повтор; если снова `401` — заново `device`-логин |
| `409` | перечитать профиль или забег (`GET /api/run/current`) и повторить действие с тем же `requestId` |
| `503` | конфиги на сервере не загружены: показать «сервис недоступен», повторить позже |
| `404` на `/api/run/current` | активного забега нет, это норма |
| сеть недоступна | забег начать нельзя; незакрытый забег восстановится через `GET /api/run/current`, когда сеть вернётся |

Каждое мутирующее действие отправлять с одним и тем же `requestId` до получения ответа: повтор не применит операцию дважды (окно 48 часов).

## Порядок и риски

Порядок обязателен: 1 → 2 → 3 → 4, пятый в любой момент после четвёртого.

- Этап 3 требует сети: серверный забег означает, что играть офлайн нельзя. Решение по офлайну — за владельцем.
- Аккаунт сейчас только по устройству: смена устройства теряет прогресс, пока не сделана привязка к платформе.
- До живых игроков нужно включить бэкапы Mongo: сейвы есть, страховки нет.
- Конфиги нельзя обновлять у клиента посреди забега: забег запинен на `configVersion`, иначе бой разойдётся. Обновление бандла — между забегами.

## See Also

- [Client API](client-api.md) — ручки, запросы, ответы
- [Battle Core in Unity](battle-core-in-unity.md) — подключение и обновление DLL ядра
- [Runs](runs.md) — серверная модель забега
- [Player State](player-state.md) — профиль, идемпотентность, конфликты
