# Клиентский battle GDD для ИИ

> **Аудитория:** ИИ / разработчик Unity-клиента `D:\Project\Lewdventure`.  
> **Цель:** реализовать сборку request и playback battle script так, чтобы клиент работал по протоколу и GDD.  
> **Канон протокола (сервер):** [`.ai-factory/specs/battle-simulation.md`](../../../.ai-factory/specs/battle-simulation.md).  
> **Презентация (UX):** [`02-presentation-contract.md`](02-presentation-contract.md).  
> **API кратко:** [`../Server/battle-api.md`](../Server/battle-api.md).  
> **protocolVersion:** `1`. При рассинхроне с сервером — сначала сверяй этот номер и Decisions в spec.

Этот файл — handoff-документ. Его можно отдать другому ИИ целиком. Не пересчитывай бой на клиенте. Не «улучшай» логику. Играй script.

---

## 1. Роли (не путать)

| Сторона | Что делает | Чего не делает |
| --- | --- | --- |
| **Сервер** (`LewdventureServer`) | Seed RNG, сборка характеристик, бой, статусы/перки/скиллы, outcome, упорядоченный script | Не двигает пиксели, не играет анимации |
| **Клиент** (Unity `Lewdventure`) | Собирает thin snapshot, POST simulate, строит lookup юнитов, играет `commands` по порядку, рисует UI/VFX | Не считает урон/крит/уклон/смерть/статусы/исход |

**Жёсткое правило:** если на экране что-то меняется (HP, energy, статус, смерть, награда в презентации) — это уже есть в команде сервера. Клиент не «додумывает».

Legacy локальный `BattleSimulatorService` / `BattleEvent` / `BattleActionType` на клиенте — deprecate. Цель: HTTP simulate + command executor.

---

## 2. End-to-end flow

```text
1. Игрок входит в бой (story level + stage известны клиенту из meta/UI).
2. Клиент собирает TeamSnapshot для teamA (игрок) и teamB (враги).
3. POST /api/battle/simulate  (без seed, без maxTurns, без HP/DMG).
4. Сервер: seed → build UnitState → симуляция → BattleScriptResponse.
5. Клиент:
   - спавнит main units по snapshot (слоты/позиции);
   - регистрирует lookup (configId, slotIndex) → entity;
   - последовательно исполняет steps[].commands;
   - SpawnUnit / KillUnit / DespawnUnit обновляют сцену и lookup;
   - SetBattleResult / outcome → экран результата.
6. (Опционально) debug: POST /api/battle/replay с тем же snapshot + seed из response.
```

Base URL local: `http://localhost:5000`.  
Auth: нет (сейчас).  
JSON: **camelCase**.

Перед боем сервер должен иметь загруженные конфиги (на старте сам sync; hot-reload — `POST /api/config/update`). Клиент конфиги баланса для боя **не** владеет.

---

## 3. Request: что собирать

### 3.1 Endpoints

| Method | Path | Body | Когда |
| --- | --- | --- | --- |
| `POST` | `/api/battle/simulate` | `BattleSimulationData` | Обычный бой |
| `POST` | `/api/battle/replay` | `BattleReplayData` (= simulate + `seed`) | Повтор того же боя бит-в-бит |

### 3.2 `BattleSimulationData`

| Field | Type | Required | Клиент |
| --- | --- | --- | --- |
| `teamA` | `TeamSnapshot` | yes | Игрок всегда A в начале |
| `teamB` | `TeamSnapshot` | yes | Враги из story/encounter |
| `storyLevelId` | `int` | no | → сервер берёт `maxTurns` + `Story_levels.enemies_*_multiplier` |
| `stageId` | `int` | no | → `StoryStage.EnemyStatsMultiplier`; `0` / нет = без множителя стадии |

**Spawn HP врага на клиенте (presentation only):** `raw Health * levelHealthMultiplier * stageMultiplier`, та же формула что `UnitStateBuilder.BuildEnemyBuckets`. Если в клиентском `StoryStages` / `StoryLevels` нет multiplier-полей — spawn будет `raw` (×1), а первый `SetHp` с сервера прыгнет вверх (stage 4: 100→127 при max 130). Канон множителей — Google Sheets на сервере; клиентский JSON обязан их держать для UI baseline.

### 3.3 Запрещено слать в simulate

- `seed`
- `maxTurns`
- HP, damage, crit, evasion, energy, любые итоговые характеристики
- профиль аккаунта, валюты, инвентарь целиком
- `activeBonusIds` (такого поля нет; бонусы приходят из конфигов по id экипов/артефактов/…)

### 3.4 `TeamSnapshot`

| Field | Type | Смысл |
| --- | --- | --- |
| `mainUnits` | `UnitSnapshot[]` | Главные юниты (персонаж / мобы) |
| `summons` | `UnitSnapshot[]` | Саммоны; слоты по возрастанию `slotIndex` (1→2→3) |

### 3.5 `UnitSnapshot` (thin)

| Field | Type | Правило сборки на клиенте |
| --- | --- | --- |
| `id` | `int` | Config id: character / enemy / summon |
| `level` | `int` | Уровень сущности (character upgrade tier / enemy level / summon level) |
| `masteryLevel` | `int` | Мастерство саммона; для non-summon = `0` |
| `equipment` | `{ id, level }[]` | Предпочтительно; `level` 1-based индекс бонусов экипа |
| `equipmentIds` | `int[]` | Legacy; сервер трактует как equipment level = 1 |
| `trainingLevel` | `int` | Тренировка player main; иначе `0` |
| `artifactIds` | `int[]` | Активные артефакты |
| `aspectIds` | `int[]` | Активные аспекты |
| `activePerkIds` | `int[]` | Перки, которые реально активны **на момент входа в бой** |
| `activeSkillIds` | `string[]` | Ключи скиллов (`"fireball"` / `"1"` и т.п.). **Не** авто-подтягиваются из `Characters.skill_ids` — только то, что клиент явно положил (+ сервер ещё может взять skill с equipment) |
| `activeStatusIds` | `int[]` | Стартовые статусы (редко; обычно `[]`) |
| `slotIndex` | `int` | Позиция. Main: `0` (и `1` для второго моба). Summons: `1`/`2`/`3` |

Пустые массивы — всегда `[]`, не `null`.

### 3.6 Минимальный пример

```json
{
  "teamA": {
    "mainUnits": [
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
    ],
    "summons": []
  },
  "teamB": {
    "mainUnits": [
      {
        "id": 10101,
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
    ],
    "summons": []
  },
  "storyLevelId": 1,
  "stageId": 0
}
```

Больше готовых тел: [`.ai-factory/samples/battle-simulate-50-requests.md`](../../../.ai-factory/samples/battle-simulate-50-requests.md).

### 3.7 Откуда клиент берёт данные snapshot

| Поле | Источник на клиенте (ожидание) |
| --- | --- |
| Player character id/level/training/equipment/artifacts/aspects/perks/skills | Локальный прогресс / save / run state |
| Enemy main units | Encounter / story stage spawn table (id, level, slotIndex) |
| Summons | Экипированные слоты саммонов игрока (и врага, если есть) |
| `storyLevelId` / `stageId` | Текущий уровень/стадия стори |

Клиент **не** должен «угадывать» вражеские статы — только id/level/slot (+ пустые perk/skill массивы, если у моба нет стартовых).

---

## 4. Response: что парсить

### 4.1 `BattleScriptResponse`

| Field | Type | Клиент |
| --- | --- | --- |
| `protocolVersion` | `int` | Если ≠ ожидаемой (`1`) — fail fast / показать ошибку несовместимости |
| `seed` | `ulong` | Сохранить для replay/debug; **не** для локального пересчёта боя |
| `outcome` | `BattleOutcome` | Итоговый результат (дублируется командой `SetBattleResult`) |
| `steps` | `BattleStep[]` | Упорядоченный script; играть **строго по порядку** |

Поля `turnsPlayed` **нет**. Число ходов = `max(steps[i].turn)` или `0`, если steps пуст.

### 4.2 `BattleOutcome`

| Value | Int | Смысл для UI |
| --- | --- | --- |
| `Unknown` | 0 | Баг / незавершённый script |
| `TeamAWin` | 1 | Победа игрока (A) |
| `TeamBWin` | 2 | Поражение |
| `Timeout` | 3 | Лимит ходов, враги живы → поражение по времени |
| `Draw` | 4 | Зарезервировано; в GDD пока не описано |

### 4.3 `BattleStep`

| Field | Type | Смысл |
| --- | --- | --- |
| `index` | `int` | Порядковый номер шага |
| `turn` | `int` | Номер хода |
| `phase` | `BattlePhaseType` | Фаза (для логов/отладки/опционального UI); playback идёт по `commands` |
| `actorId` | `int` | Config id актёра |
| `actorSlotIndex` | `int` | Слот актёра; `-1` если не применимо |
| `targetId` | `int` | Config id цели; **`-1`** если цели нет |
| `targetSlotIndex` | `int` | Слот цели; **`-1`** если цели нет |
| `commands` | `BattleCommand[]` | Операции презентации |

### 4.4 `BattleCommand`

```json
{ "operation": "ShowDamage", "params": { "...": "..." } }
```

`operation` — enum name или int (Newtonsoft обычно строка enum name).  
`params` — JSON object; ключи ниже обязательны для операции.

---

## 5. Identity юнита (критично)

**Identity = `(configId, slotIndex)`.**

Одного `id` недостаточно: два моба с одним config id в разных слотах — разные сущности.

| Где | Поля |
| --- | --- |
| Step | `actorId` + `actorSlotIndex`, `targetId` + `targetSlotIndex` |
| Commands с actor/target | `actorId`/`actorSlotIndex`, `targetId`/`targetSlotIndex` |
| Commands с unit | `unitId` + `slotIndex` (`SetHp`, `SetEnergy`, `KillUnit`, `SpawnUnit`, `DespawnUnit`, `SetBonus`) |

### Обязательный lookup на клиенте

```text
Dictionary<(int configId, int slotIndex), Entity>  // или аналог
```

Правила:

1. При старте боя зарегистрируй все `mainUnits` из snapshot (и заранее видимых summons, если спавнишь до script).
2. На `SpawnUnit` — создать entity и **добавить** в lookup.
3. На `KillUnit` — проиграть смерть; entity можно оставить мёртвым или убрать из «живых», но ключ lookup должен оставаться резолвимым до `DespawnUnit` / конца боя (иначе следующие команды на труп сломаются).
4. На `DespawnUnit` — убрать со сцены и из lookup.
5. Резолв всегда по паре `(id, slotIndex)`, никогда только по `id`.

`targetId = -1` / `targetSlotIndex = -1` → шаг/команда без внешней цели (cooldown, self-only и т.п.).

---

## 6. Каталог команд → что делать на клиенте

Исполнять **последовательно**, дожидаясь завершения анимации/wait текущей команды перед следующей (внутри step и между steps). Сервер уже вложил тайминги через `Wait`.

### 6.1 Таблица операций

| CommandType | Parameters | Действие клиента |
| --- | --- | --- |
| `Wait` | `seconds` (float) | Пауза `seconds`. Не ускоряй логикой — это ритм боя. |
| `Approach` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `isMelee` | Если melee и юнит в дальнем положении — сдвиг к цели (~0.2 с, дистанции из layout). Range: обычно no-op движения, но команду всё равно принять. |
| `ReturnToPosition` | `actorId`, `actorSlotIndex` | Вернуть actor на дальнюю/домашнюю точку слота. |
| `PlayAnimation` | `actorId`, `actorSlotIndex`, `animationKey` | Проиграть анимацию (`attack` / cast / hit / …). Ключ мапится на animator клиента. |
| `ShowDamage` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `damage`, `isCritical`, `isEvaded` | Flytext урона. Обычный: белый, меньше. Крит: оранжевый + красная обводка, больше. При hit: flash белым ~0.15 с + короткий knockback на target. **Не** меняй HP здесь — жди `SetHp`. Тики damage over time тоже идут через `ShowDamage`: `isCritical` — реальный roll crit chance источника/носителя, не баг. |
| `ShowHeal` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `heal` | Flytext лечения. HP — через `SetHp`. |
| `ShowMiss` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex` | Уклон: target скользит назад ~0.2 с, flytext `ui.battle.evasion`, возврат. |
| `ApplyStatus` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `statusId`, `stacks`, `durationTurns` | Иконка у HP-бара (`icon_art_name` из конфига статусов), цифра стаков если >1, VFX. `durationTurns = -1` = «бесконечно» для UI (сервер так шлёт вместо `int.MaxValue`). |
| `RemoveStatus` | `targetId`, `targetSlotIndex`, `statusId` | Снять иконку/VFX статуса. |
| `TickStatus` | `targetId`, `targetSlotIndex`, `statusId`, `value` | Тик (damage over time / hot): VFX типа + опционально число `value`. HP всё равно через последующий `SetHp`, если есть. |
| `CastSkill` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `skillId` | Презентация каста (VFX/UI по `skillId`). Не применяй эффекты скилла сам. |
| `TriggerPerk` | `actorId`, `actorSlotIndex`, `targetId`, `targetSlotIndex`, `perkId` | Презентация перка (иконка/плашка rarity). |
| `SetHp` | `unitId`, `slotIndex`, `hp` | **Авторитетное** HP. Выставить бар/модель в `hp`. |
| `SetEnergy` | `unitId`, `slotIndex`, `energy` | **Авторитетная** энергия. |
| `SetBonus` | `unitId`, `slotIndex`, `bonusId`, `value`, `sourceId` | Опциональный UI/debug индикатор бонуса. Не пересчитывай статы из этого. На wire — **один** `SetBonus` на `bonusId` с суммой активных слоёв. Start bonus id `1` = resolved grant (`Characters.start_bonus_value`, обычно `10`). WarHowl → bonus id `2` (`damage_local`), не второй слой `0.15` на id `1`. |
| `SpawnUnit` | `unitId`, `slotIndex` | Заспавнить summon/unit в слот, зарегистрировать lookup, idle. |
| `DespawnUnit` | `unitId`, `slotIndex` | Убрать со сцены (в т.ч. живой summon на cleanup конца боя). |
| `KillUnit` | `unitId`, `slotIndex` | Death-анимация / состояние «мёртв». |
| `GrantReward` | `rewardType`, `rewardId`, `count`, `targetId` | **Только презентация** дропа/награды в бою. Аккаунт сервер **не** мутирует. `rewardType`: `resource` / `character` / `summon` / `equipment`. `rewardId` — **int или string** (для `resource` часто string key). Не путать с `SetBonus` / `ApplyStatus`. |
| `SetBattleResult` | `outcome` (int `BattleOutcome`) | Финал script → показать результат. Не завершай бой раньше этой команды (даже если `outcome` уже в корне response — playback сначала доигрывает). |

### 6.2 Типичные цепочки (ожидай порядок, не изобретай)

**Melee hit:**

`Approach` → `PlayAnimation` → `ShowDamage` → `SetHp` → (`KillUnit`) → `ReturnToPosition` → `Wait`

**Miss:**

`Approach?` → `PlayAnimation?` → `ShowMiss` → `Wait` → …

**Статус:**

`ApplyStatus` / `TickStatus` / `RemoveStatus` (+ `SetHp` если тик урона)

**Конец:**

… → `DespawnUnit` (cleanup) → `SetBattleResult`

Клиент **не** обязан знать фазы сервера, чтобы играть. `phase` на step — для отладки и опционального UI.

### 6.3 `BattlePhaseType` (справочно)

`Unknown`, `StatusTrigger`, `PerkTrigger`, `SummonSkill`, `SummonAttack`, `UnitSkill`, `NormalAttack`, `CounterAttack`, `Combo1Attack`, `Combo2Attack`, `EnergySkill`, `Death`, `ReturnToPosition`, `Approach`, `UnitCooldown`, `SummonCooldown`, `CounterCooldown`, `ComboCooldown`.

Порядок логики на сервере (GDD), чтобы понимать «почему так в script»:

1. Turn-start bonuses (обе стороны в начале полного хода)
2. Statuses атакующей → trailing `statuses_cooldown` Wait
3. Perks → trailing `perks_cooldown`
4. Summons по `slotIndex` ↑ (skills → attack → return) → `summons_cooldown`
5. Unit skills (не energy) → `units_cooldown`
6. NormalAttack → Counter → Combo1 → Counter → Combo2 → Counter → Return
7. EnergySkill если `Energy >= MaxEnergy` (без крита, без контратаки)

Цель атак: живой `mainUnits` защитника с **минимальным** `slotIndex` (временное правило D6).

Игрок в начале всегда сторона A (атакующая).

---

## 7. Презентация (GDD → Unity)

Детали UX: [`02-presentation-contract.md`](02-presentation-contract.md). Краткий must-have:

### 7.1 Layout

Расстановка на локации — **только клиент** (расстояния N/M/O/X в Unity). Сервер говорит *когда* approach/return, не *куда в пикселях*.

Поддержи сценарии:

- персонаж vs персонаж
- персонаж vs 1 моб
- персонаж vs 2 моба (`slotIndex` 0 и 1)
- ближнее/дальнее положение; слоты саммонов симметричны

### 7.2 Урон / крит / уклон

- Hit flash + knockback из `ShowDamage` (не из локального «попал/не попал»).
- Крит/обычный — только флаги из `ShowDamage`.
- Уклон — только `ShowMiss` / `isEvaded` (если пришло в damage-команде — тоже уважай).

### 7.3 Ranged vs melee

Сервер уже решил melee/range и шлёт `Approach` / `isMelee`.

- Melee: hit в тайминг анимации.
- Ranged: projectile; визуальный hit когда долетел. Логика hit/miss/damage **уже зафиксирована** в следующих командах — не пересчитывай по факту попадания снаряда. Снаряд — косметика, синхронизируй появление flytext с прилётом по возможности.

### 7.4 Статусы / перки / сущности

- Статусы: иконка, стаки, VFX по `statusId` из клиентских ассетов/конфигов UI.
- Перки: rarity frame/icon; **какие** перки активны — из snapshot / команд, не из локальной симуляции.
- Art: `art_name`, skins, idle/attack/hit/death/cast — клиентские бандлы по config id.

### 7.5 Награды в бою

`GrantReward` = показать дроп/флайтекст. Meta-начисление на аккаунт — **вне** этого контракта (future). Не пиши в save «потому что увидел GrantReward», пока отдельно не описан meta-flow.

---

## 8. Что реализовать на клиенте (чеклист модулей)

### 8.1 Обязательный минимум для «всё работает»

1. **DTO / JSON models** зеркалят сервер: `BattleSimulationData`, `TeamSnapshot`, `UnitSnapshot`, `EquipmentSnapshot`, `BattleScriptResponse`, `BattleStep`, `BattleCommand`, enums (`BattleCommandOperation`, `BattleOutcome`, `BattlePhaseType`).
2. **Snapshot builder** — из run/save/encounter собирает thin snapshot (см. §3). Без статов.
3. **Battle API client** — `POST simulate` / `POST replay`, обработка HTTP ошибок (400 validation и т.д.).
4. **Protocol gate** — проверка `protocolVersion`.
5. **Unit registry** — lookup `(id, slotIndex)` → entity; spawn/despawn/kill обновляют его.
6. **Battle command executor** — switch/dispatch по `operation`, async/последовательный playback всех `params`.
7. **Presentation drivers** — move (approach/return/evasion), animator, flytext, HP/energy bars, status icons, death/spawn VFX.
8. **Result UI** — по `SetBattleResult` / `outcome`.
9. **Выключить / обойти** legacy локальный симулятор в боевом path.

### 8.2 Рекомендуемые touchpoints (существующий клиент)

| Задача | Где смотреть в `Lewdventure` |
| --- | --- |
| Сборка request / POST | `Assets/Scripts/Game/Battles/Services/BattleService.cs` |
| Legacy local sim | `.../Simulations/BattleSimulatorService.cs` → deprecate |
| Playback | `.../Simulations/BattlePlaybackSystem.cs` → command executor |
| Snapshot types | `.../Simulations/UnitSnapshot.cs`, `TeamSnapshot.cs` |

Имена могут съехать — ищи по `BattleService` / `BattlePlayback` / snapshot. Контракт важнее путей.

### 8.3 Порядок внедрения (чтобы не утонуть)

1. DTO + HTTP simulate на минимальном 1v1 snapshot (sample #1).
2. Lookup + executor для: `Wait`, `Approach`, `ReturnToPosition`, `PlayAnimation`, `ShowDamage`, `ShowMiss`, `SetHp`, `KillUnit`, `SetBattleResult`.
3. Добавить `SpawnUnit` / `DespawnUnit` / `SetEnergy`.
4. Статусы: `ApplyStatus` / `TickStatus` / `RemoveStatus`.
5. `CastSkill` / `TriggerPerk` / `ShowHeal` / `SetBonus`.
6. `GrantReward` presentation.
7. 2 моба + summons (обязательно проверить duplicate enemy ids + разные `slotIndex`).
8. Replay path для QA.
9. Вырезать gameplay-решения из старого локального sim.

### 8.4 Acceptance criteria

- Один и тот же snapshot + replay `seed` даёт тот же script на сервере; клиент дважды одинаково его отыгрывает визуально.
- HP бара всегда равно последнему `SetHp` для юнита; нет локального «предсказания» урона.
- Два врага с одним `id` не путаются (разные `slotIndex`).
- Пустой summon list → нет лишних `SpawnUnit`; непустой → появляются по командам.
- `targetId = -1` не крашит executor.
- `protocolVersion` mismatch обрабатывается явно.
- Крит/уклон/смерть только из команд, не из клиентского random.

---

## 9. Запреты клиенту (нарушение = баг протокола)

- Пересчитывать формулы урона, характеристик, шансы крита/уклона/комбо/контратаки.
- Крутить свой random для исхода удара.
- Менять HP/energy без `SetHp` / `SetEnergy`.
- Накладывать/снимать статусы «для красоты» без команд.
- Определять победу/поражение до `SetBattleResult` (не считать трупы сам и закрывать бой).
- Слать в simulate `seed` / `maxTurns` / полные статы.
- Резолвить юнита только по `configId` без `slotIndex`.
- Мутировать аккаунт из `GrantReward` без отдельного meta-контракта.
- Игнорировать `Wait` или переставлять порядок команд.

---

## 10. Ошибки и диагностика

| Симптом | Что проверить |
| --- | --- |
| HTTP 400 | Snapshot: unknown skill id, битые id сущностей, валидация полей |
| Пустой / странный бой | `storyLevelId` валиден на сервере; конфиги загружены |
| Клиент «не играет» | Ждёт legacy `events` вместо `steps`/`commands` |
| Не тот юнит анимируется | Lookup без `slotIndex` / перепутаны team слоты |
| HP «прыгает» | Клиент сам меняет HP помимо `SetHp` |
| Нет саммонов | Пустой `summons` в request или executor игнорит `SpawnUnit` |
| Replay расходится | Другой snapshot, другой seed, или серверные конфиги сменились между прогонами |

Логи сервера боя: префикс `[Story][Battle]` (и error-варианты). Не выдумывать новые теги без согласования.

---

## 11. Синхронизация с сервером

При любом изменении протокола:

1. Обновить [`.ai-factory/specs/battle-simulation.md`](../../../.ai-factory/specs/battle-simulation.md) (Decisions).
2. Обновить этот файл и [`02-presentation-contract.md`](02-presentation-contract.md).
3. Обновить клиентские DTO + executor в `Lewdventure`.
4. Поднять `protocolVersion`, если ломается совместимость.
5. Прогнать samples / replay с фиксированным seed.

Клиентский ИИ **не** должен менять серверный баланс «чтобы красиво игралось». Если script неудобен для UX — это запрос на изменение **серверных emit** команд, а не локальный хак.

---

## 12. Связанные документы

| Документ | Зачем |
| --- | --- |
| [`.ai-factory/specs/battle-simulation.md`](../../../.ai-factory/specs/battle-simulation.md) | Канон API + Decisions |
| [`../Server/battle-api.md`](../Server/battle-api.md) | Операционный API |
| [`01-battle-loop.md`](01-battle-loop.md) | GDD цикл хода (серверная логика) |
| [`02-presentation-contract.md`](02-presentation-contract.md) | UX презентации |
| [`00-overview.md`](00-overview.md) | Роли сервер/клиент |
| [`.ai-factory/samples/battle-simulate-50-requests.md`](../../../.ai-factory/samples/battle-simulate-50-requests.md) | Готовые request bodies |

---

## 13. Промпт-заготовка для передачи другому ИИ

Скопируй ниже вместе с этим файлом:

```text
Ты реализуешь Unity-клиент боя Lewdventure строго по документу
Assets/Documents/GDD/10-client-battle-ai.md и канону
.ai-factory/specs/battle-simulation.md (protocolVersion = 1).

Задача:
1) Собрать thin BattleSimulationData из прогресса/encounter.
2) POST /api/battle/simulate (без seed/maxTurns/статов).
3) Проиграть BattleScriptResponse.commands по порядку через executor.
4) Identity юнита = (configId, slotIndex).
5) Не считать урон/крит/уклон/статусы/исход на клиенте.
6) Legacy локальный симулятор не использовать в боевом path.

Сначала: DTO + HTTP + executor для Wait/Approach/Return/PlayAnimation/
ShowDamage/ShowMiss/SetHp/KillUnit/SetBattleResult на 1v1.
Потом: Spawn/Despawn/Energy/Statuses/Skills/Perks/GrantReward/2 mobs/summons.
Не меняй серверный протокол без явного запроса.
```
