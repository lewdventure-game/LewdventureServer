# Implementation Plan: Фикс самых жирных battle-багов

Branch: none (остаёмся на текущей `master`; `git.create_branches: false`)
Created: 2026-08-07

## Settings
- Testing: no
- Logging: verbose
- Docs: yes

## Scope

Чиним только баги, которые ломают бой прямо сейчас (GDD vs код). Не трогаем meta/run, story triggers, Trainings/Artifacts/Aspects sync stubs, breakout, resurrection lifecycle, perk selection.

### Решения по GDD (зафиксировать в коде и docs)

1. **Fire/Earth `Z * A`:** в таблице параметров нет поля `A`. Канон реализации: `A = debuff_rewards_chance`.  
   - Нет целевого дебаффа → шанс = `rewards_chance` (Z).  
   - Есть ≥1 стак нужной семьи → шанс = `rewards_chance * debuff_rewards_chance` (Z * A).  
   - При успехе — **один** apply полного `hit_rewards`.  
   - Убрать `ApplyHitRewardsTimes` по числу стаков.  
   - Убрать fallback «второй ролл → только status rewards».  
   - Default `debuff_rewards_chance` при отсутствии параметра = `1` (Z*1 = Z), не использовать как «почти всегда успех второго ролла».

2. **Air/Water cleanse:** `hit_rewards` — награда **за снятый стак**, не за каждый hit.  
   - На успешном hit: если есть стаки семьи (air=burn, water=poison) — ролл `rewards_chance`; при успехе снять все стаки семьи и выдать `hit_rewards` за каждый снятый; при провале ролла — не снимать и не награждать.  
   - Не применять плоский `TryApplyHitRewards` дополнительно к cleanse-пути.

3. **`proc_rounds` пустой:** перк **не** триггерится + `LogWarning` (GDD: только перечисленные раунды).

4. **Equipment bonuses:** GDD — типы техническими именами, значения через запятую. Принимать id **или** имя `BonusType`; delimiter значений — `,` и `;` (оба).

5. **Melee:** нет ни одного `is_melee=true` на экипе → `UnitFlags.Range`. Саммоны — поле `is_melee` в Summons (default false = Range).

## Commit Plan
- **Commit 1** (после tasks 1–3): `fix(battle): correct elemental perk reward and proc rounds`
- **Commit 2** (после tasks 4–5): `fix(battle): melee flags and equipment bonus parsing`
- **Commit 3** (после tasks 6–8): `fix(battle): energy skill gating, formula2 rounding, docs`

## Tasks

### Phase 1: Стихийные перки

- [x] Task 1: Переписать модель hit-rewards для fire/earth
  - Файлы: `Assets/Game/Battles/Services/ElementalAttackPerk.cs`, `FireAttackPerk.cs`, `EarthAttackPerk.cs`, `PerkFactory.cs`
  - Убрать stack-spam `ApplyHitRewardsTimes` из fire/earth.
  - В `TryApplyHitRewards` (или hook): effectiveChance = Z, либо Z*A при наличии poison (fire) / burn (earth); один `BattleRewardService.Apply` на успех.
  - Удалить status-only second roll.
  - Default `debuff_rewards_chance` = 1f только как множитель A (уже в factory); логировать Z, A, effectiveChance, hasDebuff.
  - LOGGING: `[Story][Battle]` Debug — roll, chance, applied/skipped, perkId, targetId; Warning если chance > 1 (clamp? зафиксировать: clamp к 1 для ролла).

- [x] Task 2: Починить air/water cleanse под шанс Z
  - Файлы: `AirAttackPerk.cs`, `WaterAttackPerk.cs`, `ElementalAttackPerk.cs`
  - Cleanse + per-stack rewards только после успешного ролла `rewards_chance`.
  - Не дублировать плоский hit_rewards apply на том же hit (override/skip base path для air/water).
  - LOGGING: Debug — stacksBefore, roll, cleansed, rewardsTimes; Warning если cleanse без rewards при success.

- [x] Task 3: `proc_rounds` empty = never
  - Файл: `ElementalAttackPerk.CanTrigger`
  - Пустой список → `false` + Warning один раз на trigger-check или при создании перка в `PerkFactory` (предпочтительно при create: «proc_rounds empty perkId=…»).
  - LOGGING: Warning при empty; Debug при skip из-за round mismatch.

<!-- Commit checkpoint: tasks 1-3 -->

### Phase 2: Флаги и экип

- [x] Task 4: Melee/Range для персонажа и саммона
  - Файлы: `UnitStateBuilder.cs`, `ISummonMapper.cs`, `SummonMapper.cs`, при необходимости `Assets/Documents/Server/config-sync.md`
  - `ResolveCharacterMeleeFlags`: fallback `UnitFlags.Range` (не Melee).
  - Добавить `is_melee` на Summon mapper (string/bool как у Equipment); в `Build` для summon: Melee если true, иначе Range.
  - LOGGING: Debug — unitId, flags, equipment melee hit; Warning если equipment id missing при resolve.

- [x] Task 5: Парсинг equipment bonus type/values по GDD
  - Файлы: `UnitStateBuilder.cs` (`GrantEquipmentBonusSlot`, `ParseEquipmentBonusValue`), при необходимости маленький helper рядом / в Bonuses
  - Type: сначала `int` id; иначе parse `BonusType` (snake_case / EnumMember) → найти bonus в `_configDistributor.Bonuses` с этим типом (первый match; Warning при 0 или >1).
  - Values: split по `,` и `;` (если есть оба — предпочесть тот, что даёт больше сегментов, либо: сначала `,`, fallback `;`).
  - LOGGING: Debug grant; Warning parse fail / ambiguous type / missing bonus.

<!-- Commit checkpoint: tasks 4-5 -->

### Phase 3: Энергия, формулы, docs

- [x] Task 6: Energy skill — ownership + фазы + cooldown
  - Файлы: `BattleSkillSimulator.cs`, `BattleAttackService.cs`, `EnergySkill.cs` (если нужно)
  - `CastAllSkills`: пропускать `SkillType.Energy` / ключ energy (только post-return path).
  - `TryCastEnergySkill`: каст только если у actor есть energy-skill в `Skills`; иначе return.
  - `EmitReturnAndUnitCooldown`: не вешать обязательный `Wait(units_cooldown)` после return, если energy не будет кастоваться; wait перед energy оставить в energy-шаге (как сейчас в `TryCastEnergySkill`). Если energy нет — после return wait не нужен по GDD (return → конец фазы юнита).
  - LOGGING: Debug skip (no skill / bar not full); Information cast; Warning если bar full но skill отсутствует.

- [x] Task 7: Округление Formula 2 для множителей
  - Файл: `CharacteristicCalculator.cs`
  - GDD: математически округлять дробный итог у `КРИТ_МН`, `КОМБО_МН` (combo1/combo2 multipliers), `КОНТР_МН`, `СПЕЛЛ_МН`.
  - Шансы (crit/combo/counter/evasion) и `АТК_МН` / energy gain / vampyrism — **не** округлять в этом таске (GDD для шансов не требует round; energy — целое, но отдельным follow-up если понадобится).
  - LOGGING: Debug rebuild уже есть; при необходимости Debug raw→rounded для затронутых полей.

- [x] Task 8: Docs checkpoint
  - Файлы: `Assets/Documents/Server/battle-api.md`, `Assets/Documents/Server/config-sync.md`, `Assets/Documents/GDD/05-perks.md` (короткая заметка про Z*A = rewards_chance * debuff_rewards_chance), при необходимости `07-equipment.md` (delimiter/types).
  - Зафиксировать: energy только post-return при наличии skill; melee fallback Range; equip parse id|name + `,`/`;`; elemental chance model.
  - Не плодить новые log-теги.

<!-- Commit checkpoint: tasks 6-8 -->

## Out of scope

- Trainings / Artifacts / Aspects Google Sheets wiring
- Story triggers / Story_events / Perk_groups selection
- `next_battles` / run-lifecycle work modes
- Resurrection remove-perk / next-turn-after-respawn
- DoT snapshot vs live damage
- Phase-end cooldown polish (status/perk/summon trailing waits) — отдельный план
- Generic skill catalog / enemy skill_ids / equipment skill_id binding
- breakout_multis
- Тесты (запрещены settings)

## Risks

- Sheets Equipments могут уже слать bonus **id** и `;` — сохраняем оба формата, чтобы не сломать текущие таблицы.
- `Z * A` может дать chance > 1 — clamp ролла к 1, лог Warning.
- Lookup bonus по `BonusType` неоднозначен при нескольких строках одного типа — Warning + first match; если в проде всплывёт — править sheet на id.
