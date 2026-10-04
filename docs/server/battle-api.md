[← Server index](README.md) · [Back to Documents](../README.md) · [Next →](config-sync.md)

# Battle API

Headless симуляция боя: клиент шлёт snapshot сторон, сервер считает истину и возвращает battle script для презентации в Unity.

Полный protocol: [`.ai-factory/specs/battle-simulation.md`](../../.ai-factory/specs/battle-simulation.md).

## Summary

| | |
| --- | --- |
| Endpoint | `POST /api/battle/simulate` |
| Replay | `POST /api/battle/replay` (тот же snapshot + `seed`) |
| Base URL (local) | `http://localhost:5000` |
| Base URL (VPS) | `https://<домен окружения>` через Caddy, см. [deploy-and-rollback](../runbooks/deploy-and-rollback.md) |
| Body | `BattleSimulationData` / `BattleReplayData` (JSON camelCase) |
| Response | `BattleScriptResponse` |
| Auth | нет (сейчас); авторизация игроков запланирована отдельно |
| Limits | rate limit по IP (`RateLimit:Battle`), параллелизм (`RateLimit:BattleConcurrencyLimit`), тело до `RequestLimits:BattleMaxRequestBodyBytes` |

Перед боем конфиги должны быть загружены: сервер берёт активный снапшот (Mongo на VPS, файл локально), см. [config-sync](config-sync.md).

## Коды ответов

| Код | Когда | Тело |
| --- | --- | --- |
| `200` | бой посчитан | `BattleScriptResponse` |
| `400` | битый JSON, пустое тело, неизвестный skill и другие ошибки входных данных | `{"error": "..."}` |
| `413` | тело больше лимита | `{"error":"Request body is too large."}` |
| `429` | превышен rate limit или лимит параллелизма | пустое |
| `500` | необработанное исключение (уходит алерт в Discord) | `{"error": "Внутренняя ошибка сервера"}` |
| `503` | конфиги ещё не загружены | `{"error": "..."}` |

## Flow

```text
Unity (BattleService)
  → собирает thin TeamSnapshot / UnitSnapshot
  → POST /api/battle/simulate
LewdventureServer (BattleSimulatorService)
  → seed RNG, maxTurns из storyLevelId
  → build UnitState (stats/perks/skills/statuses)
  → SpawnUnit summons + initial ApplyStatus
  → side-turn: status → perk → summon → unit skill → attack/counter/combo → energy
  → BattleScriptResponse
Unity (BattlePlayback)
  → играет commands по порядку (без пересчёта логики)
```

## Side-turn phases

| Order | Phase | Notes |
| --- | --- | --- |
| 1 | Statuses | per-main slot ↑, `proc_order` inside unit; `Wait(statuses_cooldown)` after each proc; empty = no wait; death skips rest of that unit |
| 2 | Perks | `proc_order`; `Wait(perks_cooldown)` after each proc; empty = no wait |
| 3 | Summons | slot ↑; skills (`summons_cooldown` after each) → attack if off `attack_cooldown` → Return if melee → `summons_cooldown`; skip attack = no attack wait; empty = no wait |
| 4 | Unit skills | non-energy from `activeSkillIds` (+ equipment `skill_id` known skills); no EndCast Wait |
| 5 | Units cooldown | one `Wait(units_cooldown)` before normal attack |
| 6 | Attack chain | normal → counter → combo1/2 (+ counter); melee Approach/Return on normal/counter/combo; miss → `Wait(battle_flytext_timer)` |
| 7 | Energy skill | only if energy skill owned **and** `Energy >= MaxEnergy`; cast after attack; no crit, no counter |

Rewards in battle: `bonus` / `status` применяются в симуляции; `resource` / `character` / `summon` / `equipment` эмитятся как `GrantReward` (presentation only). Для `resource` поле `rewardId` может быть **string key** или int.

### Melee / range

- Character: `Characters.is_melee` (true → Melee, иначе Range).
- Summon: `Summons.is_melee` (true → Melee, иначе Range).
- Enemy: `Enemies.is_melee` (true → Melee, иначе Range).

### Characteristics / bonuses (battle)

- Итоги считаются из **слоёв** (`CharacteristicBuckets` + `ICharacteristicCalculator`, формулы GDD 1/2/5/6/7), не через flat `+=` на finals.
- Formula 2 multipliers с округлением: `КРИТ_МН`, `КОМБО_1/2_МН`, `КОНТР_МН`, `СПЕЛЛ_МН` (`MathF.Round` после replace).
- Источники build: constants → training → equipment(level) → artifacts → aspects → account и run бонусы (`activeBonuses`) → perks/statuses/runtime. Бонусы мастерства саммонов в бою не считаются: с 2026-10-04 это разовые награды на аккаунт, они приходят через `activeBonuses`.
- Промоуты персонажа бонусов в бою **не** дают: лист `Character_promotes` выдаёт награды на аккаунт в момент прокачки (в том числе `bonus:<id>`), и в бой они приезжают через `activeBonuses` снапшота. В билде юнита остаются константы, тренировки, снаряжение, мастерство саммонов, артефакты, аспекты, перки и статусы.
- Equipment `equip_bonus_type_*`: **bonus id** или техническое имя `BonusType`; `equip_bonus_values_*`: уровни через `,` или `;` (берётся более «длинный» split; при равенстве — `,`).
- `IBattleBonusService` применяет `operator` (`add` / `replace`) и battle `work_mode`: `end_of_battle`, `first_turns:N`, `every_turn`, `next_battles:N` (RemainingBattles, decrement на battle end), `if_equipped`.
- `current_health_local` + `replace` → set HP (clamp MaxHealth); `add` → delta.
- Combo1 / Combo2 — раздельные множители; legacy `combo_multiplier_*` → fallback в оба. Combo2 только после успешного Combo1.
- Vampyrism heal-back только после успешного strike (normal/counter/combo), не на elemental/skills.
- Healing effects: `healing` / `healing_from_max` — разово при grant.
- После miss обычной атаки counter/combo **не** крутятся; energy skill всё равно, если бар полный.
- Если обычка недоступна (`CanUseNormalAttack` = нет) — сразу energy skill, без Approach/Return/counter/combo.
- Melee атакующий: Approach → Wait → удар; комбо без Return; Return только в конце цепочки. Melee-защитник на контре подходит и возвращается на свой слот.

### Elemental perks

- `proc_rounds` пустой → перк не триггерится (Warning на create).
- Fire/Earth: шанс hit_rewards = `rewards_chance` (Z); при наличии poison (fire) / burn (earth) → `Z * debuff_rewards_chance` (A); один apply; clamp chance > 1.
- Air/Water: flat hit_rewards на hit нет; при стаках семьи — ролл Z, затем cleanse всех стаков + `hit_rewards` за каждый снятый.

### Statuses

- `parameters`: `key:value;...` (`damage_ratio`, `flat_value`, `damage_length`, `max_stacks`, `bonuses`).
- `activeBonuses` в snapshot: бонусы аккаунта (`remainingBattles = 0`) + бонусы забега; сервер грантит их через `IBattleBonusService.Grant` с `sourceKey run:{id}:{index}`.
- Apply учитывает `status_target` (`caster` / `enemy`) относительно source/target reward.
- Damage over time: side-wide queue by `proc_order`; tick damage = live `source.Damage * damage_ratio * (1-DEF)`; crit from live source. `sourceUnitId = -1` (статус из награды события или стартовый статус забега) → tick damage = `flat_value * (1-DEF)`, крита нет, roll крита не тратится; нет `flat_value` → Warning и урон 0.
- Non–damage-over-time statuses expire at battle end (`bonus_change` lifecycle).
- Resurrection perk: charges → 0 removes perk; death-time resurrect aborts remainder of current side-turn.

## Request

| Field | Required | Description |
| --- | --- | --- |
| `teamA` | yes | Атакующая сторона (игрок) |
| `teamB` | yes | Защищающаяся сторона |
| `storyLevelId` | no | → `maxTurns` + enemy level multipliers |
| `stageId` | no | → `StoryStage.EnemyStatsMultiplier` (вместе с level multipliers); `0` = без стадии |

**Не слать в simulate:** `seed`, `maxTurns`, HP/DMG/crit и прочие характеристики.

### Replay

`POST /api/battle/replay` — тот же body, что simulate, плюс `seed` из прошлого response. Нужен, чтобы перепроверить script без рандома.

```json
{
  "teamA": { "...": "как в simulate" },
  "teamB": { "...": "как в simulate" },
  "storyLevelId": 42,
  "stageId": 7,
  "seed": 12345678901234567890
}
```

Response тот же `BattleScriptResponse`; поле `seed` = переданный.

### Unit snapshot (thin)

`id`, `level`, `masteryLevel`, `equipments[]` (`{id,level}`; alias `equipment` на сервере), legacy `equipmentIds` → level 1, `trainingLevel`, `artifactIds`, `aspectIds`, `activePerkIds`, `activeSkillIds`, `activeStatusIds`, `activeBonuses[]` (`{id,count,remainingBattles}`), `slotIndex`, `currentHealth`, `skillLevels[]` (уровни скиллов саммона по порядку `Summons.skill_ids`, нет значения — 1).

Клиент без инвентаря шлёт пустые `equipments` / `artifactIds` / `aspectIds` и `trainingLevel = 0`. Не выдумывать id. Перки/статусы/skill ids — из рантайма и загруженных конфигов. Сервер дополнительно инжектит `Characters.skill_ids`, `Enemies.skill_ids` и summon `skill_ids` (int/string → skill id), даже если `activeSkillIds` пустой.

Пример минимального 1v1:

```json
{
  "teamA": {
    "mainUnits": [
      {
        "id": 1,
        "level": 5,
        "masteryLevel": 0,
        "equipments": [
          { "id": 101, "level": 2 },
          { "id": 102, "level": 1 }
        ],
        "trainingLevel": 3,
        "artifactIds": [],
        "aspectIds": [],
        "activePerkIds": [3],
        "activeSkillIds": ["1"],
        "activeStatusIds": [],
        "activeBonuses": [],
        "slotIndex": 0
      }
    ],
    "summons": []
  },
  "teamB": {
    "mainUnits": [
      {
        "id": 201,
        "level": 5,
        "masteryLevel": 0,
        "equipments": [],
        "trainingLevel": 0,
        "artifactIds": [],
        "aspectIds": [],
        "activePerkIds": [],
        "activeSkillIds": [],
        "activeStatusIds": [],
        "activeBonuses": [],
        "slotIndex": 0
      }
    ],
    "summons": []
  },
  "storyLevelId": 42,
  "stageId": 7
}
```

## Response

| Field | Description |
| --- | --- |
| `protocolVersion` | Версия протокола (`1`) |
| `seed` | Seed RNG, сгенерированный **сервером** (replay/debug) |
| `outcomeType` | int `OutcomeType`: `Unknown=0`, `TeamAWin=1`, `TeamBWin=2`, `Timeout=3`, `Draw=4` |
| `steps` | Упорядоченный script |

Ходы: `max(steps[].turn)` (или `0`, если пусто). Поля `turnsPlayed` нет.

Каждый step: `index`, `turn`, `phase`, `actorId`, `targetId` (`-1` если нет цели), `commands[]`.

Каждая command: `commandType` (int enum) + `parameters` (схема ключей — в [battle-simulation.md](../../.ai-factory/specs/battle-simulation.md)).

Фрагмент response:

```json
{
  "protocolVersion": 1,
  "seed": 12345678901234567890,
  "outcomeType": 1,
  "steps": [
    {
      "index": 0,
      "turn": 1,
      "phase": "NormalAttack",
      "actorId": 1,
      "actorSlotIndex": 0,
      "targetId": 201,
      "targetSlotIndex": 0,
      "commands": [
        { "commandType": 2, "parameters": { "actorId": 1, "actorSlotIndex": 0, "targetId": 201, "targetSlotIndex": 0, "isMelee": true } },
        { "commandType": 5, "parameters": { "actorId": 1, "actorSlotIndex": 0, "targetId": 201, "targetSlotIndex": 0, "damage": 42, "isCritical": false, "isEvaded": false } },
        { "commandType": 13, "parameters": { "unitId": 201, "slotIndex": 0, "hp": 58 } }
      ]
    }
  ]
}
```

## Client rules

- Сервер — источник истины. Клиент **не** считает урон, крит, уклон, статусы, исход.
- Identity юнита = `(configId, slotIndex)`. В step: `actorId`/`actorSlotIndex`, `targetId`/`targetSlotIndex`. В commands: те же поля / `unitId`+`slotIndex` для `KillUnit`/`SetHp`/….
- Lookup на клиенте: `(id, slotIndex)` → entity (один config id на команду недостаточен при дублях врагов).
- `targetId = -1` / `targetSlotIndex = -1` — шаг без цели.
- Seed в response simulate — для `/api/battle/replay`, не для локального пересчёта боя на клиенте.
- Legacy `BattleEvent` / `BattleActionType` — deprecate; цель — command playback.

Презентация (анимации, flytext, approach): [`gdd/02-presentation-contract.md`](../gdd/02-presentation-contract.md).  
Handoff для ИИ (клиент целиком): [`gdd/10-client-battle-ai.md`](../gdd/10-client-battle-ai.md).

## Unity touchpoints

| Что | Где (клиент `C:\UnityProjects\Lewdventure`) |
| --- | --- |
| Сборка request / POST | `Assets/Scripts/Game/Battles/Services/BattleService.cs` |
| Локальный симулятор (legacy) | `.../Simulations/BattleSimulatorService.cs` |
| Playback | `.../Simulations/BattlePlaybackSystem.cs` |
| Snapshot | `.../Simulations/UnitSnapshot.cs`, `TeamSnapshot.cs` |

Серверные точки: `src/Lewdventure.Server.Api/Endpoints/BattleEndpoints.cs`, `src/Lewdventure.Server.Battle/Battles/Services/BattleSimulatorService.cs`, DTO в `src/Lewdventure.Server.Contracts/Battles/`.

## Troubleshooting

| Симптом | Что проверить |
| --- | --- |
| `400` unknown skill | `activeSkillIds` должен резолвиться в Skills config (`id` или `type`, напр. `"1"` / `"fireball"`) |
| `maxTurns = 0` / странный outcome | `storyLevelId` есть в загруженных story configs |
| Клиент не играет script | Response format: `steps`/`commands`, не legacy `events` |
| Неизвестный unit id | Snapshot `id` должен существовать в characters/enemies/summons config |
| Нет SpawnUnit | Summons пустые в snapshot / не прошли build |

## Структура симуляции

Порядок хода выражен списком фаз (`IBattleTurnPhase`): статусы, перки, саммоны, действия главных юнитов — как в ГДД §3.2. Новая фаза добавляется объектом в список, симулятор править не нужно.

Состояние боя живёт в `BattleTurnState` (действующий юнит и юниты, чьи действия прерваны воскрешением) и передаётся параметром: в сервисах боя нет изменяемых полей, поэтому бой можно считать параллельно и переносить в другой рантайм.

Сборка юнита разделена: `UnitBucketsFactory` считает базовые характеристики, `UnitBonusGranter` выдаёт бонусы шести источников, `UnitLoadoutBinder` привязывает перки, скиллы и статусы, `UnitStateBuilder` только координирует.

Урон округляется математически в одном месте (`IBattleDamageMath`), множители остаются дробными.

Числовая политика закреплена тестами: в боевом коде запрещены `Pow`, `Sqrt`, тригонометрия, `FusedMultiplyAdd`, `double`, обращения к времени и `Guid` — это условие того, что тот же код даст те же числа в Unity.

Броски случайности именованные: `GetRandomValue` принимает имя броска (`evasion`, `critical`, `combo`, `counter`, `perk_rewards`, `action_reward`), генератор создаётся через `ISeededRandomFactory`. Эталоны `seed-42.rolls.txt` у четырёх кейсов фиксируют трассу бросков: имя и значение каждого броска по порядку. Лишний или пропавший бросок виден сразу и с именем, а не как расхождение урона на десятом шаге.

## Доставка боя клиенту: скрипт или сид

`POST /api/run/advance` и `/api/run/choose` принимают `battleDelivery`. По умолчанию (`script`) в `step.battle` едет весь скрипт. С `battleDelivery = "seed"` вместо скрипта приходит `step.battleInput` — вход боя с сидом, и клиент переигрывает бой сам тем же ядром.

В обоих режимах есть `step.battleDigest` (`sha256` по структуре скрипта, где float берутся битами (`SingleToInt32Bits`), а не текстом) и `step.battleStepCount`. Клиент считает дайджест своего результата и сверяет: совпало — бой идентичен серверному, не совпало — расхождение видно до показа боя, а не по странному урону.

Дайджест намеренно не считается по тексту JSON: .NET Core печатает float кратчайшей формой (`0.047588766`), а Mono в Unity девятью значащими цифрами (`0.0475887656`). Это один и тот же float, поэтому сравнивать надо биты, а не строки.

Сколько это экономит на реальных кейсах (вход против скрипта, gzip):

| Кейс | Вход | Скрипт | Вход gzip | Скрипт gzip | Выигрыш |
| --- | --- | --- | --- | --- | --- |
| Базовый 1v1 | 458 B | 36.9 KB | 180 B | 1.4 KB | 7.9x |
| Три саммона | 986 B | 50.2 KB | 210 B | 1.9 KB | 9.0x |
| Долгий бой до лимита ходов | 691 B | 144.4 KB | 236 B | 3.9 KB | 16.5x |
| Долгая победа | 654 B | 73.8 KB | 242 B | 2.2 KB | 9.1x |

Проверка на стороне сервера: интеграционный тест забега на каждом бою строит второе ядро из тех же конфигов, переигрывает `battleInput` и требует совпадения дайджеста, числа шагов и исхода.

Переключать клиент на `seed` имеет смысл после ручного прогона паритета в Unity: до него мы знаем только, что .NET-клиент воспроизводит бой один в один.

## Расширение боя

Перк, скилл и статус добавляются одним классом-создателем: `IPerkCreator` объявляет `PerkType`, ключ типа из таблицы и сборку перка, `ISkillCreator` — то же для скиллов. Реестры внутри `PerkFactory` и `SkillFactory` строятся из списка создателей, switch в фабриках больше нет; список собирается в `BattleComposition`.

Параметры читает `PerkParameterReader` (ключи, награды, проки, пороги действий), поэтому создатель занимается только своей механикой.

Тест `EffectRegistryConsistencyTests` сверяет ключи создателей с дескрипторами `EffectParameterRegistry`: добавили перк или скилл без описания параметров — тест красный, и геймдизайнер не получит проверку данных при публикации. `SkillCoverageGoldenTests` требует эталонный кейс на каждый известный тип скилла.

Общие операции вынесены в сервисы: `IBattleConstantsReader` (константы боя), `IBattleTeamQuery` (выбор цели и живые юниты), `IStatusClassifier` (DoT-статусы). Копий этих методов по классам больше нет.

## Переносимость ядра в Unity

Три проекта собираются под два таргета сразу: `Lewdventure.Server.Contracts`, `Lewdventure.Server.GameConfig`, `Lewdventure.Server.Battle` — `net10.0;netstandard2.1`. Из зависимостей у них только `Newtonsoft.Json` и `Microsoft.Extensions.Logging.Abstractions`, ASP.NET, Mongo, Google и контейнер внедрения зависимостей остаются в `Api`, `Infrastructure` и `Runs`.

Собранные под `netstandard2.1` DLL кладутся в Unity как есть: клиент получает тот же симулятор, тот же `SeededRandomService` и те же модели команд, поэтому по одному сиду он переигрывает бой один в один.

Что пришлось заменить ради `netstandard2.1`: `record`-типы требуют полифилла `IsExternalInit` (папка `Compatibility` в `Contracts` и `GameConfig`), вместо `Convert.ToHexStringLower` в `ConfigSnapshotHasher` свой перевод байтов в hex, вместо `BitOperations.RotateLeft` в `SeededRandomService` свой сдвиг, вместо `ArgumentOutOfRangeException.ThrowIfLessThanOrEqual` обычная проверка, `float.TryParse` с явными `NumberStyles`, атрибуты `System.ComponentModel.DataAnnotations` из DTO боя убраны.

Сборка под оба таргета идёт в CI на каждом коммите — если в ядро попадёт API, которого нет в Unity, сборка упадёт сразу.

### Как клиент подключает ядро

Ядро отдаёт публичный фасад, поэтому клиенту не нужно ни знать внутренние типы, ни собирать граф сервисов:

```csharp
var result = new SharedCoreFactory().CreateFromBundle(bundleJson, new UnityCoreLog());
var script = result.SharedCore.Battle.Replay(replayData);
```

`SharedCoreFactory` принимает тело `GET /api/config/bundle` как есть или список `CoreConfigDomain`, строит конфиги теми же парсерами и валидаторами, что сервер, и возвращает `ISharedCore`: `ConfigVersion`, `Configs` — полный read-model конфигов для визуала (арты, редкости, перки, статусы, сюжет) и `Battle` (`IBattleCore`: `Simulate`, `Replay`, `TryValidate`, `ComputeDigest`, `BuildCharacteristics` для экранов персонажа). Логирование идёт через `ICoreLog` (в ядре есть `SilentCoreLog`), сериализация — через `BattleJsonSettingsFactory`, те же настройки, что у сервера. Версия конфигов считается тем же sha256, что версия снапшота, поэтому бандл из `GET /api/config/bundle` даёт ядру ровно ту версию, которую забег пинит в `configVersion`.

Граф сервисов боя собирает `BattleComposition` — один конструктор на 28 объектов, без контейнера внедрения зависимостей. Сервер регистрирует в DI его же, поэтому порядок фаз хода и состав сервисов у клиента и сервера не могут разойтись. Остаётся клиентская часть: адаптер `ICoreLog` на `Debug.Log` и `link.xml` для IL2CPP — подробности и примеры в [Battle Core in Unity](battle-core-in-unity.md).

Порядок обхода `Dictionary` между рантаймами не гарантирован, поэтому выбор в боевом коде не должен от него зависеть: в `UnitBonusGranter` бонус по типу выбирается по наименьшему id, а не «первый встреченный». Новые `foreach` по `.Values` и `.Keys` в боевом коде запрещены тестом политики.

## See Also

- [Client Handover](client-handover.md) — один документ для клиентской команды: ядро, ручки, порядок миграции
- [Client API](client-api.md) — все ручки с моделями запросов и ответов для клиента
- [Config Sync](config-sync.md) — снапшоты конфигов, Google Sheets, managers, источник правды
- [Golden-тесты](../runbooks/golden-tests.md) — эталоны ответов боя
- [Battle Simulation Spec](../../.ai-factory/specs/battle-simulation.md) — канон protocol + Decisions
- [Presentation contract](../gdd/02-presentation-contract.md) — обязанности Unity
- [Architecture](../../.ai-factory/ARCHITECTURE.md) — Modular Monolith, battle flow
