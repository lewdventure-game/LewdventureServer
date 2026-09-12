# Battle Simulation Spec (Server)

> Контракт headless симуляции боя. Синхронизировать с Unity-клиентом Lewdventure при изменении protocol.

## Endpoint

- **POST** `/api/battle/simulate`
- **Body:** `BattleSimulationData` (JSON, camelCase)
- **Response:** `BattleScriptResponse`

- **POST** `/api/battle/replay`
- **Body:** `BattleReplayData` (JSON, camelCase) — те же поля, что `BattleSimulationData`, плюс обязательный `seed`
- **Response:** `BattleScriptResponse` (тот же формат; `seed` в response = переданный)

## Request (`BattleSimulationData`)

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `teamA` | `TeamSnapshot` | yes | Атакующая сторона (игрок всегда A в начале боя) |
| `teamB` | `TeamSnapshot` | yes | Защищающая сторона |
| `storyLevelId` | `int` | no | ID уровня → сервер берёт `maxTurns` и enemy level multipliers из story config |
| `stageId` | `int` | no | ID стадии → `StoryStage.EnemyStatsMultiplier` применяется к health/damage врагов вместе с level multipliers; `0` / отсутствует = множитель стадии не применяется |

**Не передаём в `/api/battle/simulate`:** `seed`, `maxTurns`, полные характеристики юнитов, профиль аккаунта, валюты, инвентарь.

Seed RNG для `/api/battle/simulate` генерирует только сервер и возвращает в response. Для проверки/replay используй `/api/battle/replay` с тем же snapshot и `seed` из прошлого response.

## Request (`BattleReplayData`)

Те же поля, что `BattleSimulationData`, плюс:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `seed` | `ulong` | yes | Seed RNG из предыдущего `BattleScriptResponse` |

Одинаковый snapshot + одинаковый `seed` → бит-в-бит тот же script (при тех же загруженных конфигах).

### `TeamSnapshot`

| Field | Type | Description |
| --- | --- | --- |
| `mainUnits` | `UnitSnapshot[]` | Главные юниты стороны (персонаж / мобы) |
| `summons` | `UnitSnapshot[]` | Саммоны (слоты 1→2→3) |

### `UnitSnapshot`

| Field | Type | Description |
| --- | --- | --- |
| `id` | `int` | ID сущности в конфиге (character / enemy / summon) |
| `level` | `int` | Уровень сущности (character upgrade tier / summon level / enemy level) |
| `masteryLevel` | `int` | Уровень мастерства саммона; для non-summons = `0` |
| `equipments` | `EquipmentSnapshot[]` | Экипировка с уровнем. Клиент шлёт это имя. Alias `equipment` принимается сервером |
| `equipmentIds` | `int[]` | Legacy: только id; сервер трактует как `equipments` с `level = 1` |
| `trainingLevel` | `int` | Уровень тренировки (player main); `0` если нет |
| `artifactIds` | `int[]` | Активные артефакты |
| `aspectIds` | `int[]` | Активные аспекты |
| `activePerkIds` | `int[]` | Активные перки на момент боя |
| `activeSkillIds` | `string[]` | Активные скиллы (ключи из конфига) |
| `activeStatusIds` | `int[]` | Стартовые статусы (id из Statuses) |
| `activeBonuses` | `{ id, count, remainingBattles }[]` | Run-bonuses персонажа A из story ledger. Саммоны/враги — `[]`. `remainingBattles`: `0` = не сжигать по боям (`permanent` / `end_of_game` / `first_turns` / `every_turn`); `> 0` = осталось боёв (`end_of_battle` / `next_battles` / fork `RewardLenght`) |
| `slotIndex` | `int` | Позиция в команде (2 моба / слоты саммонов) |

### `EquipmentSnapshot`

| Field | Type | Description |
| --- | --- | --- |
| `id` | `int` | ID экипировки в конфиге |
| `level` | `int` | Уровень предмета (1-based индекс в `equip_bonus_values_*`) |

Snapshot — вход для серверной сборки `UnitState`. Итоговые HP/DMG/crit/evasion/combo/counter/energy сервер считает из constants + character start/upgrades + training + equipment(level) + summons mastery + artifacts + aspects + perks/statuses/runtime bonuses + snapshot `activeBonuses` (`IBattleBonusService.Grant`, sourceKey `run:{id}:{index}`).

Если на клиенте нет модуля инвентаря/тренировок — `equipments` / `artifactIds` / `aspectIds` пустые, `trainingLevel = 0`. Не слать фейковые id. `activePerkIds` / `activeStatusIds` — из рантайма. `activeSkillIds` — из конфига сущности (character `skill_ids` как строки, summon `skill_id`). `activeBonuses` — только у attacking character. Неизвестный bonus id: WARN + skip, simulate не валится. Сервер всё равно инжектит `ICharacterMapper.SkillIds` и summon `SkillId` из своих конфигов.

`level` / `masteryLevel` на клиенте сейчас часто `1` / `0`: у `ICharacter`/`ISummon` нет runtime mastery/level UI. Не выдумывать.

**Validate fail logging:** при ошибке валидации сервер пишет `[Config]` INFO summary новых полей snapshot (`stageId`, `masteryLevel`, `trainingLevel`, counts `equipment` / `artifactIds` / `aspectIds`) вместе с причиной отказа.

## Response (`BattleScriptResponse`)

| Field | Type | Description |
| --- | --- | --- |
| `protocolVersion` | `int` | Версия протокола (сейчас `1`) |
| `seed` | `ulong` | Seed RNG, сгенерированный сервером |
| `outcomeType` | `OutcomeType` as int | Результат боя (`Unknown=0`, `TeamAWin=1`, `TeamBWin=2`, `Timeout=3`, `Draw=4`) |
| `steps` | `List<BattleStep>` | Пошаговый script |

Число отыгранных ходов клиент берёт из `steps` (`max(turn)`, либо `0` если script пуст). Отдельного поля `turnsPlayed` нет.

## `BattleOutcome`

| Value | Meaning |
| --- | --- |
| `Unknown = 0` | Не задан |
| `TeamAWin = 1` | Победа стороны A |
| `TeamBWin = 2` | Победа стороны B |
| `Timeout = 3` | Лимит ходов исчерпан, враги живы |
| `Draw = 4` | Ничья (недокументировано в GDD; зарезервировано последним значением) |

## `BattleStep`

| Field | Description |
| --- | --- |
| `index` | Порядковый номер шага в script |
| `turn` | Номер хода |
| `phase` | `BattlePhaseType` |
| `actorId` | ID действующего юнита (config id из snapshot) |
| `targetId` | ID цели (config id); **`-1`** если цели нет |
| `commands` | Список `BattleCommand` для клиентской презентации |

### Правила `actorId` / `targetId`

- Значения = `UnitSnapshot.id` из request (не ECS entity id клиента).
- Клиент при спавне строит lookup `id → entity` и играет команды по этим id.
- Если у шага нет внешней цели (cooldown, self-buff без target и т.п.) — `targetId = -1`.
- В `parameters` конкретных команд (`ShowDamage`, `Approach`, …) `targetId` обязан быть валидным id, если операция подразумевает цель.

## `BattleCommand`

| Field | Description |
| --- | --- |
| `commandType` | `CommandType` enum as int (`Wait=1` … `GrantReward=20`) |
| `parameters` | `JObject` — параметры команды для клиента |

### `CommandType` → `parameters`

| CommandType | Parameters keys | Notes |
| --- | --- | --- |
| `Wait` | `seconds` | Пауза между фазами / cooldown |
| `Approach` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `isMelee` | Подход melee к цели |
| `ReturnToPosition` | `actorId`, `actorSlotIndex` | Возврат на позицию |
| `PlayAnimation` | `actorId`, `actorSlotIndex`, `animationKey` | Анимация атаки / каста / hit |
| `ShowDamage` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `damage`, `isCritical`, `isEvaded` | Flytext урона |
| `ShowHeal` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `heal` | Flytext лечения |
| `ShowMiss` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex` | Уклонение |
| `ApplyStatus` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `statusId`, `stacks`, `durationTurns` | Наложение статуса; `durationTurns = -1` если бесконечно |
| `RemoveStatus` | `targetId`, `targetSlotIndex`, `statusId` | Снятие статуса |
| `TickStatus` | `targetId`, `targetSlotIndex`, `statusId`, `value` | Тик статуса (DOT/hot value) |
| `CastSkill` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `skillId` | Каст скилла |
| `TriggerPerk` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `perkId` | Срабатывание перка |
| `SetHp` | `unitId`, `slotIndex`, `hp` | Авторитетное HP после логики |
| `SetEnergy` | `unitId`, `slotIndex`, `energy` | Авторитетная энергия |
| `SetBonus` | `unitId`, `slotIndex`, `bonusId`, `value`, `sourceId` | Изменение бонуса |
| `SpawnUnit` | `unitId`, `slotIndex` | Появление юнита/саммона |
| `DespawnUnit` | `unitId`, `slotIndex` | Убрать юнита со сцены |
| `KillUnit` | `unitId`, `slotIndex` | Смерть юнита |
| `GrantReward` | `rewardType`, `rewardId`, `count`, `targetId` | Meta-награда для презентации (`resource` / `character` / `summon` / `equipment`); аккаунт сервер **не** мутирует. `rewardId` — int или string. `bonus` / `status` через эту команду **не** идут |
| `SetBattleResult` | `outcome` | Финал script (`OutcomeType` as int) |

Клиент **не** решает: крит, уклон, урон, смерть, статусы, исход. Только играет команды по порядку.

### Dual combo multipliers (constants / bonuses / enemy pack)

| Key / bucket | Role |
| --- | --- |
| `combo_1_multiplier_*` / `Combo1Multiplier*` | Множитель Combo1 |
| `combo_2_multiplier_*` / `Combo2Multiplier*` | Множитель Combo2 |
| Legacy `combo_multiplier_*` | Fallback: значение копируется в оба bucket, если раздельные ключи отсутствуют |

Combo2 roll выполняется **только** если Combo1 сработал (шанс прошёл).

## Simulation Loop (high level)

1. Seed RNG (`SeededRandomService`) — сервер генерирует seed.
2. Build `TeamSimulationState` для A и B из snapshot + configs (характеристики, perks, skills, statuses).
3. Emit initial `SpawnUnit` для summons обеих сторон, затем initial `ApplyStatus` для стартовых статусов.
4. Loop `currentTurn` пока `< maxTurns` и у обеих сторон есть живые main:
   - `BeginBattleTurn()` — сбросить abort remaining turn.
   - Turn-start bonuses обеим сторонам.
   - Side A.
   - Если resurrection abort: `++currentTurn`, continue (сторона B этот ход не играет).
   - Если B мёртв: break.
   - Side B.
   - Если A мёртв: break.
   - `++currentTurn`.
5. Battle-end bonuses, outcome, `SetBattleResult`. Живых саммонов в конце **не** despawn'ить.

### Side-turn order (GDD)

Когда сторона становится атакующей:

0. **Turn-start bonuses** уже начислены в начале полного хода (оба стороны), не внутри side-turn.
1. **Statuses** — статусы **main** атакующей стороны по слотам, внутри юнита по `proc_order`; тики одного id суммируются за ход; саммоны в очередь не входят. `Wait(statuses_cooldown)` после каждого сработавшего, включая последнее. Пустая очередь — без wait. Тик, убивший юнита, обрывает его оставшиеся статусы; очередь переходит к следующему живому main или (если живых main нет) остаток хода стороны не выполняется. `SetHp` если статус изменил HP; статус без изменения HP `SetHp` не шлёт.
2. **Perks** — перки атакующей стороны по `proc_order` (доступные на ходе; `proc_rounds` 1-based). `Wait(perks_cooldown)` после каждого сработавшего; пустая очередь — без wait.
3. **Summons** — слоты по возрастанию `slotIndex`: summon skills → SummonAttack (крит от живого main с мин. `slotIndex`) → Return если melee → `summons_cooldown`. Trailing Wait после фазы.
4. **Main unit(s)** (все живые main стороны; два моба — оба ходят):
   1. Unit skills (не energy) → `units_cooldown` внутри skill steps.
   2. NormalAttack (крит/уклон) → energy gain при hit.
   3. Counter → Combo1 → Counter → Combo2 → Counter **только после hit** обычной атаки. Miss → Return + energy skill без цепочки. Контр не триггерит контр. Combo2 только если Combo1 прошло.
   4. ReturnToPosition.
   5. EnergySkill, если `Energy >= MaxEnergy` (без крита, с уклоном, без контратаки) → сброс energy.

Цель атак/скиллов/саммонов: живой `mainUnits` защитника с минимальным `slotIndex`.

**Resurrection:** `TryResurrectOnDeath` ставит `ShouldAbortRemainingTurn`. Остаток **полного** хода пропускается (не только текущей стороны). Сброс флага — `BeginBattleTurn` в начале следующей итерации, не в `BeginSideTurn`.

## Example JSON (минимальный 1v1)

### Request

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
        "artifactIds": [1],
        "aspectIds": [],
        "activePerkIds": [3],
        "activeSkillIds": ["fireball"],
        "activeStatusIds": [],
        "slotIndex": 0
      }
    ],
    "summons": [
      {
        "id": 10,
        "level": 3,
        "masteryLevel": 2,
        "equipments": [],
        "trainingLevel": 0,
        "artifactIds": [],
        "aspectIds": [],
        "activePerkIds": [],
        "activeSkillIds": [],
        "activeStatusIds": [],
        "slotIndex": 1
      }
    ]
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
        "slotIndex": 0
      }
    ],
    "summons": []
  },
  "storyLevelId": 42,
  "stageId": 7
}
```

### Response (фрагмент)

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
      "targetId": 201,
      "commands": [
        { "commandType": 2, "parameters": { "actorId": 1, "actorSlotIndex": 0, "targetId": 201, "targetSlotIndex": 0, "isMelee": true } },
        { "commandType": 4, "parameters": { "actorId": 1, "actorSlotIndex": 0, "animationKey": "attack" } },
        { "commandType": 5, "parameters": { "actorId": 1, "actorSlotIndex": 0, "targetId": 201, "targetSlotIndex": 0, "damage": 42, "isCritical": false, "isEvaded": false } },
        { "commandType": 13, "parameters": { "unitId": 201, "slotIndex": 0, "hp": 58 } },
        { "commandType": 3, "parameters": { "actorId": 1, "actorSlotIndex": 0 } },
        { "commandType": 1, "parameters": { "seconds": 0.3 } }
      ]
    },
    {
      "index": 1,
      "turn": 1,
      "phase": "UnitCooldown",
      "actorId": 1,
      "targetId": -1,
      "commands": [
        { "commandType": 1, "parameters": { "seconds": 0.2 } }
      ]
    }
  ]
}
```

## Decisions

Зафиксированные решения контракта. Менять только осознанно и синхронно с Unity-клиентом.

| ID | Решение | Статус |
| --- | --- | --- |
| D1 | Response = `BattleScriptResponse` + упорядоченные `steps` / `commands` | Accepted |
| D2 | Request несёт **thin snapshot** (id/level/mastery/equipment+level/training/artifacts/aspects/perks/skills/statuses/slot + optional `stageId`); характеристики считает сервер | Accepted |
| D11 | Meta-награды (`resource` / `character` / `summon` / `equipment`) эмитятся как `GrantReward` в script; аккаунт не мутируется. `bonus` / `status` — через существующие battle commands | Accepted |
| D12 | Combo1 и Combo2 — раздельные множители; legacy singular `combo_multiplier_*` → fallback в оба. Combo2 только после успешного Combo1 | Accepted |
| D3 | `maxTurns` только на сервере (из `storyLevelId` / story config); клиент не шлёт `maxTurns` | Accepted |
| D4 | `seed` в `/api/battle/simulate` генерирует только сервер; в request simulate **нет**. Replay/проверка: `POST /api/battle/replay` с тем же snapshot + `seed` | Accepted |
| D5 | Identity = `(configId, slotIndex)`: step fields `actorId`/`actorSlotIndex`, `targetId`/`targetSlotIndex`; commands carry the same for unit ops (`KillUnit`/`SetHp`/…). Config id alone is ambiguous when team has duplicate enemy ids | Accepted |
| D6 | Цель при 2 мобах: живой `mainUnits` с **минимальным** `slotIndex` | Accepted (временно до GD) |
| D7 | Legacy Unity `BattleEvent` / `BattleActionType` — deprecate; playback мигрирует на command executor | Accepted |
| D8 | Поля `turnsPlayed` нет; клиент считает ходы из `steps[].turn` | Accepted |
| D9 | Нет цели → `targetId = -1` (не `0`) | Accepted |
| D10 | `Draw` = ничья, **последнее** значение enum (`= 4`); в GDD пока не описано | Accepted |

### D6 — цель при нескольких main units

Текущее серверное правило: живой `mainUnits` с минимальным `slotIndex`.  
TODO GD: подтвердить (focus-fire / lowest HP / иное) и обновить при изменении.

## Extension Recipe

1. Добавить фазу в `BattlePhaseType` (если нужна новая фаза).
2. Добавить значение в `CommandType` (если нужна новая клиентская команда).
3. Реализовать logic в `BattleSimulatorService` или dedicated service.
4. Обновить этот spec и клиентский battle spec в Lewdventure.
5. Добавить unit test с фиксированным seed.

## Config Dependencies

Симуляция читает constants, characters, enemies, summons, equipments, perks, statuses, bonuses, masteries, story levels через `IConfigDistributor`.
Skills: thin registry по `SkillType` / string id (`fireball`); полный Skills sheet — later.
Перед тестами configs должны быть загружены (`/api/config/update` или test fixture).

Источник правды конфигов — колонки Google Sheets (не клиентский JSON-экспорт). Подробности: [`config-sync.md`](../../Assets/Documents/Server/config-sync.md).

Story level: `enemies_attack_multiplier` / `enemies_health_multiplier` применяются к health/damage врагов при build по `storyLevelId`.
Stage: при `stageId > 0` дополнительно умножается `StoryStage.EnemyStatsMultiplier` на health/damage врагов.
Bonus `work_mode` — строка на mapper, parse через `BonusWorkModeParser`; battle apply (`operator` + `end_of_battle` / `first_turns` / `every_turn`) — `IBattleBonusService` + layered `CharacteristicBuckets` rebuild (формулы GDD 1/2/5/6/7; формула 5 со `|raw|` в знаменателе; шансы/АТК_МН = формула 2, не «(1+base+local)» из сырого GDD). Status apply uses `status_target`; damage-over-time stacks aggregate into one presentation tick per `statusId` per turn, queue = attacking mains only.
Training / Artifact / Aspect sheets — data-only mappers + managers; grant в `UnitStateBuilder` из snapshot ids/levels. Пустые sheets = runtime no-op.
