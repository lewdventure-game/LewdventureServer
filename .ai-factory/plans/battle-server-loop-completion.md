# Implementation Plan: Server battle loop completion (D–G)

Branch: master
Created: 2026-08-05

## Settings

- Testing: no
- Logging: verbose — только `[Story][Battle]`, однострочные `_logger.LogDebug($"...");`, пробелы вокруг `=`
- Docs: yes — после реализации checkpoint через `/aif-docs` (обновить `Assets/Documents/Server/battle-api.md` + spec при необходимости)

## Context

Предыдущий план `.ai-factory/plans/battle-client-server-contract.md`: Tasks 1–12 **done** (серверный контракт + MVP attack/status/validation). Tasks 13–15 (Unity) — **skipped**.

Этот план = только сервер: добить порядок хода по GDD (`01-battle-loop.md`, roadmap Phases D–G).

**Уже есть:** statuses + normal attack + death/outcome + validation + `RewardBonusParser` (только `bonus:`).

**Дыры:** perks/summons закомментированы в `SimulateSideTurn`; counter/combo не эмитятся; skills пустые; единого reward applicator для `status:` нет; `PerkFactory` только FireAttack.

**Стиль (жёстко):** csharp-author-style — нет nullable reference types, нет optional params, нет abbrev, интерфейсы + explicit access, свойства→методы, blank lines между members, trailing newline, логгер первым в ctor.

## Tasks

### Phase 1: Attack chain (counter / combo)

- [x] **Task 1: Counter + Combo1 + Combo2 после normal attack**
  - В `BattleAttackService` после успешного (не miss) normal attack:
    1. Wait(`counterattack_cooldown`) → roll `CounterChance` защитника → при успехе CounterAttack (формула GDD: `ДМГ_защитника * КОНТР_МН * (1-ЗАЩИТА_атакующего)`, крит/уклон; **контратака не триггерит контратаку**).
    2. Wait(`comboattack_cooldown`) → roll `Combo1Chance` → Combo1Attack (формула комбо 1) → снова возможна контратака.
    3. Wait → roll `Combo2Chance` → Combo2Attack → снова возможна контратака.
  - Death через тот же KillUnit/SetHp flow.
  - Один `ISeededRandomService` на весь бой (уже прокинут).
  - **Logging:** `[Story][Battle]` DEBUG rolls (counter/combo/crit/evasion); INFO damage.
  - **Files:** `BattleAttackService.cs`, возможно вынести shared damage helpers
  - **Depends on:** none (MVP attack уже есть)

### Phase 2: Battle rewards pipeline

- [x] **Task 2: Единый** `IBattleRewardService`
  - Парсер reward format: `тип:id:кол-во;...` с bracket-aware split (как status bonuses).
  - Поддержка минимум: `bonus` (через существующий bonus apply), `status` (наложить ActiveStatus стак с max_stacks / tickDamageBase / bonuses).
  - API без optional params; команды `ApplyStatus` / `SetBonus` в переданный `List<BattleCommand>`.
  - Заменить/расширить узкий `RewardBonusParser` или обернуть его — не оставлять мёртвый `ParserUtils.ParseRewardList`.
  - **Logging:** `[Story][Battle]` DEBUG каждый reward entry; WARN unknown type/id.
  - **Files:** `IBattleRewardService.cs`, `BattleRewardService.cs`, `BattleRewardParser.cs` (или аналог), `Program.cs`
  - **Depends on:** status ActiveStatus model (есть)

### Phase 3: Perks (Phase D)

- [x] **Task 3: Perk phase simulator**
  - `IBattlePerkSimulator` / `BattlePerkSimulator`: очередь перков атакующей стороны по `proc_order`.
  - Вставить в `SimulateSideTurn` после statuses, до summons: `SimulatePerks(...)`.
  - Wait `perks_cooldown` между перками.
  - **Logging:** `[Story][Battle]` DEBUG perk id, order, side.
  - **Files:** новые simulator + `BattleSimulatorService.cs`, `Program.cs`
  - **Depends on:** Task 2 (для hit_rewards)

- [x] **Task 4: FireAttack perk runtime + hit_rewards**
  - Расширить `IPerk` / `FireAttackPerk`: проверка `proc_rounds` vs currentTurn; урон `damage_ratio`; projectile_count → команды (PlayAnimation / ShowDamage / SetHp по числу).
  - `hit_rewards` через `IBattleRewardService` с `rewards_chance` / `debuff_rewards_chance` rolls.
  - Цель: минимальный живой main `slotIndex` защитника (как attack).
  - **Logging:** `[Story][Battle]` DEBUG proc, rolls, damage; WARN parse miss.
  - **Files:** `FireAttackPerk.cs`, `IPerk.cs`, `PerkFactory.cs`, perk simulator
  - **Depends on:** Task 3, Task 2

- [x] **Task 5: Остальные** `PerkType` **в factory (не stub-null)**
  - Earth/Air/WaterAttack — тот же каркас, что Fire (отдельные sealed классы или shared elemental base — по стилю проекта: один тип на файл).
  - Reward / ActionReward / Resurrection — apply через `IBattleRewardService` / resurrection logic; если GDD-параметры неясны — ERROR log + no-op без `NotImplementedException` в бою.
  - **Logging:** `[Story][Battle]` DEBUG type dispatch; ERROR unsupported params.
  - **Files:** `PerkFactory.cs`, новые perk classes
  - **Depends on:** Task 4

### Phase 4: Summons (Phase E)

- [x] **Task 6: Summon phase — skills stub gate + attack**
  - `IBattleSummonSimulator`: слоты по возрастанию `slotIndex`; на каждый живой summon: (skill если есть — Task 8 может добить) → SummonAttack → ReturnToPosition если melee → Wait(`summons_cooldown`).
  - Урон: `summon.Damage * (1 - target.Defence)`; крит от **атакующего main unit** стороны (GDD); уклон цели.
  - SpawnUnit в script при первом появлении, если ещё не эмитили (или в EmitInitial вместе со статусами — выбрать одно место и задокументировать).
  - Вставить в `SimulateSideTurn` после perks, до main units.
  - **Logging:** `[Story][Battle]` DEBUG slot, actor, damage, crit source unit id.
  - **Files:** `IBattleSummonSimulator.cs`, `BattleSummonSimulator.cs`, `BattleSimulatorService.cs`, `Program.cs`
  - **Depends on:** Task 1 patterns (damage/death)

### Phase 5: Skills + energy (Phase F)

- [x] **Task 7: Bind skills from snapshot + SkillFactory DI**
  - `UnitStateBuilder`: собирать skills из `ActiveSkillIds` (+ summon `skill_id` из конфига); WARN unknown id.
  - Зарегистрировать `ISkillFactory` в `Program.cs`.
  - Validator: unknown skill id → 400 (если skills станут резолвиться из config; если skill registry ещё thin — валидировать непустой string / известный SkillType map).
  - **Logging:** `[Story][Battle]` DEBUG skill ids per unit; WARN missing.
  - **Files:** `UnitStateBuilder.cs`, `SkillFactory.cs`, `Program.cs`, `BattleSimulationValidator.cs`
  - **Depends on:** none soft

- [x] **Task 8: UnitSkill phase + EnergySkill**
  - Перед normal attack атакующего main: доступные unit skills на ходе → CastSkill / damage pipeline → Wait(`units_cooldown`).
  - После return/комбо-цепочки: если `Energy >= MaxEnergy` → EnergySkill (без крита, с уклоном, **без** контратаки) → сброс/трата энергии по GDD/constants → SetEnergy.
  - Summon skills: вызвать из summon simulator (Task 6) когда skill bound.
  - **Logging:** `[Story][Battle]` DEBUG skill id, energy gate, targets.
  - **Files:** skill services / `BattleAttackService.cs` / summon simulator
  - **Depends on:** Task 7, Task 6

### Phase 6: Protocol polish (Phase G remainder)

- [x] **Task 9: Spec + docs sync + side-turn order assert**
  - Обновить `.ai-factory/specs/battle-simulation.md`: полный порядок фаз side-turn (status→perk→summon→unit skill→attack/counter/combo→energy).
  - `/aif-docs`: `Assets/Documents/Server/battle-api.md` — актуальный flow, rewards, без клиентских Tasks.
  - В `SimulateSideTurn` порядок строго как в GDD; DEBUG в начале side-turn: side + turn.
  - **Logging:** N/A для docs; DEBUG side-turn entry.
  - **Files:** spec, `Assets/Documents/Server/battle-api.md`, `BattleSimulatorService.cs`
  - **Depends on:** Tasks 1–8

## Commit Plan

- **Commit 1** (after Tasks 1–2): `feat(battle): counter/combo chain and battle reward applicator`
- **Commit 2** (after Tasks 3–5): `feat(battle): perk phase and elemental perk runtime`
- **Commit 3** (after Tasks 6–8): `feat(battle): summon and skill/energy phases`
- **Commit 4** (after Task 9): `docs(battle): sync spec and battle-api with full loop`

## Out of scope

- Unity playback / client DTO (Tasks 13–15 старого плана)
- Account/meta perk choice UI
- Non-demo shield и прочий GDD outside demo
- Unit tests (Testing: no)

## Ссылки

| Документ | Путь |
| --- | --- |
| Предыдущий план | `.ai-factory/plans/battle-client-server-contract.md` |
| Battle loop | `Assets/Documents/GDD/01-battle-loop.md` |
| Perks | `Assets/Documents/GDD/05-perks.md` |
| Entities | `Assets/Documents/GDD/06-entities.md` |
| Equipment | `Assets/Documents/GDD/07-equipment.md` |
| Rewards | `Assets/Documents/GDD/08-triggers-rewards.md` |
| Roadmap | `Assets/Documents/GDD/09-implementation-roadmap.md` |
| Spec | `.ai-factory/specs/battle-simulation.md` |
