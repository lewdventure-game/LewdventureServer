[← Battle API](battle-api.md) · [Server index](README.md) · [Back to Documents](../README.md)

# Config Sync

Загрузка игровых таблиц из Google Sheets в runtime-индексы сервера.

## Summary

| | |
| --- | --- |
| Endpoint | `POST /api/config/update` |
| Header | `X-Config-Secret` |
| Owner | `GameConfigService` → `IConfigDistributor` |
| Log tag | `[Config]` |

## Источник правды

1. **Колонки и значения Google Sheets — канон.**
2. JSON-экспорт клиента/парсеров — вторичен. При конфликте верим таблице.
3. Компактные числовые enum в стороннем JSON (например Bonuses `bonus_type: 1..7`) **не совпадают** с серверным `BonusType`. Sheets отдаёт строки (`max_health_perk`) → `StringEnumConverter` + snake_case.
4. Пустая таблица (сейчас Equipments) — не ошибка: count = 0, sync продолжается.

## Managers

| Тип | Base | API | Пример |
| --- | --- | --- | --- |
| List | `BaseManager<T>` | `Add(T)`, `Collection`, `Clear`, linear `TryGet` | Characters, Perks, StoryLevels |
| Dictionary | `BaseDictionaryManager<TKey,TValue>` (не list) | `Add(key, value)`, `TryGet`, `Clear`, `Count` | Bonuses |

List managers удобны для enumeration / будущего Unity-view. Hot-path id lookup бонусов — dictionary.

DI: runtime доступ к конфигам **только** через `IConfigDistributor`. Отдельные `I*MapperManager` в `Program.cs` не регистрируются.

## Sheets → domains

| Sheet | Manager property | Notes |
| --- | --- | --- |
| Constants | `Constants` | keyed by `constant_name` |
| Characters | `Characters` | `is_melee`; `start_bonus_type` = **bonus id**; `upgrade_costs` / `upgrade_bonus_types` / `upgrade_bonus_values` — списки `;` (или скаляр → один элемент); апгрейд `i` = `costs[i]` + `types[i]` + `values[i]` |
| Bonuses | `Bonuses` | dictionary by id; `work_mode` — сырая строка на mapper, parse через `BonusWorkModeParser` |
| Statuses | `Statuses` | |
| Summons | `Summons` | `breakout_multis`, `bonus_mastery_levels`, `bonus_types`, `is_melee`, `attack_cooldown`, `skill_ids` |
| Summon_levels | `SummonLevels` | load-only для боя |
| Mastery | `Masteries` | composite key id+level |
| Enemies | `Enemies` | `is_melee`; `skill_ids`; flat client columns **or** packed `other_characteristics` (`key:value;...`); packed overrides flat when non-empty; dual combo keys + legacy `combo_multiplier` |
| Equipments | `Equipments` | может быть пусто; `equip_bonus_type_*` = bonus **id** или имя `BonusType`; `equip_bonus_values_*` по уровню через `,` или `;`; `is_melee`; `skill_id` → inject known energy/skill into character build (unknown skipped) |
| Trainings | `Trainings` | stub: sheet id не wired, manager empty после sync |
| Artifacts | `Artifacts` | stub: sheet id не wired |
| Aspects | `Aspects` | stub: sheet id не wired |
| Story_levels | `StoryLevels` | `enemies_attack_multiplier`, `enemies_health_multiplier` |
| Story_stages | `StoryStages` | `enemy_stats_multiplier` применяется при `stageId > 0` |
| Story_events | `StoryEvents` | load-only |
| Exp_levels_patterns | `ExperienceLevelPatterns` | load-only |
| Perks | `Perks` | |
| Skills | `Skills` | `C:F`; `id`, `type`, `proc_order`, `parameters`. Factory maps `type` → class. `id=2` is `summon_1_skill_1`, **not** energy. Energy row missing → equipment `skill_id=energy` skipped |
| Perk_groups | `PerkGroups` | load-only |

## work_mode

Парсер: `BonusWorkModeParser` (`[Config]` logs на sync; `ParseCore` без логов в battle consumers).

Поддерживаемые строки: `permanent`, `end_of_game`, `end_of_battle`, `every_turn`, `next_battles:N`, `first_turns:N`, `if_equipped:entity:id`.

Mapper хранит только `work_mode` строку (data-only). Parse — в сервисах, не на mapper.

**Battle apply:** `IBattleBonusService` применяет `operator` + режимы, влияющие на бой (`end_of_battle`, `first_turns:N`, `every_turn`, `if_equipped`).  
Meta (`permanent` / `end_of_game`) в текущем бою живут как уже выданные слои из snapshot/build; полный run-lifecycle — later.
`next_battles:N` внутри simulate: RemainingBattles, active пока > 0, decrement + remove на `OnBattleEnd`.
`if_equipped` проверяется по `UnitState.EquippedEntities` (equipments / characters / summons команды).

## Enemy multipliers

При билде врагов `UnitStateBuilder` умножает health/damage на `Story_levels.enemies_*_multiplier` по `storyLevelId` и на `Story_stages.enemy_stats_multiplier` по `stageId` (если `stageId > 0`).

Клиентский spawn HP обязан использовать **ту же** формулу (`BattleUnitHealthBuilder.BuildEnemyMaxHealth`). Если в `ConfigBundle` нет этих полей — UI baseline = raw Health, а wire `SetHp` будет «прыгать вверх» после первого удара. Канон значений — Sheets на сервере (пример stage 4 → ×1.3 → enemy 10101 max = 130).

Constants: `combo_1_multiplier_base` / `combo_2_multiplier_base`; fallback с legacy `combo_multiplier_base` в оба.

## Logging

- Config sync: `[Config]` Information/Warning/Debug/Error
- Battle consumers: `[Story][Battle]`
- Missing story/constant: `[Error][Story][Battle]`

## See Also

- [Battle API](battle-api.md)
- [Battle simulation spec](../../../.ai-factory/specs/battle-simulation.md)
- [Architecture](../../../.ai-factory/ARCHITECTURE.md)
