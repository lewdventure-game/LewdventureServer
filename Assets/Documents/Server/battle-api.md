[← Server index](README.md) · [Back to Documents](../README.md) · [Next →](config-sync.md)

# Battle API

Headless симуляция боя: клиент шлёт snapshot сторон, сервер считает истину и возвращает battle script для презентации в Unity.

Полный protocol: [`.ai-factory/specs/battle-simulation.md`](../../../.ai-factory/specs/battle-simulation.md).

## Summary

| | |
| --- | --- |
| Endpoint | `POST /api/battle/simulate` |
| Replay | `POST /api/battle/replay` (тот же snapshot + `seed`) |
| Base URL (local) | `http://localhost:5000` |
| Body | `BattleSimulationData` / `BattleReplayData` (JSON camelCase) |
| Response | `BattleScriptResponse` |
| Auth | нет (сейчас) |

Перед боем конфиги должны быть загружены. На старте сервер сам вызывает тот же sync, что и `POST /api/config/update`; ручной POST нужен только для hot-reload без рестарта.

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
| 1 | Statuses | side-wide `proc_order` queue; trailing `statuses_cooldown` Wait always |
| 2 | Perks | `proc_order`; trailing `perks_cooldown` Wait always |
| 3 | Summons | slot ↑; skills → attack; trailing `summons_cooldown` Wait always |
| 4 | Unit skills | non-energy from `activeSkillIds` (+ equipment `skill_id` known skills); no EndCast Wait |
| 5 | Units cooldown | one `Wait(units_cooldown)` before normal attack |
| 6 | Attack chain | normal → counter → combo1/2 (+ counter); melee Approach/Return on normal/counter/combo; miss → `Wait(battle_flytext_timer)` |
| 7 | Energy skill | only if energy skill owned **and** `Energy >= MaxEnergy`; cast after attack; no crit, no counter |

Rewards in battle: `bonus` / `status` применяются в симуляции; `resource` / `character` / `summon` / `equipment` эмитятся как `GrantReward` (presentation only). Для `resource` поле `rewardId` может быть **string key** или int.

### Melee / range

- Character: `UnitFlags.Melee` только если хотя бы один equipped item имеет `is_melee=true`; иначе `Range`.
- Summon: `Summons.is_melee` (true → Melee, иначе Range).
- Enemy: `Enemies.is_melee` как раньше.

### Characteristics / bonuses (battle)

- Итоги считаются из **слоёв** (`CharacteristicBuckets` + `ICharacteristicCalculator`, формулы GDD 1/2/5/6/7), не через flat `+=` на finals.
- Formula 2 multipliers с округлением: `КРИТ_МН`, `КОМБО_1/2_МН`, `КОНТР_МН`, `СПЕЛЛ_МН` (`MathF.Round` после replace).
- Источники build: constants → character start/upgrades → training → equipment(level) → summon mastery/account → artifacts → aspects → perks/statuses/runtime.
- Character upgrades (GDD): `min(level - 1, upgrade_costs.Length)` штук; апгрейд `i` грантит только `upgrade_bonus_types[i]` с value `upgrade_bonus_values[i]` (не спам всех пар на каждый лвл). Start/upgrade value из Characters, не `Bonuses.bonus_value`.
- Equipment `equip_bonus_type_*`: **bonus id** или техническое имя `BonusType`; `equip_bonus_values_*`: уровни через `,` или `;` (берётся более «длинный» split; при равенстве — `,`).
- `IBattleBonusService` применяет `operator` (`add` / `replace`) и battle `work_mode`: `end_of_battle`, `first_turns:N`, `every_turn`, `next_battles:N` (RemainingBattles, decrement на battle end), `if_equipped`.
- `current_health_local` + `replace` → set HP (clamp MaxHealth); `add` → delta.
- Combo1 / Combo2 — раздельные множители; legacy `combo_multiplier_*` → fallback в оба. Combo2 только после успешного Combo1.
- Vampyrism heal-back только после успешного strike (normal/counter/combo), не на elemental/skills.
- Healing effects: `healing` / `healing_from_max` — разово при grant.
- После miss обычной атаки цепочка counter/combo всё ещё крутится; energy gain — только на hit.

### Elemental perks

- `proc_rounds` пустой → перк не триггерится (Warning на create).
- Fire/Earth: шанс hit_rewards = `rewards_chance` (Z); при наличии poison (fire) / burn (earth) → `Z * debuff_rewards_chance` (A); один apply; clamp chance > 1.
- Air/Water: flat hit_rewards на hit нет; при стаках семьи — ролл Z, затем cleanse всех стаков + `hit_rewards` за каждый снятый.

### Statuses

- `parameters`: `key:value;...` (`damage_ratio`, `damage_length`, `max_stacks`, `bonuses`).
- Apply учитывает `status_target` (`caster` / `enemy`) относительно source/target reward.
- Damage over time: side-wide queue by `proc_order`; tick damage = live `source.Damage * damage_ratio * (1-DEF)`; crit from live source; seed `sourceUnitId = -1` → bearer fallback + Warning.
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

`id`, `level`, `masteryLevel`, `equipment[]` (`{id,level}`), legacy `equipmentIds` → level 1, `trainingLevel`, `artifactIds`, `aspectIds`, `activePerkIds`, `activeSkillIds`, `activeStatusIds`, `slotIndex`.

Пример минимального 1v1:

```json
{
  "teamA": {
    "mainUnits": [
      {
        "id": 1,
        "level": 5,
        "masteryLevel": 0,
        "equipment": [
          { "id": 101, "level": 2 },
          { "id": 102, "level": 1 }
        ],
        "trainingLevel": 3,
        "artifactIds": [],
        "aspectIds": [],
        "activePerkIds": [3],
        "activeSkillIds": ["fireball"],
        "activeStatusIds": [],
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
        "equipment": [],
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

## Response

| Field | Description |
| --- | --- |
| `protocolVersion` | Версия протокола (`1`) |
| `seed` | Seed RNG, сгенерированный **сервером** (replay/debug) |
| `outcome` | `TeamAWin` / `TeamBWin` / `Timeout` / `Draw` |
| `steps` | Упорядоченный script |

Ходы: `max(steps[].turn)` (или `0`, если пусто). Поля `turnsPlayed` нет.

Каждый step: `index`, `turn`, `phase`, `actorId`, `targetId` (`-1` если нет цели), `commands[]`.

Каждая command: `operation` + `params` (схема ключей — в [battle-simulation.md](../../../.ai-factory/specs/battle-simulation.md)).

Фрагмент response:

```json
{
  "protocolVersion": 1,
  "seed": 12345678901234567890,
  "outcome": "TeamAWin",
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
        { "operation": "Approach", "params": { "actorId": 1, "actorSlotIndex": 0, "targetId": 201, "targetSlotIndex": 0, "isMelee": true } },
        { "operation": "ShowDamage", "params": { "actorId": 1, "actorSlotIndex": 0, "targetId": 201, "targetSlotIndex": 0, "damage": 42, "isCritical": false, "isEvaded": false } },
        { "operation": "SetHp", "params": { "unitId": 201, "slotIndex": 0, "hp": 58 } }
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

Презентация (анимации, flytext, approach): [`GDD/02-presentation-contract.md`](../GDD/02-presentation-contract.md).  
Handoff для ИИ (клиент целиком): [`GDD/10-client-battle-ai.md`](../GDD/10-client-battle-ai.md).

## Unity touchpoints

| Что | Где (клиент `D:\Project\Lewdventure`) |
| --- | --- |
| Сборка request / POST | `Assets/Scripts/Game/Battles/Services/BattleService.cs` |
| Локальный симулятор (legacy) | `.../Simulations/BattleSimulatorService.cs` |
| Playback | `.../Simulations/BattlePlaybackSystem.cs` |
| Snapshot | `.../Simulations/UnitSnapshot.cs`, `TeamSnapshot.cs` |

Серверные точки: `Program.cs` (`MapPost`), `Assets/Game/Battles/Services/BattleSimulatorService.cs`, DTO в `Assets/Game/Battles/Models/`.

## Troubleshooting

| Симптом | Что проверить |
| --- | --- |
| `400` unknown skill | `activeSkillIds` должен резолвиться в `SkillType` (`fireball` / `1`) |
| `maxTurns = 0` / странный outcome | `storyLevelId` есть в загруженных story configs |
| Клиент не играет script | Response format: `steps`/`commands`, не legacy `events` |
| Неизвестный unit id | Snapshot `id` должен существовать в characters/enemies/summons config |
| Нет SpawnUnit | Summons пустые в snapshot / не прошли build |

## See Also

- [Config Sync](config-sync.md) — Google Sheets sync, managers, источник правды
- [Battle Simulation Spec](../../../.ai-factory/specs/battle-simulation.md) — канон protocol + Decisions
- [Presentation contract](../GDD/02-presentation-contract.md) — обязанности Unity
- [Architecture](../../../.ai-factory/ARCHITECTURE.md) — Modular Monolith, battle flow
