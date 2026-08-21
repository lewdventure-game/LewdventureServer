# Battle simulate requests (50 + bonus probes)

`POST http://localhost:5000/api/battle/simulate`  
Content-Type: `application/json`

Id из клиентских JSON / Sheets-канона: character `1`, enemies `10101`/`10102`, summons `1`/`2`/`3`, perks `1`–`7`, statuses `1`–`5`, story levels `1`–`6`, stages `1`–`10`.  
Equipments в клиентском экспорте пустые — в реквестах `equipment: []` (если в Sheets появятся id — можно подставить).

`outcome` зависит от seed (сервер его сам крутит). Ниже — что должно быть видно в script / какой исход наиболее вероятен, не «железобетонный TeamAWin».

## Characters / Bonuses (канон Sheets, проверено на реквестах 1–4)

**Characters `id=1` (текущий sheet):** `upgrade_costs=10` (один слот → **макс 1 прокачка**), `start_bonus_type=1`, `start_bonus_value=10`, `upgrade_bonus_types=1`, `upgrade_bonus_values=10`, `skill_ids=1;2`.

**Bonuses (фрагмент):**

| id | operator | bonus_type | bonus_value | work_mode |
| --- | --- | --- | --- | --- |
| 1 | add | max_health_perk | 0.15 | permanent |
| 2 | add | damage_local | 5 | if_equipped:characters:1 |
| 3 | add | evasion_local | 0.05 | end_of_game |
| 4 | add | crit_chance_local | 0.15 | end_of_battle |
| 5 | add | damage_perk | -0.15 | next_battles:5 |
| 6 | add | combo_1_chance_perk | 1 | first_turns:1 |
| 7 | add | damage_perk | -0.05 | permanent |
| 8 | replace | crit_chance_local | 1 | first_turns:1 |
| 9 | add | healing_from_max | 0.5 | permanent |

### Character upgrades (GDD) vs runtime Grant

1. **Build персонажа** (`GrantCharacterUpgradeBonuses`):  
   - макс апгрейдов = `upgrade_costs.Length`;  
   - применено = `min(level - 1, costs.Length)`;  
   - апгрейд `i` → **только** `types[i]` + `values[i]` (value из Characters).  
   На текущем sheet у id `1` один cost → level 2 и level 20 дают **одинаковый** набор апгрейдов (start + одна прокачка). Чтобы level 20 качал дальше — в Sheets нужны 19 пар costs/types/values.

2. **Runtime grant** (`BattleBonusService.Grant` ← perk/status/skill):  
   всегда `Bonuses.bonus_value`. WarHowl выдаёт bonus id `2` (`damage_local` +5), **не** id `1` (`max_health_perk`). Start bonus id `1` остаётся resolved `Characters.start_bonus_value` (обычно `10`).

### Как билдится персонаж в simulate

- Start: bonus id `start_bonus_type` с value `start_bonus_value`.
- Upgrades: ordinal по GDD (см. выше). **Не** «level−1 раз все пары».
- На текущем Characters пресете level почти не качает DPS (апгрейд = `max_health_perk`).
- Snapshot **не** принимает `activeBonusIds`.
- `skill_ids` персонажа не авто-подтягиваются: только `activeSkillIds` (+ equipment `skill_id`).

Шаблон юнита без баффов:

```json
{
  "id": 1,
  "level": 1,
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
```

---

### 1. Baseline 1v1 vs tank

**Что:** голый character vs melee tank `10101`, level 1, story 1.  
**Ожидание:** полный side-turn loop; у врага `Approach` → attack → `ReturnToPosition`; `ShowDamage` игрока ~`19.05` за хит; ХП игрока ~2200 (только start `max_health_perk` value `10`); танк ~100 HP → ~6 хитов / kill около turn 5; чаще `TeamAWin`.

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 2. Baseline vs glass cannon

**Что:** vs range `10102` (мало HP, evasion 0.5).  
**Ожидание:** при промахе — `ShowMiss` / `isEvaded`; при попадании — one-shot (`damage` = Constants damage, часто ровно `20`, `KillUnit` в тот же ход) → чаще `TeamAWin`. Evasion 0.5 не гарантирует miss на конкретном seed.

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10102,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 3. High level vs glass (sheet: max 1 upgrade)

**Что:** character level 20 vs `10102`.  
**Ожидание:** урон как #2 (DPS не от level). На текущем sheet `upgrade_costs` длины 1 → level 20 = start + **одна** прокачка (не 19). HP выше #2 примерно на один апгрейд `+10` perk, не ~40k. One-shot glass при hit → `TeamAWin`.

```json
{"teamA":{"mainUnits":[{"id":1,"level":20,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10102,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 4. High level vs tank (sheet: max 1 upgrade)

**Что:** level 20 vs `10101`.  
**Ожидание:** тот же урон ~`19.05` / тот же ~6-хитовый килл. HP = start + 1 upgrade (cap по `upgrade_costs`). Чтобы проверить длинную лестницу прокачек — удлини в Sheets `upgrade_costs;types;values` до нужной длины.

```json
{"teamA":{"mainUnits":[{"id":1,"level":20,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 5. Dual enemies (target lowest slot)

**Что:** два врага slot 0=`10101`, slot 1=`10102`.  
**Ожидание:** все атаки в slot 0 пока жив; после `KillUnit` 10101 — фокус на 10102; `TeamAWin` если хватает DPS.

```json
{"teamA":{"mainUnits":[{"id":1,"level":10,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0},{"id":10102,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":1}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 6. Dual enemies reversed slots

**Что:** slot 0=`10102`, slot 1=`10101`.  
**Ожидание:** сначала бьют glass; после его смерти — tank.

```json
{"teamA":{"mainUnits":[{"id":1,"level":10,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10102,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0},{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":1}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 7. Fireball skill

**Что:** `activeSkillIds: ["fireball"]`.  
**Ожидание:** фаза unit skills → `CastSkill` fireball до normal attack; урон скиллом в script.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":["fireball"],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 8. Energy skill ownership (bar empty)

**Что:** только `"energy"` в skills, старт с 0 energy.  
**Ожидание:** energy skill **не** кастуется в начале; после хитов `SetEnergy`; когда bar full — `CastSkill` energy после return.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":["energy"],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 9. Fireball + energy

**Что:** оба скилла.  
**Ожидание:** fireball каждый ход в skill-фазе; energy только при полном баре после атаки.

```json
{"teamA":{"mainUnits":[{"id":1,"level":8,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":["fireball","energy"],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":3,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 10. Numeric skill ids (1 / 2)

**Что:** `"1"` = fireball, `"2"` = energy.  
**Ожидание:** то же, что #9; валидация принимает numeric keys.

```json
{"teamA":{"mainUnits":[{"id":1,"level":8,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":["1","2"],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10102,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 11. Reward perk (id 1)

**Что:** perk type Reward.  
**Ожидание:** `TriggerPerk` / `GrantReward` (resource) по правилам перка; meta, аккаунт не мутится.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[1],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10102,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 12. Fire Attack perk (id 2)

**Что:** elemental fire, `proc_rounds` odd, hit_rewards → burn status 1.  
**Ожидание:** на odd turns `TriggerPerk` + projectile damage; шанс `ApplyStatus` burn; DoT `TickStatus` на стороне врага.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[2],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 13. Earth Attack perk (id 3)

**Что:** earth → poison status 3; Z*A если уже есть burn.  
**Ожидание:** как fire, но статус poison; при наличии burn — другой effective chance.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[3],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 14. Air Attack perk (id 4)

**Что:** air cleanse burn stacks → rewards per stack.  
**Ожидание:** без burn на цели — нет flat hit_rewards; с burn (см. #37) — roll + cleanse + rewards.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[4],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 15. Water Attack perk (id 5)

**Что:** water cleanse poison.  
**Ожидание:** аналогично air, семья poison.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[5],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 16. ActionReward perk (id 6)

**Что:** rewards на attack/counter/combo thresholds.  
**Ожидание:** `TriggerPerk` / `GrantReward` / `SetBonus` по счётчикам actions.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[6],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":2,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 17. Resurrection perk (id 7)

**Что:** resurrection на character, слабый vs сильный tank.  
**Ожидание:** при смерти → revive ~80% HP, charges--; `KillUnit` не финал если заряд есть; после расхода заряда — обычная смерть / `TeamBWin`.

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[7],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":10,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 18. All elemental perks stacked

**Что:** perks 2–5 вместе.  
**Ожидание:** на odd turns несколько `TriggerPerk`; статусы могут копиться; script жирный.

```json
{"teamA":{"mainUnits":[{"id":1,"level":8,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[2,3,4,5],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":3,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 19. Full perk kit

**Что:** все 7 перков.  
**Ожидание:** смесь reward/elemental/action/resurrect; не валится валидация.

```json
{"teamA":{"mainUnits":[{"id":1,"level":10,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[1,2,3,4,5,6,7],"activeSkillIds":["fireball","energy"],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":2,"stageId":1}
```

### 20. Start burn on enemy

**Что:** `activeStatusIds: [1]` на враге.  
**Ожидание:** initial `ApplyStatus`; каждый enemy side-turn `TickStatus` burn; DoT урон в script.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[1],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 21. Start strong burn (status 2)

**Что:** strong burn + bonus.  
**Ожидание:** сильнее DoT (`damage_ratio` 0.2) + `SetBonus` от статуса.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[2],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 22. Start poison on enemy

**Что:** status 3.  
**Ожидание:** poison ticks на стороне B.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[3],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 23. Start strong poison

**Что:** status 4.  
**Ожидание:** как #21, poison ветка.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[4],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 24. Crit replacement status on self (status 5)

**Что:** status 5 на character (`status_target` caster).  
**Ожидание:** initial `ApplyStatus` на A; бонус крита (`replace` crit chance) первые turns по work_mode бонуса 8.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[5],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 25. Burn + poison stacked start

**Что:** statuses 1 и 3 на враге.  
**Ожидание:** оба тикают по `proc_order`; суммарный DoT заметнее.

```json
{"teamA":{"mainUnits":[{"id":1,"level":3,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[1,3],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 26. Single summon venom (id 1)

**Что:** summon 1 + skill venom.  
**Ожидание:** `SpawnUnit` summon; summon phase → skill + SummonAttack; crit от main.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[{"id":1,"level":5,"masteryLevel":1,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 27. Summon war howl (id 2)

**Что:** summon 2.  
**Ожидание:** `summon_2_skill_1` (WarHowl) в summon phase.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[{"id":2,"level":5,"masteryLevel":2,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}]},"teamB":{"mainUnits":[{"id":10102,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 28. Summon soul siphon (id 3)

**Что:** summon 3.  
**Ожидание:** `summon_3_skill_1` (SoulSiphon); валидация skill ok.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[{"id":3,"level":5,"masteryLevel":3,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}]},"teamB":{"mainUnits":[{"id":10101,"level":2,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 29. Three summons slot order

**Что:** summons 1,2,3 в слотах 0,1,2.  
**Ожидание:** порядок действий slot ↑; три `SpawnUnit`; три summon атаки за ход A.

```json
{"teamA":{"mainUnits":[{"id":1,"level":10,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[{"id":1,"level":10,"masteryLevel":5,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0},{"id":2,"level":10,"masteryLevel":5,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":1},{"id":3,"level":10,"masteryLevel":5,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":2}]},"teamB":{"mainUnits":[{"id":10101,"level":3,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 30. Maxed summon DPS

**Что:** один summon level 50 mastery 10.  
**Ожидание:** огромный summon damage → быстрый `TeamAWin` vs glass.

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[{"id":1,"level":50,"masteryLevel":10,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}]},"teamB":{"mainUnits":[{"id":10102,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 31. Training level only

**Что:** `trainingLevel: 10` (Trainings stub empty — бонус может быть 0, но snapshot валиден).  
**Ожидание:** 200 OK; build не падает; статы как без training если manager пуст.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":10,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 32. Artifact / aspect stubs

**Что:** непустые `artifactIds`/`aspectIds` (managers stub).  
**Ожидание:** валидация пропускает; бонусы артефактов/аспектов не применятся (empty managers) — бой как baseline.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[1,2],"aspectIds":[1],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 33. Stage multiplier on

**Что:** `stageId: 5` (boss stage в клиентском JSON).  
**Ожидание:** враг сильнее через `enemy_stats_multiplier` из Sheets; дольше бой / выше шанс `TeamBWin`/`Timeout` vs #1.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":5}
```

### 34. Stage 0 = no stage mult

**Что:** явно `stageId: 0`.  
**Ожидание:** только level multipliers; сравнение с #33 — враг слабее.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 35. Story level 6

**Что:** другой `storyLevelId` (maxTurns + enemy mult из Sheets).  
**Ожидание:** 200; `maxTurns` из level 6; множители врагов другие, чем на level 1.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":6,"stageId":10}
```

### 36. Counter-heavy tank

**Что:** vs `10101` (counter 0.2) долгий бой.  
**Ожидание:** в script `CounterAttack` фазы / counter damage на character после hit.

```json
{"teamA":{"mainUnits":[{"id":1,"level":3,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 37. Fire perk + pre-burn on enemy (Z*A path)

**Что:** perk 2 + enemy уже burn.  
**Ожидание:** effectiveChance = rewards_chance * debuff_rewards_chance; один apply hit_rewards при успехе (не spam по стакам).

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[2],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":2,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[3],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 38. Air cleanse with burn present

**Что:** perk 4 + enemy burn stacks.  
**Ожидание:** на hit odd turn — roll Z; success → `RemoveStatus` burn stacks + rewards per stack; fail → статусы остаются.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[4],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":2,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[1,2],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 39. Water cleanse with poison present

**Что:** perk 5 + poison на враге.  
**Ожидание:** как #38 для poison семьи.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[5],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":2,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[3,4],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 40. Kitchen sink (happy path)

**Что:** skills + perks + summon + statuses + stage.  
**Ожидание:** 200; script содержит Spawn/ApplyStatus/TriggerPerk/CastSkill/ShowDamage/SetBattleResult.

```json
{"teamA":{"mainUnits":[{"id":1,"level":12,"masteryLevel":0,"equipment":[],"trainingLevel":3,"artifactIds":[],"aspectIds":[],"activePerkIds":[2,6,7],"activeSkillIds":["fireball","energy"],"activeStatusIds":[5],"slotIndex":0}],"summons":[{"id":1,"level":15,"masteryLevel":4,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0},{"id":2,"level":12,"masteryLevel":3,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":1}]},"teamB":{"mainUnits":[{"id":10101,"level":4,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[1],"slotIndex":0},{"id":10102,"level":4,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":1}],"summons":[]},"storyLevelId":3,"stageId":5}
```

### 41. Validation: unknown character

**Что:** character id 999.  
**Ожидание:** `400` — `Unknown character id = 999`.

```json
{"teamA":{"mainUnits":[{"id":999,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 42. Validation: unknown enemy

**Что:** enemy 99999.  
**Ожидание:** `400` — `Unknown enemy id = 99999`.

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":99999,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 43. Validation: unknown storyLevelId

**Что:** storyLevelId 999.  
**Ожидание:** `400` — `Unknown storyLevelId = 999`.

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":999,"stageId":0}
```

### 44. Validation: unknown stageId

**Что:** stageId 999 при nonzero.  
**Ожидание:** `400` — `Unknown stageId = 999`.

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":999}
```

### 45. Validation: unknown perk

**Что:** perk 999.  
**Ожидание:** `400` — `Unknown perk id = 999`.

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[999],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 46. Validation: unknown skill

**Что:** `"skill_heal"`.  
**Ожидание:** `400` — `Unknown skill id = skill_heal`.

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":["skill_heal"],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 47. Validation: unknown summon

**Что:** summon id 99.  
**Ожидание:** `400` — `Unknown summon id = 99`.

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[{"id":99,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 48. Validation: empty teamA mainUnits

**Что:** пустой mainUnits.  
**Ожидание:** `400` — mainUnits must not be empty.

```json
{"teamA":{"mainUnits":[],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 49. Validation: unknown equipment id

**Что:** equipment id которого нет в Sheets/managers.  
**Ожидание:** `400` — `Unknown equipment id = 101` (пока Equipments пустые после sync — любой id валится).

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[{"id":101,"level":1}],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 50. Timeout pressure

**Что:** слабый character, жирный tank, story с maxTurns ~25.  
**Ожидание:** высокий шанс `Timeout` (враг жив) или `TeamBWin`; в конце `SetBattleResult` с outcome 2 или 3.

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":20,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":5}
```

---

## Bonus probes (id 1–9)

В snapshot **нет** поля «выдай bonus id N». Ниже — что реально крутится из текущего API + хуков в коде/Statuses.  
Где хука нет — реквест всё равно дан как шаблон: сначала в Sheets повесь `bonuses:[bonus:N:1]` на status `5` (или новый bonus_change), потом гоняй JSON.

| bonus id | Как прогнать сейчас | Что смотреть |
| --- | --- | --- |
| 1 | #1 / #4 (Characters; cap по `upgrade_costs`); #51 (WarHowl → Grant damage `bonus:2`) | HP: start / start+1 upgrade; Howl **не** бампает max HP |
| 2 | #52 (нужен Sheets: status/perk с `bonus:2`) | `ShowDamage` выше baseline (~+5 local), `if_equipped:characters:1` ок на teamA id=1 |
| 3 | #53 (Sheets: `bonus:3`) | больше `isEvaded` / `ShowMiss` на ударах **по** носителю |
| 4 | #54 (Sheets: `bonus:4`) | чаще `isCritical` у носителя до конца боя |
| 5 | #55 (Sheets: `bonus:5`) | урон ниже (`damage_perk -0.15`); work_mode `next_battles` в одном simulate почти как permanent |
| 6 | #56 (Sheets: `bonus:6`) | на **turn 0** почти гарант combo1; со turn 1 — как baseline |
| 7 | #57 (Sheets: `bonus:7`) | урон чуть ниже (`damage_perk -0.05`) весь бой |
| 8 | #24 / #58 (status 5) / #59 (SoulSiphon) | `replace` crit=1 на `first_turns:1` → криты на первом полном ходу |
| 9 | #60 (Sheets: `bonus:9` **или** любой Grant healing_from_max) | `SetBonus` / heal `0.5 * maxHp` разово (effect, не бакет) |

### 51. Bonus 2 via runtime Grant (WarHowl) — `damage_local` +5

**Что:** summon 2 кастует WarHowl → `BattleBonusService.Grant(ally, bonusId=2)` с табличным `damage_local` +5 (`if_equipped:characters:1`).  
**Ожидание:** max HP main **остаётся** start-only (`2200` при start perk `10`). `SetBonus` bonusId `2` value `5` (не `bonusId=1` value `0.15`). Урон ally выше baseline за счёт +5 local.

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[{"id":2,"level":5,"masteryLevel":2,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 52. Bonus 2 probe — `damage_local` +5 (`if_equipped:characters:1`)

**Предусловие Sheets:** status `5` (или другой bonus_change) → `bonuses:[bonus:2:1]`.  
**Что:** status на character id `1` (equipped characters:1 → work_mode проходит).  
**Ожидание:** урон по танку выше baseline (~`19.05` → заметно больше за счёт +5 local); без equipped character id 1 — бонус skip в логе `if_equipped skip`.

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[5],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 53. Bonus 3 probe — `evasion_local` +0.05

**Предусловие Sheets:** `bonuses:[bonus:3:1]` на status, повешенном на **цель ударов** (сюда — character, чтобы враг промахивался).  
**Ожидание:** чаще `ShowMiss` / `isEvaded` на атаках 10101 → teamA.

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[5],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 54. Bonus 4 probe — `crit_chance_local` +0.15 (`end_of_battle`)

**Предусловие Sheets:** `bonuses:[bonus:4:1]` на status у character.  
**Ожидание:** чаще `isCritical: true` у ударов teamA vs #1; действует весь бой.

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[5],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 55. Bonus 5 probe — `damage_perk` −0.15 (`next_battles:5`)

**Предусловие Sheets:** `bonuses:[bonus:5:1]` на status у character.  
**Ожидание:** урон ниже baseline; в одном simulate `next_battles` не сгорает mid-fight (decrement на battle end).

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[5],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 56. Bonus 6 probe — `combo_1_chance_perk` = 1 (`first_turns:1`)

**Предусловие Sheets:** `bonuses:[bonus:6:1]` на status у character.  
**Ожидание:** на первом полном ходу (turn index 0 в first_turns) почти всегда combo1 после normal; со следующего хода — обычный шанс.

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[5],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 57. Bonus 7 probe — `damage_perk` −0.05 (`permanent`)

**Предусловие Sheets:** `bonuses:[bonus:7:1]` на status у character.  
**Ожидание:** урон чуть ниже #1, весь бой.

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[5],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 58. Bonus 8 via status 5 (уже в Sheets)

**Что:** `activeStatusIds: [5]` — bonus_change с `bonus:8` (`replace crit_chance_local = 1`, `first_turns:1`).  
**Ожидание:** на первом ходе удары character почти все `isCritical: true`; дальше — baseline crit. См. также #24.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[5],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 59. Bonus 8 via SoulSiphon Grant

**Что:** summon 3 → Grant ally bonus id `8` (табличный value `1`, `first_turns:1`).  
**Ожидание:** после siphon skill у main крит-replace на первом ходе; `SetBonus` bonusId 8.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[{"id":3,"level":5,"masteryLevel":1,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 60. Bonus 9 probe — `healing_from_max` 0.5

**Предусловие Sheets:** status/perk/reward с `bonus:9:1` (effect: heal `0.5 * maxHp * healingBoost`, не стат-бакет).  
**Что:** повесь на character; лучше бить танком пару раз до apply, чтобы было куда хилить (или смотреть `SetHp` вверх сразу на seed).  
**Ожидание:** `SetBonus` / скачок текущего HP; max HP не обязан расти.

```json
{"teamA":{"mainUnits":[{"id":1,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[5],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

### 61. Strong burn / poison status bonuses (sheet-defined)

**Что:** status `2` / `4` на враге — strong DoT + `bonuses` из Statuses (если в parameters уже висят bonus id).  
**Ожидание:** DoT + `SetBonus` на носителе статуса; конкретный bonus id — смотри Sheets Statuses, не угадывать.

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[2],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

```json
{"teamA":{"mainUnits":[{"id":1,"level":5,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[],"slotIndex":0}],"summons":[]},"teamB":{"mainUnits":[{"id":10101,"level":1,"masteryLevel":0,"equipment":[],"trainingLevel":0,"artifactIds":[],"aspectIds":[],"activePerkIds":[],"activeSkillIds":[],"activeStatusIds":[4],"slotIndex":0}],"summons":[]},"storyLevelId":1,"stageId":0}
```

---

## Curl one-liner

```powershell
$body = Get-Content -Raw 'D:\Project\LewdventureServer\.ai-factory\samples\battle-simulate-50-requests.md'
# проще скопировать JSON из секции и:
Invoke-RestMethod -Method Post -Uri 'http://localhost:5000/api/battle/simulate' -ContentType 'application/json' -Body '{"teamA":{...},"teamB":{...},"storyLevelId":1,"stageId":0}'
```
