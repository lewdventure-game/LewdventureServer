# План реализации: контракт боя (клиент ↔ сервер)

Branch: none
Created: 2026-08-04

## Original Request

давай подумаем, как правильно сделать, чтобы у меня работал бой
что должен принимать клиент и что должен отдавать сервер?

## Settings

- Testing: no
- Logging: verbose
- Docs: yes

---

## Краткий ответ (целевая модель)

**Клиент отправляет** снимок сторон боя (`teamA`, `teamB`) + опциональный контекст уровня.  
**Сервер возвращает** детерминированный battle script: seed, исход, число ходов и упорядоченные `steps` с `commands` для презентации.

Клиент **не считает** урон, крит, уклонение, статусы и исход — только воспроизводит команды.

---

## Целевой контракт API

### Endpoint

`POST /api/battle/simulate`  
JSON camelCase, `NullValueHandling.Ignore`.

### Request — `BattleSimulationData`

| Поле | Тип | Обязательно | Назначение |
| --- | --- | --- | --- |
| `teamA` | `TeamSnapshot` | да | Атакующая сторона (игрок всегда A в начале боя) |
| `teamB` | `TeamSnapshot` | да | Защищающаяся сторона |
| `storyLevelId` | `int` | нет | ID story level → сервер берёт `maxTurns` из конфига |
| `seed` | `ulong` | нет | Опциональный seed для replay/QA; если `0`/отсутствует — сервер генерирует |

**Не передаём в request:** `maxTurns` (серверный derived), полные характеристики юнитов, профиль аккаунта, валюты, инвентарь.

#### `TeamSnapshot`

| Поле | Тип | Назначение |
| --- | --- | --- |
| `mainUnits` | `UnitSnapshot[]` | Главные юниты стороны (персонаж / мобы) |
| `summons` | `UnitSnapshot[]` | Саммоны игрока (слоты 1→2→3) |

#### `UnitSnapshot` (целевой минимальный снимок)

| Поле | Тип | Назначение |
| --- | --- | --- |
| `id` | `int` | ID сущности в конфиге (character / enemy / summon) |
| `level` | `int` | Уровень сущности |
| `equipmentIds` | `int[]` | Экипировка (id из конфига; уровни/rarity — отдельным полем при необходимости) |
| `activePerkIds` | `int[]` | Активные перки стороны/юнита на момент боя |
| `activeSkillIds` | `string[]` | Активные скиллы (ключи из конфига) |
| `activeStatusIds` | `int[]` | Стартовые статусы (id из Statuses) |
| `slotIndex` | `int` | Позиция в команде (для 2 мобов / слотов саммонов) |

**Принцип:** snapshot — входные данные для **серверной** сборки `UnitState`. Итоговые HP/DMG/crit/evasion/combo/counter/energy сервер считает из constants + bonuses + equipment + perks + mastery (см. `04-characteristics-bonuses.md`, `06-entities.md`, `07-equipment.md`).

**Стабильные battle id:** `actorId`/`targetId` в script = `id` юнита из snapshot (не ECS entity id клиента). Клиент маппит `id` → визуальную сущность на сцене при спавне.

### Response — `BattleScriptResponse`

| Поле | Тип | Назначение |
| --- | --- | --- |
| `protocolVersion` | `int` | Версия протокола (сейчас `1`) |
| `seed` | `ulong` | Seed RNG (для replay/debug; клиент **не** пересчитывает бой) |
| `outcome` | `BattleOutcome` | `TeamAWin`, `TeamBWin`, `Draw`, `Timeout` |
| `turnsPlayed` | `int` | Сколько полных ходов отыграно |
| `steps` | `BattleStep[]` | Упорядоченный script |

#### `BattleStep`

| Поле | Тип | Назначение |
| --- | --- | --- |
| `index` | `int` | Порядковый номер шага |
| `turn` | `int` | Номер хода |
| `phase` | `BattlePhaseType` | Логическая фаза (StatusTrigger, NormalAttack, …) |
| `actorId` | `int` | Кто действует |
| `targetId` | `int` | Цель (0 если нет) |
| `commands` | `BattleCommand[]` | Команды презентации для клиента |

#### `BattleCommand`

| Поле | Тип | Назначение |
| --- | --- | --- |
| `operation` | `BattleCommandOperation` | Тип команды |
| `params` | `object` (JSON) | Параметры операции |

**Ключевые operations и params** (см. `BattleCommandUtils.cs`, `02-presentation-contract.md`):

| Operation | Params (ключи) | Когда |
| --- | --- | --- |
| `Wait` | `seconds` | Паузы между фазами |
| `Approach` | `actorId`, `targetId`, `isMelee` | Подход melee |
| `ReturnToPosition` | `actorId` | Возврат на позицию |
| `PlayAnimation` | `actorId`, `animationKey` | Анимация атаки/каста |
| `ShowDamage` | `actorId`, `targetId`, `damage`, `isCritical`, `isEvaded` | Урон |
| `ShowMiss` | `actorId`, `targetId` | Уклонение |
| `ShowHeal` | `actorId`, `targetId`, `heal` | Лечение |
| `SetHp` | `unitId`, `hp` | Авторитетное HP после логики |
| `SetEnergy` | `unitId`, `energy` | Энергия |
| `ApplyStatus` / `TickStatus` / `RemoveStatus` | `statusId`, `stacks`, `durationTurns`, … | Статусы |
| `TriggerPerk` / `CastSkill` | `perkId` / `skillId`, `actorId`, `targetId` | Перки и скиллы |
| `SpawnUnit` / `DespawnUnit` / `KillUnit` | `unitId`, … | Жизненный цикл |
| `SetBattleResult` | `outcome` | Финал script (перед UI результата) |

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

| Область | Статус |
| --- | --- |
| DTO `BattleSimulationData` / `BattleScriptResponse` | Есть, близко к spec |
| `UnitSnapshot` | Только `id`, `level`, `equipmentIds` — недостаточно для GDD |
| Симуляция | Каркас turn loop; активна только частичная фаза статусов |
| RNG seed | Генерируется, но не используется в геймплее |
| `BuildTeamState` | Характеристики из global constants; level/equipment игнорируются |
| Deserialization | `ITeamSnapshot` / `IUnitSnapshot` — риск падения JSON bind |

### Unity-клиент (`D:\Project\Lewdventure`)

| Область | Статус |
| --- | --- |
| Request | `teamA`, `teamB`, **`seed`**, **`maxTurns`** — не совпадает с сервером |
| `UnitSnapshot` | **Fat snapshot**: все характеристики, perks, statuses, skills, `isMelee` |
| Response | Ожидает **`IBattleScript`** с `events[]` (`BattleEvent` + `BattleActionType`) |
| Playback | `BattlePlaybackSystem` играет legacy events, не `BattleStep`/`BattleCommand` |
| `actorId` | ECS `entityId` — не совместим с config id на сервере |

### Вывод

Бой «не работает» не из-за одного бага, а из-за **трёх слоёв рассинхрона**:

1. **Request contract** — разные поля (`storyLevelId` vs `maxTurns`, thin vs fat snapshot).
2. **Response contract** — новый `BattleScriptResponse` vs legacy `BattleEvent[]`.
3. **Simulation depth** — сервер отдаёт почти пустой script (только status tick).

**Рекомендуемый порядок:** сначала зафиксировать и синхронизировать контракт (spec + DTO + клиентский playback), затем наращивать логику симуляции по фазам roadmap (`09-implementation-roadmap.md`).

---

## Ключевые решения (зафиксировать до кода)

| # | Решение | Рекомендация |
| --- | --- | --- |
| D1 | Формат response | **`BattleScriptResponse` + steps/commands** — канон (spec, GDD `02`) |
| D2 | Формат snapshot | **Thin snapshot** на сервере; клиент перестаёт слать характеристики |
| D3 | `maxTurns` | Только сервер: из `storyLevelId` (+ fallback constant для sandbox) |
| D4 | `seed` | Опционально в request; **всегда** echo в response |
| D5 | Battle unit id | Config `id` + `slotIndex`; клиент строит lookup id→entity при спавне |
| D6 | Цель при 2 мобах | Зафиксировать в spec: slot 0 main target по умолчанию (уточнить с геймдизайном) |
| D7 | Legacy `BattleEvent` | Deprecate; миграция playback на command executor |

---

## Commit Plan

- **Commit 1** (после задач 1–3): `docs: finalize battle client-server contract spec`
- **Commit 2** (после задач 4–6): `feat: align battle request DTO and JSON binding`
- **Commit 3** (после задач 7–9): `feat: emit presentation commands from battle simulator skeleton`
- **Commit 4** (после задач 10–12): `feat: migrate Unity client to BattleScriptResponse playback`

---

## Tasks

### Phase 0: Контракт и документация

- [ ] **Task 1: Расширить battle spec до полного контракта**
  - Обновить `.ai-factory/specs/battle-simulation.md`:
    - полный `UnitSnapshot` (perks, skills, statuses, slotIndex);
    - optional `seed` в request;
    - таблица `BattleCommandOperation` → params;
    - правила `actorId`/`targetId`;
    - пример request/response JSON (минимальный бой 1v1).
  - Синхронизировать с `Assets/Documents/02-presentation-contract.md`.
  - **Logging:** N/A (docs only).
  - **Files:** `.ai-factory/specs/battle-simulation.md`, `Assets/Documents/02-presentation-contract.md`

- [ ] **Task 2: Зафиксировать решения D1–D7 в spec**
  - Добавить секцию «Decisions» с выбранными вариантами.
  - Открытый вопрос D6 (2 моба) — placeholder + TODO для геймдизайна.
  - **Logging:** N/A.
  - **Files:** `.ai-factory/specs/battle-simulation.md`

- [ ] **Task 3: Добавить примеры интеграции в docs/**
  - Через `/aif-docs`: страница «Battle API» с request/response, flow diagram, ссылка на Unity playback.
  - **Logging:** N/A.
  - **Files:** `docs/battle-api.md` (или согласованный путь из `paths.docs`)

### Phase 1: Request — что клиент отправляет

- [ ] **Task 4: Расширить серверный `UnitSnapshot` и `TeamSnapshot`**
  - Добавить поля: `activePerkIds`, `activeSkillIds`, `activeStatusIds`, `slotIndex`.
  - Concrete types вместо interface в DTO (или JsonConverter) для надёжной десериализации.
  - **Logging:** `[Battle][BuildTeamState]` DEBUG — snapshot ids/levels/slot per unit; WARN при неизвестном config id.
  - **Files:** `Assets/Game/Battles/Models/UnitSnapshot.cs`, `TeamSnapshot.cs`, `BattleSimulationData.cs`, интерфейсы

- [ ] **Task 5: Добавить optional `seed` в request**
  - Если `seed > 0` — использовать; иначе генерировать.
  - Всегда возвращать тот же seed в response.
  - **Logging:** `[Battle][Simulate]` DEBUG seed source (client/generated).
  - **Files:** `BattleSimulationData.cs`, `BattleSimulatorService.cs`

- [ ] **Task 6: Сборка `UnitState` из snapshot + configs**
  - Заменить hardcoded constants/skill/perk на lookup через `IConfigDistributor`.
  - Учесть `equipmentIds`, `level`, стартовые statuses/perks/skills.
  - **Logging:** `[Battle][BuildTeamState]` DEBUG итоговые характеристики per unit; ERROR при missing config.
  - **Depends on:** Task 4
  - **Files:** `BattleSimulatorService.cs`, новый `UnitStateBuilder` (или сервис в `Game/Battles/Services/`)

### Phase 2: Response — что сервер отдаёт

- [ ] **Task 7: Step emitter helper**
  - Единый метод добавления `BattleStep` с авто-`index`, фазой, actor/target.
  - **Logging:** `[Battle][Script]` DEBUG каждый step (index, phase, actor, command count).
  - **Files:** новый `BattleStepEmitter.cs` в `Assets/Game/Battles/Services/`

- [ ] **Task 8: Завершить набор `BattleCommandUtils`**
  - Добавить `ShowMiss`, `SpawnUnit`, `DespawnUnit` и недостающие params.
  - **Logging:** N/A (helpers).
  - **Files:** `BattleCommandUtils.cs`

- [ ] **Task 9: Emit `SetBattleResult` + death flow**
  - При HP ≤ 0: `Death` phase, `KillUnit`, `SetHp`.
  - В конце боя: финальный step с `SetBattleResult`.
  - **Logging:** `[Battle][Outcome]` INFO outcome + turnsPlayed; DEBUG death events.
  - **Depends on:** Task 7
  - **Files:** `BattleSimulatorService.cs`

### Phase 3: Минимально играбельный бой (MVP loop)

- [ ] **Task 10: Phase B — normal attack chain (MVP)**
  - Раскомментировать/реализовать `SimulateMainUnits`: approach → attack → damage/miss/crit → SetHp → return.
  - Использовать `SeededRandomService` для crit/evasion.
  - Waits из constants (`units_cooldown`, `counterattack_cooldown`, …).
  - **Logging:** `[Battle][Attack]` DEBUG roll values (crit/evasion); INFO damage applied.
  - **Depends on:** Tasks 6, 7, 8
  - **Files:** `BattleSimulatorService.cs`, возможно `BattleAttackService.cs`

- [ ] **Task 11: Phase C — status apply/tick (доработка)**
  - Заполнять `ActiveStatusIds` из snapshot; BonusChange TODO; turn-end expire.
  - **Logging:** `[Battle][Status]` DEBUG trigger order, stacks, damage/heal.
  - **Depends on:** Task 6
  - **Files:** `BattleStatusSimulator.cs`, `BattleSimulatorService.cs`

- [ ] **Task 12: Валидация endpoint + error responses**
  - 400 при invalid snapshot (unknown id, empty teams).
  - **Logging:** `[Battle][API]` WARN validation failures; ERROR unhandled exceptions.
  - **Files:** `Program.cs`, опционально validator service

### Phase 4: Unity-клиент — приём response

- [ ] **Task 13: Обновить request на thin snapshot**
  - `BattleService.CreateBattle`: слать `storyLevelId`, убрать `maxTurns` из body.
  - `UnitSnapshot` на клиенте: config id, level, equipment, perks, skills, statuses, slotIndex (без характеристик).
  - **Logging:** `[Story][Battle]` DEBUG payload summary (ids only, no secrets).
  - **Files:** `Lewdventure/.../BattleService.cs`, `UnitSnapshot.cs`, `BattleSimulationData.cs`

- [ ] **Task 14: DTO `BattleScriptResponse` на клиенте**
  - Модели `BattleStep`, `BattleCommand`, enums — зеркало сервера.
  - `ServerApiClient.Post<BattleScriptResponse>`.
  - **Logging:** `[Story][Battle]` DEBUG steps count, outcome, seed.
  - **Files:** новые модели в `Lewdventure/Assets/Scripts/Game/Battles/Models/`

- [ ] **Task 15: Command playback executor**
  - Заменить/дополнить `BattlePlaybackSystem`: итерировать `steps[].commands`, dispatch по `operation`.
  - Маппинг `unitId` → ECS entity через таблицу, построенную при спавне.
  - **Logging:** `[Battle][Playback]` DEBUG operation + params; WARN unknown operation.
  - **Depends on:** Tasks 13, 14
  - **Files:** `BattlePlaybackSystem.cs`, новый `BattleCommandExecutor.cs`

### Phase 5: Дальнейшее наращивание (после MVP)

- [ ] **Task 16: Perks phase (Phase D roadmap)**
  - **Logging:** `[Battle][Perk]` DEBUG proc order, perk id, effects.
  - **Files:** perk simulators, `BattleSimulatorService.cs`

- [ ] **Task 17: Summons phase (Phase E roadmap)**
  - **Logging:** `[Battle][Summon]` DEBUG slot, damage formula, crit source.
  - **Files:** summon simulation services

- [ ] **Task 18: Entity skills + equipment energy (Phase F roadmap)**
  - **Logging:** `[Battle][Skill]` DEBUG skill id, targets, energy gate.
  - **Files:** skill services, equipment wiring

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
  "storyLevelId": 42,
  "seed": 0
}
```

## Пример фрагмента response (целевой)

```json
{
  "protocolVersion": 1,
  "seed": 18446744073709551615,
  "outcome": "TeamAWin",
  "turnsPlayed": 3,
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

| Документ | Путь |
| --- | --- |
| API spec | `.ai-factory/specs/battle-simulation.md` |
| Battle loop | `Assets/Documents/01-battle-loop.md` |
| Presentation | `Assets/Documents/02-presentation-contract.md` |
| Entities / snapshot | `Assets/Documents/06-entities.md` |
| Roadmap фаз | `Assets/Documents/09-implementation-roadmap.md` |
| Сервер simulator | `Assets/Game/Battles/Services/BattleSimulatorService.cs` |
| Unity battle service | `D:\Project\Lewdventure\Assets\Scripts\Game\Battles\Services\BattleService.cs` |
