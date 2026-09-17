[← Battle API](battle-api.md) · [Server index](README.md) · [Back to Documents](../README.md)

# Config Sync

Как игровые таблицы из Google Sheets попадают в runtime-индексы сервера.

## Summary

| | |
| --- | --- |
| Единица конфигов | снапшот: сырые строки всех листов + версия `sha256:<hex>` |
| Хранилище | Mongo: `config_snapshots`, `config_state`, `config_activations` |
| Публикация dev/stage | Apps Script в таблице → `POST /api/config/publish` (`X-Config-Key`) |
| Публикация prod | GitHub Actions `config-promote` (stage → prod, с одобрением) |
| Runtime | `IGameConfigSetProvider.Current` → неизменяемый `ConfigDistributor` на версию |
| Log tags | `[Config]`, `[Config][Snapshot]` |

## Поток

```text
Google Sheets
  → GoogleSheetsConfigImporter (сырые строки листов)
  → GameConfigSnapshot (version = sha256 по domain + rows)
  → ConfigSnapshotValidator (ошибки блокируют, warnings по колонкам)
  → GameConfigSetBuilder (новый ConfigDistributor, те же парсеры и managers)
  → Mongo config_snapshots (идемпотентно по version)
  → активация в транзакции: config_state.active + запись в config_activations
  → IGameConfigSetProvider.Swap (атомарная подмена, бой в полёте дорабатывает на старой версии)
```

Одинаковые таблицы дают одинаковую версию, повторная публикация ничего не меняет. Короткая форма версии в логах и health: `cfg-<12 hex>`.

## Источники при старте

`GameConfig:Source` выбирает, откуда сервер берёт конфиги:

| Source | Где используется | Поведение |
| --- | --- | --- |
| `Mongo` | dev, stage, prod, локальный compose | грузит `PinnedVersion` или активную версию; если активной нет и `BootstrapFromGoogleSheetsIfEmpty=true` — импортирует из Sheets и активирует; при недоступности Mongo — файловый кэш `LocalCachePath` |
| `GoogleSheets` | `dotnet run` без Mongo | импорт при старте, как раньше |
| `File` | тесты, CI, нагрузка | снапшот из `FilePath` (например `tests/Lewdventure.Server.GoldenTests/Golden/Fixtures/config-snapshot.v1.json`) |

`FailStartupIfUnavailable=true` роняет старт, если конфиги получить не удалось; иначе сервер поднимается, `/health/ready` отдаёт critical, а бой отвечает `503`.

Перезагрузка активной версии: `ReloadMode=Poll` (dev/stage, раз в `PollIntervalSeconds`) или вручную `POST /admin/config/reload` на ops-порту (prod, `ReloadMode=Manual`; скрипт `config-transfer.sh` делает это сам).

## Endpoints

| Endpoint | Порт | Доступ | Назначение |
| --- | --- | --- | --- |
| `POST /api/config/publish` | public | `X-Config-Key`, только если `ConfigPublisher:Enabled` (dev/stage/local) | импорт из Sheets → валидация → сохранение → активация; ответ: version, warnings, errors, diff по доменам |
| `POST /api/config/update` | public | как publish, плюс устаревший заголовок `X-Config-Secret` с warning в логе | alias publish для старых вызовов |
| `GET /api/config/status` | public | `X-Config-Key` | активная и загруженная версии |
| `GET /admin/config/status` | ops | `X-Admin-Key` | состояние провайдера и Mongo |
| `GET /admin/config/snapshots` | ops | `X-Admin-Key` | последние снапшоты |
| `GET /admin/config/snapshots/{version}` | ops | `X-Admin-Key` | экспорт снапшота |
| `POST /admin/config/snapshots` | ops | `X-Admin-Key` | загрузка снапшота файлом |
| `POST /admin/config/activate` | ops | `X-Admin-Key` | активировать сохранённую версию |
| `POST /admin/config/reload` | ops | `X-Admin-Key` | перечитать активную версию |

Ops-порт (9090) никогда не публикуется наружу: на VPS он слушает `127.0.0.1`, Caddy режет `/admin/*` и `/health*`.

## ConfigTool

`tools/Lewdventure.Server.ConfigTool` (образ `lewdventure-config-tool`):

| Команда | Нужен Mongo | Что делает |
| --- | --- | --- |
| `import --out file` | нет | снять снапшот из Google Sheets в файл |
| `validate --file file` | нет | ошибки и warnings по колонкам |
| `hash --file file` | нет | версия снапшота |
| `diff --from a --to b` | нет | изменения по доменам и id |
| `publish --file file [--activate] [--actor name] [--reason text]` | да | сохранить (и активировать) снапшот |
| `activate --version v [--actor name] [--reason text]` | да | активировать сохранённую версию |
| `list` | да | 50 последних снапшотов, `*` — активный |
| `export --version v|active --out file` | да | выгрузить снапшот в файл |
| `status` | да | активная версия |

Процесс для геймдизайнера: [runbooks/config-publish.md](../runbooks/config-publish.md).

## Известные расхождения таблиц и маппинга

Найдены валидатором при снятии фикстуры, логика не менялась:

- Bonuses: колонка в таблице называется `work_modes`, маппер читает `work_mode` — у всех бонусов режим `Unknown` (всегда активен).
- Диапазоны листов отрезают колонки: Characters `skill_ids`; Summons часть полей; Story_levels trigger и multipliers; Story_events `reward_xp_value`; Perks `desc_loc`; Perk_groups `choice_count`.
- Enemies: плоских колонок нет, используется только упакованная `other_characteristics`.
- Equipments: ожидаемых колонок нет.

Диапазоны задаются в `GoogleSheets:Sheets[*].Range` (`appsettings.json`), менять только по решению владельца.

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
| Characters | `Characters` | `start_bonus_type` = **bonus id**; `upgrade_costs` / `upgrade_bonus_types` / `upgrade_bonus_values` — списки `;` (или скаляр → один элемент); апгрейд `i` = `costs[i]` + `types[i]` + `values[i]` |
| Bonuses | `Bonuses` | dictionary by id; `work_mode` — сырая строка на mapper, parse через `BonusWorkModeParser` |
| Statuses | `Statuses` | |
| Summons | `Summons` | `breakout_multis`, `bonus_mastery_levels`, `bonus_types`, `is_melee` |
| Summon_levels | `SummonLevels` | load-only для боя |
| Mastery | `Masteries` | composite key id+level |
| Enemies | `Enemies` | flat client columns **or** packed `other_characteristics` (`key:value;...`); packed overrides flat when non-empty; dual combo keys + legacy `combo_multiplier` |
| Equipments | `Equipments` | может быть пусто; `equip_bonus_type_*` = bonus **id** или имя `BonusType`; `equip_bonus_values_*` по уровню через `,` или `;`; `is_melee`; `skill_id` → inject known energy/skill into character build (unknown skipped) |
| Trainings | `Trainings` | stub: sheet id не wired, manager empty после sync |
| Artifacts | `Artifacts` | stub: sheet id не wired |
| Aspects | `Aspects` | stub: sheet id не wired |
| Story_levels | `StoryLevels` | `enemies_attack_multiplier`, `enemies_health_multiplier` |
| Story_stages | `StoryStages` | `enemy_stats_multiplier` применяется при `stageId > 0` |
| Story_events | `StoryEvents` | load-only |
| Exp_levels_patterns | `ExperienceLevelPatterns` | load-only |
| Perks | `Perks` | |
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
- [Battle simulation spec](../../.ai-factory/specs/battle-simulation.md)
- [Architecture](../../.ai-factory/ARCHITECTURE.md)
