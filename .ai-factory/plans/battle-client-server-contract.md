# План реализации: контракт боя (клиент ↔ сервер)

Branch: none
Created: 2026-08-04

## Settings

- Testing: no
- Logging: verbose
- Docs: yes

---

## Краткий ответ (целевая модель)

**Клиент отправляет** снимок сторон боя (`teamA`, `teamB`) + опциональный контекст уровня.  
**Сервер возвращает** детерминированный battle script: seed, исход и упорядоченные `steps` с `commands` для презентации.

Клиент **не считает** урон, крит, уклонение, статусы и исход — только воспроизводит команды.

---



## Целевой контракт API



### Endpoint

`POST /api/battle/simulate`  
JSON camelCase, `NullValueHandling.Ignore`.

### Request — `BattleSimulationData`


| Поле           | Тип            | Обязательно | Назначение                                          |
| -------------- | -------------- | ----------- | --------------------------------------------------- |
| `teamA`        | `TeamSnapshot` | да          | Атакующая сторона (игрок всегда A в начале боя)     |
| `teamB`        | `TeamSnapshot` | да          | Защищающаяся сторона                                |
| `storyLevelId` | `int`          | нет         | ID story level → сервер берёт `maxTurns` из конфига |


**Не передаём в request:** `seed`, `maxTurns` (серверный derived), полные характеристики юнитов, профиль аккаунта, валюты, инвентарь.

#### `TeamSnapshot`


| Поле        | Тип              | Назначение                              |
| ----------- | ---------------- | --------------------------------------- |
| `mainUnits` | `UnitSnapshot[]` | Главные юниты стороны (персонаж / мобы) |
| `summons`   | `UnitSnapshot[]` | Саммоны игрока (слоты 1→2→3)            |




#### `UnitSnapshot` (целевой минимальный снимок)


| Поле              | Тип        | Назначение                                                                    |
| ----------------- | ---------- | ----------------------------------------------------------------------------- |
| `id`              | `int`      | ID сущности в конфиге (character / enemy / summon)                            |
| `level`           | `int`      | Уровень сущности                                                              |
| `equipmentIds`    | `int[]`    | Экипировка (id из конфига; уровни/rarity — отдельным полем при необходимости) |
| `activePerkIds`   | `int[]`    | Активные перки стороны/юнита на момент боя                                    |
| `activeSkillIds`  | `string[]` | Активные скиллы (ключи из конфига)                                            |
| `activeStatusIds` | `int[]`    | Стартовые статусы (id из Statuses)                                            |
| `slotIndex`       | `int`      | Позиция в команде (для 2 мобов / слотов саммонов)                             |


**Принцип:** snapshot — входные данные для **серверной** сборки `UnitState`. Итоговые HP/DMG/crit/evasion/combo/counter/energy сервер считает из constants + bonuses + equipment + perks + mastery (см. `04-characteristics-bonuses.md`, `06-entities.md`, `07-equipment.md`).

**Стабильные battle id:** `actorId`/`targetId` в script = `id` юнита из snapshot (не ECS entity id клиента). Клиент маппит `id` → визуальную сущность на сцене при спавне.

### Response — `BattleScriptResponse`


| Поле              | Тип             | Назначение                                                   |
| ----------------- | --------------- | ------------------------------------------------------------ |
| `protocolVersion` | `int`           | Версия протокола (сейчас `1`)                                |
| `seed`            | `ulong`         | Seed RNG (для replay/debug; клиент **не** пересчитывает бой) |
| `outcome`         | `BattleOutcome` | `TeamAWin`, `TeamBWin`, `Timeout`, `Draw` (последний)        |
| `steps`           | `BattleStep[]`  | Упорядоченный script                                         |


Число ходов клиент вычисляет из `steps` (`max(turn)` / `0` если пусто). Поля `turnsPlayed` нет.

#### `BattleOutcome`


| Value          | Meaning                                                      |
| -------------- | ------------------------------------------------------------ |
| `Unknown = 0`  | Не задан                                                     |
| `TeamAWin = 1` | Победа стороны A                                             |
| `TeamBWin = 2` | Победа стороны B                                             |
| `Timeout = 3`  | Лимит ходов исчерпан                                         |
| `Draw = 4`     | Ничья; недокументировано в GDD — **последнее** значение enum |




#### `BattleStep`


| Поле       | Тип               | Назначение                                       |
| ---------- | ----------------- | ------------------------------------------------ |
| `index`    | `int`             | Порядковый номер шага                            |
| `turn`     | `int`             | Номер хода                                       |
| `phase`    | `BattlePhaseType` | Логическая фаза (StatusTrigger, NormalAttack, …) |
| `actorId`  | `int`             | Кто действует                                    |
| `targetId` | `int`             | Цель; `-1` если цели нет                         |
| `commands` | `BattleCommand[]` | Команды презентации для клиента                  |




#### `BattleCommand`


| Поле        | Тип                      | Назначение         |
| ----------- | ------------------------ | ------------------ |
| `operation` | `BattleCommandOperation` | Тип команды        |
| `params`    | `object` (JSON)          | Параметры операции |


**Ключевые operations и params** (см. `BattleCommandUtils.cs`, `02-presentation-contract.md`):


| Operation                                     | Params (ключи)                                            | Когда                              |
| --------------------------------------------- | --------------------------------------------------------- | ---------------------------------- |
| `Wait`                                        | `seconds`                                                 | Паузы между фазами                 |
| `Approach`                                    | `actorId`, `targetId`, `isMelee`                          | Подход melee                       |
| `ReturnToPosition`                            | `actorId`                                                 | Возврат на позицию                 |
| `PlayAnimation`                               | `actorId`, `animationKey`                                 | Анимация атаки/каста               |
| `ShowDamage`                                  | `actorId`, `targetId`, `damage`, `isCritical`, `isEvaded` | Урон                               |
| `ShowMiss`                                    | `actorId`, `targetId`                                     | Уклонение                          |
| `ShowHeal`                                    | `actorId`, `targetId`, `heal`                             | Лечение                            |
| `SetHp`                                       | `unitId`, `hp`                                            | Авторитетное HP после логики       |
| `SetEnergy`                                   | `unitId`, `energy`                                        | Энергия                            |
| `ApplyStatus` / `TickStatus` / `RemoveStatus` | `statusId`, `stacks`, `durationTurns`, …                  | Статусы                            |
| `TriggerPerk` / `CastSkill`                   | `perkId` / `skillId`, `actorId`, `targetId`               | Перки и скиллы                     |
| `SpawnUnit` / `DespawnUnit` / `KillUnit`      | `unitId`, …                                               | Жизненный цикл                     |
| `SetBattleResult`                             | `outcome`                                                 | Финал script (перед UI результата) |




### Поток данных

```text
Unity (сборка snapshot из ECS + meta)
        │
        ▼ POST BattleSimulationData
LewdventureServer (configs + BattleSimulatorService)
        │
        ▼ BattleScriptResponse (steps/commands)
Unity BattlePlayback (только презентация)
```

---



## Текущее состояние и расхождения



### Сервер (`LewdventureServer`)


| Область                                             | Статус                                                           |
| --------------------------------------------------- | ---------------------------------------------------------------- |
| DTO `BattleSimulationData` / `BattleScriptResponse` | Есть, близко к spec                                              |
| `UnitSnapshot`                                      | Только `id`, `level`, `equipmentIds` — недостаточно для GDD      |
| Симуляция                                           | Каркас turn loop; активна только частичная фаза статусов         |
| RNG seed                                            | Генерируется, но не используется в геймплее                      |
| `BuildTeamState`                                    | Характеристики из global constants; level/equipment игнорируются |
| Deserialization                                     | `ITeamSnapshot` / `IUnitSnapshot` — риск падения JSON bind       |




### Unity-клиент (`D:\Project\Lewdventure`)


| Область        | Статус                                                                       |
| -------------- | ---------------------------------------------------------------------------- |
| Request        | `teamA`, `teamB`, `seed`, `maxTurns` — не совпадает с сервером               |
| `UnitSnapshot` | **Fat snapshot**: все характеристики, perks, statuses, skills, `isMelee`     |
| Response       | Ожидает `IBattleScript` с `events[]` (`BattleEvent` + `BattleActionType`)    |
| Playback       | `BattlePlaybackSystem` играет legacy events, не `BattleStep`/`BattleCommand` |
| `actorId`      | ECS `entityId` — не совместим с config id на сервере                         |




### Вывод

Бой «не работает» не из-за одного бага, а из-за **трёх слоёв рассинхрона**:

1. **Request contract** — разные поля (`storyLevelId` vs `maxTurns`, thin vs fat snapshot).
2. **Response contract** — новый `BattleScriptResponse` vs legacy `BattleEvent[]`.
3. **Simulation depth** — сервер отдаёт почти пустой script (только status tick).

**Рекомендуемый порядок:** сначала зафиксировать и синхронизировать контракт (spec + DTO + клиентский playback), затем наращивать логику симуляции по фазам roadmap (`09-implementation-roadmap.md`).

---



## Ключевые решения (зафиксировать до кода)


| #   | Решение              | Статус                                                                          |
| --- | -------------------- | ------------------------------------------------------------------------------- |
| D1  | Формат response      | `BattleScriptResponse` **+ steps/commands** — канон (spec, GDD `02`)            |
| D2  | Формат snapshot      | **Thin snapshot** на сервере; клиент перестаёт слать характеристики             |
| D3  | `maxTurns`           | Только сервер: из `storyLevelId` (+ fallback constant для sandbox)              |
| D4  | `seed`               | Только сервер генерирует и отдаёт в response; **не** в request                  |
| D5  | Battle unit id       | `(configId, slotIndex)` everywhere in script (step + commands); not id alone   |
| D6  | Цель при 2 мобах     | Зафиксировать в spec: slot 0 main target по умолчанию (уточнить с геймдизайном) |
| D7  | Legacy `BattleEvent` | Deprecate; миграция playback на command executor                                |
| D8  | `turnsPlayed`        | **Убран** из response; клиент считает по `steps[].turn`                         |
| D9  | `targetId` без цели  | Sentinel `-1` (не `0`)                                                          |
| D10 | `Draw`               | Ничья; **последнее** значение enum (`= 4`); в GDD пока нет                      |


---



## Commit Plan

- **Commit 1** (после задач 1–3): `docs: finalize battle client-server contract spec`
- **Commit 2** (после задач 4–6): `feat: align battle request DTO and JSON binding`
- **Commit 3** (после задач 7–9): `feat: emit presentation commands from battle simulator skeleton`
- **Commit 4** (после задач 10–12): `feat: migrate Unity client to BattleScriptResponse playback`

---



## Tasks



### Phase 0: Контракт и документация

- [x] **Task 1: Расширить battle spec до полного контракта**
  - Обновить `.ai-factory/specs/battle-simulation.md`:
    - полный `UnitSnapshot` (perks, skills, statuses, slotIndex);
    - `seed` только в response (не в request);
    - таблица `BattleCommandOperation` → params;
    - правила `actorId`/`targetId`;
    - пример request/response JSON (минимальный бой 1v1).
  - Синхронизировать с `Assets/Documents/GDD/02-presentation-contract.md`.
  - **Logging:** N/A (docs only).
  - **Files:** `.ai-factory/specs/battle-simulation.md`, `Assets/Documents/GDD/02-presentation-contract.md`

- [x] **Task 2: Зафиксировать решения D1–D10 в spec**
  - Добавить секцию «Decisions» с выбранными вариантами (включая `Draw` последним, без `turnsPlayed`, `targetId = -1`).
  - Открытый вопрос D6 (2 моба) — placeholder + TODO для геймдизайна.
  - **Logging:** N/A.
  - **Files:** `.ai-factory/specs/battle-simulation.md`

- [x] **Task 3: Добавить примеры интеграции в docs/**
  - Через `/aif-docs`: страница «Battle API» с request/response, flow diagram, ссылка на Unity playback.
  - **Logging:** N/A.
  - **Files:** `Assets/Documents/Server/battle-api.md` (`paths.docs`)



### Phase 1: Request — что клиент отправляет

- [x] **Task 4: Расширить серверный** `UnitSnapshot` **и** `TeamSnapshot`
  - Добавить поля: `activePerkIds`, `activeSkillIds`, `activeStatusIds`, `slotIndex` (интерфейсы **оставить**).
  - JsonConverter `IUnitSnapshot`→`UnitSnapshot`, `ITeamSnapshot`→`TeamSnapshot` + регистрация в Newtonsoft.
  - **Logging:** `[Story][Battle]` DEBUG — snapshot ids/levels/slot per unit.
  - **Files:** `IUnitSnapshot.cs`, `UnitSnapshot.cs`, converters, `Program.cs`, `BattleSimulatorService.cs`

- [x] **Task 5: Гарантировать server-only** `seed` **в response**
  - Request не принимает `seed`; сервер всегда генерирует и кладёт в response.
  - Убедиться, что клиентский payload с `seed` игнорируется / не биндится.
  - **Logging:** `[Story][Battle]` DEBUG generated seed.
  - **Files:** `BattleSimulationData.cs`, `BattleSimulatorService.cs`

- [x] **Task 6: Сборка** `UnitState` **из snapshot + configs + runtime state**
  - Заменить hardcoded constants/skill/perk на lookup через `IConfigDistributor`.
  - Учесть `equipmentIds`, `level`, стартовые statuses/perks; skills — без Fireball-stub (пусто до GDD/config).
  - Runtime: `ActiveStatus` struct list + tick decrement; EnemyMapper `other_characteristics`.
  - WARN/ERROR при неизвестном config id; mastery miss → ERROR + multiplier 1.0.
  - **Logging:** `[Story][Battle]` DEBUG итоговые характеристики per unit.
  - **Depends on:** Task 4
  - **Files:** `UnitStateBuilder.cs`, `ActiveStatus.cs`, `EnemyMapper`*, `UnitState.cs`, `BattleSimulatorService.cs`, `Program.cs`



### Phase 2: Response — что сервер отдаёт

- [x] **Task 7: BattleScriptBuilder helper**
  - Единый метод добавления `BattleStep` с авто-`index`, фазой, actor/target (`IBattleScriptBuilder` / `BattleScriptBuilder`).
  - **Logging:** `[Story][Battle]` DEBUG каждый step (index, phase, actor, command count).
  - **Files:** `IBattleScriptBuilder.cs`, `BattleScriptBuilder.cs` в `Assets/Game/Battles/Services/`

- [x] **Task 8: Завершить набор** `BattleCommandUtils`
  - Добавить `ShowMiss`, `SpawnUnit`, `DespawnUnit` и недостающие params.
  - **Logging:** N/A (helpers).
  - **Files:** `BattleCommandUtils.cs`

- [x] **Task 9: Emit** `SetBattleResult` **+ death flow**
  - При HP ≤ 0: `Death` phase, `KillUnit`, `SetHp`.
  - В конце боя: финальный step с `SetBattleResult`.
  - **Logging:** `[Story][Battle]` INFO outcome + max turn from steps; DEBUG death events.
  - **Depends on:** Task 7
  - **Files:** `BattleSimulatorService.cs`, `BattleStatusSimulator.cs`



### Phase 3: Минимально играбельный бой (MVP loop)

- [x] **Task 10: Phase B — normal attack chain (MVP)**
  - Раскомментировать/реализовать `SimulateMainUnits`: approach → attack → damage/miss/crit → SetHp → return.
  - Использовать `SeededRandomService` для crit/evasion (один instance на весь `Simulate`).
  - Waits из constants (`units_cooldown`).
  - **Logging:** `[Story][Battle]` DEBUG roll values (crit/evasion); INFO damage applied.
  - **Depends on:** Tasks 6, 7, 8
  - **Files:** `IBattleAttackService.cs`, `BattleAttackService.cs`, `BattleSimulatorService.cs`

- [x] **Task 11: Phase C — status apply/tick (доработка)**
  - Заполнять `ActiveStatusIds` из snapshot; BonusChange + strong bonuses; turn-end expire; initial `ApplyStatus`.
  - **Logging:** `[Story][Battle]` DEBUG trigger order, stacks, damage/heal.
  - **Depends on:** Task 6
  - **Files:** `BattleStatusSimulator.cs`, `BattleSimulatorService.cs`, `StatusBonusApplicator.cs`, parsers

- [x] **Task 12: Валидация endpoint + error responses**
  - 400 при invalid snapshot (unknown id, empty teams).
  - **Logging:** `[Story][Battle]` WARN validation failures; ERROR unhandled exceptions.
  - **Files:** `Program.cs`, `IBattleSimulationValidator.cs`, `BattleSimulationValidator.cs`



### Phase 4: Unity-клиент — приём response

> **SKIPPED** — вне скоупа: только сервер. Клиентские Tasks 13–15 не делаем в этом плане.

- [ ] ~~**Task 13: Обновить request на thin snapshot**~~ — skipped (Unity)
- [ ] ~~**Task 14: DTO** `BattleScriptResponse` **на клиенте**~~ — skipped (Unity)
- [ ] ~~**Task 15: Command playback executor**~~ — skipped (Unity)



### Phase 5: Дальнейшее наращивание (после MVP)

> Перенесено в новый full-план: `.ai-factory/plans/battle-server-loop-completion.md` (counter/combo + D–G, без Unity).

- [ ] ~~**Task 16–18**~~ — см. `battle-server-loop-completion.md`

---



## Пример минимального request (целевой)

```json
{
  "teamA": {
    "mainUnits": [
      { "id": 1, "level": 5, "equipmentIds": [101, 102], "activePerkIds": [3], "activeSkillIds": ["fireball"], "activeStatusIds": [], "slotIndex": 0 }
    ],
    "summons": [
      { "id": 10, "level": 3, "equipmentIds": [], "activePerkIds": [], "activeSkillIds": [], "activeStatusIds": [], "slotIndex": 1 }
    ]
  },
  "teamB": {
    "mainUnits": [
      { "id": 201, "level": 5, "equipmentIds": [], "activePerkIds": [], "activeSkillIds": [], "activeStatusIds": [], "slotIndex": 0 }
    ],
    "summons": []
  },
  "storyLevelId": 42
}
```



## Пример фрагмента response (целевой)

```json
{
  "protocolVersion": 1,
  "seed": 18446744073709551615,
  "outcome": "TeamAWin",
  "steps": [
    {
      "index": 0,
      "turn": 1,
      "phase": "NormalAttack",
      "actorId": 1,
      "targetId": 201,
      "commands": [
        { "operation": "Approach", "params": { "actorId": 1, "targetId": 201, "isMelee": true } },
        { "operation": "PlayAnimation", "params": { "actorId": 1, "animationKey": "attack" } },
        { "operation": "ShowDamage", "params": { "actorId": 1, "targetId": 201, "damage": 42, "isCritical": false, "isEvaded": false } },
        { "operation": "SetHp", "params": { "unitId": 201, "hp": 58 } },
        { "operation": "ReturnToPosition", "params": { "actorId": 1 } },
        { "operation": "Wait", "params": { "seconds": 0.3 } }
      ]
    }
  ]
}
```

---



## Ссылки


| Документ             | Путь                                                                           |
| -------------------- | ------------------------------------------------------------------------------ |
| API spec             | `.ai-factory/specs/battle-simulation.md`                                       |
| Battle loop          | `Assets/Documents/GDD/01-battle-loop.md`                                       |
| Presentation         | `Assets/Documents/GDD/02-presentation-contract.md`                             |
| Entities / snapshot  | `Assets/Documents/GDD/06-entities.md`                                          |
| Roadmap фаз          | `Assets/Documents/GDD/09-implementation-roadmap.md`                            |
| Сервер simulator     | `Assets/Game/Battles/Services/BattleSimulatorService.cs`                       |
| Unity battle service | `D:\Project\Lewdventure\Assets\Scripts\Game\Battles\Services\BattleService.cs` |


