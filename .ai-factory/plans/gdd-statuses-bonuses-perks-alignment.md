# Implementation Plan: GDD statuses / bonuses / perks alignment

Branch: master (`git.create_branches: false` — ветку не создаём)
Created: 2026-08-05

## Settings

- Testing: no
- Logging: verbose — однострочные `_logger.LogDebug/Information/Warning/Error($"...");`, пробелы вокруг `=`, теги `[Story][Battle]` / `[Config]`
- Docs: yes — обязательный checkpoint через `/aif-docs` (`Assets/Documents/Server/battle-api.md`, `config-sync.md`, при необходимости `.ai-factory/specs/battle-simulation.md`)
- Roadmap Linkage: Milestone `"none"` — `ROADMAP.md` отсутствует; Rationale: skipped

## Source of truth

1. `Assets/Documents/GDD/03-statuses.md`, `04-characteristics-bonuses.md`, `05-perks.md`, battle-часть `08-triggers-rewards.md`
2. Google Sheets колонки/значения (через текущий sync) — wire-формат строк
3. Код — подогнать под 1+2; заглушки и flat-мутации выкинуть

Конфликт GDD-прозы vs Sheets: **механика** из GDD, **формат ячеек** из Sheets.

## Inventory notes (Task 1)

Источник сверки: клиентский экспорт `D:\Project\Lewdventure\Assets\Configs\Game\Json\` (зеркало Sheets).

| Область | Канон |
| --- | --- |
| Status `parameters` | `key:value;key:value` (`damage_ratio`, `damage_length`, `max_stacks`, `bonuses:[...]`) |
| Status types in data | 1–5 only (burning…bonus_change). **Resurrection status отсутствует** → status-path удалить; resurrection только perk |
| `status_target` | `1=caster`, `2=enemy` (bonus_change=caster; DoT=enemy) |
| Perk `perk_parameters` | `key:value;...`; elemental + action + resurrection perk |
| Perk type 6 | Sheets wire: `action_reward` (`ActionReward` + snake_case); GDD prose name `reward_on_target_action`; numeric compact JSON = 6 |
| Bonuses `work_mode` | string: `permanent`, `end_of_battle`, `first_turns:N`, `if_equipped:...`, … |
| Bonuses `operator` | `1=add`, `2=replace` |

## Scope

**In:** runtime характеристик/бонусов, статусы, перки, battle rewards `bonus`/`status`.

**Out:** meta run/account rewards, shield, deep `06`/`07`, Unity, tests.

## Tasks

### Phase 0: Инвентарь Sheets ↔ GDD ↔ код

- [x] **Task 1: Сверка живых строк Statuses / Bonuses / Perks**
  - Logging: `[Config]` DEBUG summary; ERROR на неизвестные enum/raw.
  - Depends: none

### Phase 1: Характеристики и бонусы (GDD 04)

- [x] **Task 2: Runtime buckets + calculator (формулы 1, 2, 5, 6, 7)**
- [x] **Task 3: `IBattleBonusService` — apply/remove + operator + battle work_mode**
- [x] **Task 4: Vampyrism heal-back в damage pipeline**

### Phase 2: Статусы (GDD 03)

- [x] **Task 5: Парсер parameters + apply с `status_target`**
- [x] **Task 6: Tick/expire presentation по GDD**

### Phase 3: Перки + rewards (GDD 05 / 08)

- [x] **Task 7: Сверка perk mechanics с Sheets+GDD**
- [x] **Task 8: Reward pipeline — battle-relevant полная семантика bonus**

### Phase 4: Docs

- [x] **Task 9: Docs + spec sync**

## Commit Plan

- **Commit 1** (Tasks 1–3): `feat(battle): layered characteristics and battle bonus work modes`
- **Commit 2** (Tasks 4–6): `feat(battle): status_target and GDD status tick/bonus fidelity`
- **Commit 3** (Tasks 7–8): `feat(battle): align perk and reward apply with GDD`
- **Commit 4** (Task 9): `docs(battle): sync status bonus perk fidelity`
