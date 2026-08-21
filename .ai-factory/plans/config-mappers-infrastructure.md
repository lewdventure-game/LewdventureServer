# Актуализация config mappers / managers / DI

Branch: master (ветки не создаём — `git.create_branches: false`)
Created: 2026-08-05

## Settings

- Testing: no
- Logging: verbose — однострочные `_logger.LogDebug($"...");` / `LogInformation` / `LogWarning` / `LogError`, пробелы вокруг `=`
- Log tags: `[Story][Battle]` для battle/usage; `[Config]` для GameConfigService / sync
- Docs: yes — обязательный checkpoint через `/aif-docs` в конце
- Roadmap: none

## Источник правды по конфигам

1. Колонки и значения из Google Sheets (табличный вид) — канон.
2. JSON-экспорт клиента/парсеров — вторичен. Если JSON и таблица расходятся — верим таблице.
3. Компактные числовые enum в JSON (Bonuses `bonus_type: 1..7`) не совпадают с серверным `BonusType`. Sheets отдаёт строки → `StringEnumConverter` + snake_case — ок. На числовой JSON из экспорта опираться нельзя.
4. Пустые таблицы (Equipments) — маппер остаётся, sync не падает.

## Inventory (таблица → mapper → gaps)

| Таблица | Manager | Gaps / notes |
| --- | --- | --- |
| Bonuses | dictionary | lookup fixed; work_mode parse only |
| Characters | list | `start_bonus_type` = bonus id → `StartBonusId` |
| Constants | list | OK; ConstantKeys already cover sheet |
| Enemies | list | `other_characteristics` OK with empty `[]` |
| Equipments | list | empty sheet OK |
| Exp_levels_patterns | list | load-only |
| Mastery | list | OK |
| Perk_groups | list | load-only |
| Perks | list | OK |
| Statuses | list | OK |
| Story_events | list | load-only; event_parameters unparsed |
| Story_levels | list | enemies_*_multiplier mapped + applied |
| Story_stages | list | enemy_stats_multiplier map-only |
| Summon_levels | list | load-only |
| Summons | list | `breakout_multis` |

## Follow-up (НЕ в этом плане)

1. Полный apply `work_mode` + `operator` в runtime
2. Перепил моделей статусов / перков / скиллов / бонусных формул
3. Apply `Story_stages.enemy_stats_multiplier` когда в API будет stageId
4. Полный parser `Story_events.event_parameters`

## Tasks

### Phase 0: Канон и инвентарь

- [x] **Task 1: Зафиксировать inventory «таблица → mapper → manager → consumer»**

### Phase 1: Core collections + DI

- [x] **Task 2: Переделать `BaseDictionaryManager`**
- [x] **Task 3: Починить `BonusMapperManager` под dictionary**
- [x] **Task 4: Разгрести `Program.cs` + единый владелец конфигов**
- [x] **Task 5: Обновить `GameConfigService` sync API под list vs dictionary**

### Phase 2: Mapper schema sync

- [x] **Task 6: Summons — `breakout_multis`**
- [x] **Task 7: Story_levels — enemy multipliers**
- [x] **Task 8: Story_stages — `enemy_stats_multiplier`**
- [x] **Task 9: Characters — семантика start/upgrade bonus**
- [x] **Task 10: Сверить остальные домены без ломания**

### Phase 3: work_mode = вариант A (parse only)

- [x] **Task 11: Типизированный `work_mode` parser (без apply)**

### Phase 4: Usage sites

- [x] **Task 12: Починить consumers бонусов после dictionary fix**
- [x] **Task 13: Применить story-level enemy multipliers при билде врагов**
- [x] **Task 14: Вычистить Console/emoji leftover в затронутых файлах**

### Phase 5: Docs + verify

- [x] **Task 15: Docs checkpoint (`/aif-docs`)**
- [x] **Task 16: Сборка + ручной smoke** (`dotnet build` OK; live `/api/config/update` + simulate — вручную у владельца с credentials)
