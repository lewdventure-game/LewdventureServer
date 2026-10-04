[← Battle API](battle-api.md) · [Server index](README.md) · [Back to Documents](../README.md)

# Config Sync

Как игровые таблицы из Google Sheets попадают в runtime-индексы сервера. Сервер в Google не ходит: листы читает Apps Script от имени человека и присылает строки.

## Summary

| | |
| --- | --- |
| Единица конфигов | снапшот: сырые строки всех листов + версия `sha256:<hex>` |
| Хранилище | Mongo: `config_snapshots`, `config_state`, `config_activations` |
| Публикация dev/stage | Apps Script в таблице читает листы сам → `POST /api/config/upload` (`X-Config-Key`), ключ Google серверу не нужен |
| Публикация prod | GitHub Actions `config-promote` (stage → prod, с одобрением) |
| Runtime | `IGameConfigSetProvider.Current` → неизменяемый `ConfigDistributor` на версию |
| Log tags | `[Config]`, `[Config][Snapshot]` |

## Поток

```text
Google Sheets
  → Apps Script читает листы от имени пользователя и шлёт строки на сервер
  → GameConfigSnapshot (version = sha256 по domain + rows)
  → ConfigSnapshotValidator (ошибки блокируют, warnings по колонкам)
  → GameConfigSetBuilder (новый ConfigDistributor, те же парсеры и managers);
    строки разбираются по одной, ошибка значения не роняет сборку, а называет лист, строку и колонку
  → Mongo config_snapshots (идемпотентно по version)
  → активация в транзакции: config_state.active + запись в config_activations
  → IGameConfigSetProvider.Swap (атомарная подмена, бой в полёте дорабатывает на старой версии)
```

Одинаковые таблицы дают одинаковую версию, повторная публикация ничего не меняет. Короткая форма версии в логах и health: `cfg-<12 hex>`.

## Источники при старте

`GameConfig:Source` выбирает, откуда сервер берёт конфиги:

| Source | Где используется | Поведение |
| --- | --- | --- |
| `Mongo` | dev, stage, prod, локальный compose | грузит `PinnedVersion` или активную версию; если активной нет или она не собирается, а задан `BootstrapFilePath` — публикует снимок из файла; при недоступности Mongo — файловый кэш `LocalCachePath` |
| `File` | тесты, CI, нагрузка | снапшот из `FilePath` (например `tests/Lewdventure.Server.GoldenTests/Golden/Fixtures/config-snapshot.v1.json`) |

`FailStartupIfUnavailable=true` роняет старт, если конфиги получить не удалось; иначе сервер поднимается, `/health/ready` отдаёт critical, а бой отвечает `503`.

Перезагрузка активной версии: `ReloadMode=Poll` (dev/stage, раз в `PollIntervalSeconds`) или вручную `POST /admin/config/reload` на ops-порту (prod, `ReloadMode=Manual`; скрипт `config-transfer.sh` делает это сам).

## Несколько версий одновременно

Активная версия — мастер: её получают все, у кого нет закреплённой версии. Кроме неё сервер держит в памяти версии, на которых идут запросы игроков.

| Компонент | Где | Что делает |
| --- | --- | --- |
| `GameConfigSelection` | scoped, Infrastructure | версия на запрос; scoped `IConfigDistributor` берётся из неё, без выбора — мастер на момент первого обращения, и до конца запроса она не меняется |
| `GameConfigSelectionMiddleware` | Api, после авторизации, только с Mongo | для эндпоинтов с `GameConfigRequiredMetadata` и опознанного игрока спрашивает версию у `PlayerConfigVersionResolver` и выбирает её |
| `PlayerConfigVersionResolver` | Infrastructure | версия активного забега игрока (`player_runs.configVersion`), иначе пусто, то есть мастер; сюда же встанут группы экспериментов |
| `GameConfigSetCache` | singleton, Infrastructure | собранные версии: мастер из провайдера, остальные — до 8 штук с вытеснением давно не используемых; прежний мастер после активации остаётся в кэше без пересборки; недостающая версия грузится из `config_snapshots` и собирается один раз |

Если закреплённая версия не загрузилась (снапшот не найден или не собирается), запрос идёт на мастере, в лог пишется warning `[Config][Snapshot] pinned version unavailable`, в Discord — алерт с ключом `config-pinned-missing:<version>`; повторная попытка загрузки — не раньше чем через 60 секунд. Так игрок не застревает в забеге, хотя правила для него меняются.

`GET /api/config/bundle` отдаёт бандл выбранной версии: клиент в забеге получает конфиги забега. Бандлы кэшируются по версии.

Без Mongo (`Source=File`, тесты) выбора нет, все запросы идут на мастере.

## Endpoints

| Endpoint | Порт | Доступ | Назначение |
| --- | --- | --- | --- |
| `GET /api/config/bundle` | public | игровой токен (или открыт, если авторизация выключена) | активный снапшот в формате бандла для клиентского ядра боя; `ETag` + `If-None-Match` → `304` |
| `POST /api/config/upload` | public | `X-Config-Key`, только если `ConfigPublisher:Enabled` (dev/stage/local) | приём строк листов от Apps Script → валидация → сохранение → активация; ключ Google не нужен |
| `GET /api/config/sheets` | public | `X-Config-Key` | список доменов с id таблиц и диапазонами: Apps Script берёт его, чтобы не дублировать настройки |
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

## Проверки данных мобов

Полностью пустые строки листа (все колонки кроме `name` и `is_off` пустые) сервер пропускает: такие строки-заглушки встречаются в листах и не должны ломать загрузку конфигов. Домены из списка необязательных (сейчас `Equipment_promotes`) при отсутствии в снапшоте дают предупреждение, а не ошибку — иначе новый лист уронил бы сервер до первой публикации.

Лист `Skills` проверяется отдельно (`SkillComponentValidator`): неизвестное условие или действие в `triggers`/`actions` — ошибка, незнакомый параметр, запятая внутри `{}` вместо двоеточия и аргументы без фигурных скобок — предупреждения. Разделители по общему правилу: первый порядок `;`, второй `,`, третий `:`. Подробности — [`docs/gdd/12-skills.md`](../gdd/12-skills.md).

Кроме колонок валидатор смотрит внутрь `other_characteristics` листа `Enemies` и предупреждает, если:

- колонка пустая — все характеристики моба станут нулями;
- в строке нет части ожидаемых ключей — они тоже станут нулями;
- шанс механики больше нуля, а множитель равен нулю (`crit_chance = 0.2`, `crit_multiplier = 0`) — механика сработает, но урона не нанесёт.

Это предупреждения, а не ошибки: публикация не блокируется, но в диалоге таблицы видно, что данные неполные.

Диапазон листа определяет сам Apps Script (`getDataRange`), сервер хранит только каталог таблиц `ConfigSheets:Sheets` (`appsettings.json`), менять его только по решению владельца.

### Какая вкладка публикуется

Домены (18 листов) и таблицы соотносятся один к одному: домен определяется по `SpreadsheetId` из каталога, а внутри таблицы Apps Script берёт **первую видимую вкладку**. Какая вкладка открыта у геймдизайнера и как она называется — не важно.

Скрытые вкладки пропускаются (`isSheetHidden`). До 2026-10-02 скрипт брал `getSheets()[0]`, то есть первую вкладку **по индексу**, а скрытые в этот индекс тоже входят: скрытый старый лист скиллов стоял первым, и сервер получал его вместо нового, который геймдизайнер видел как первый. Теперь в диалоге публикации печатается список «домен ← название прочитанной вкладки и диапазон» — по нему сразу видно, что ушло на сервер.

Отсюда правила работы с таблицей:

- лишние вкладки (черновики, расчёты, старые версии) допустимы, но держать их нужно **правее** рабочей;
- если слева окажется не та вкладка, публикация упадёт на валидаторе («нет обязательной колонки») — либо, если колонки случайно совпали, опубликуются черновые данные;
- два домена одной таблицей двумя вкладками не положить: нужна отдельная таблица и запись в `ConfigSheets:Sheets`.

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
| Characters | `Characters` | `id`, `is_melee`, `promote_id` (ссылка на набор в `Character_promotes`), `skill_ids` и `promote_to_skill_levels` — списки `;`, `art_name`. Бонусных колонок больше нет |
| Character_promotes | `Character_promotes` | `id` = набор промоутов (ссылка из `Characters.promote_id`), `promote_level`, `frame_id`, `copies_to_upgrade`, награды тремя колонками `reward_types` / `reward_ids` / `reward_values` через `;` |
| Bonuses | `Bonuses` | dictionary by id; `work_mode` — сырая строка на mapper, parse через `BonusWorkModeParser` |
| Statuses | `Statuses` | |
| Summons | `Summons` | `id`, `art_name`, `is_melee`, `rarity`, `dmg_on_lvls`, `attack_cooldown`, `mastery_id`, `level_pattern_id`, `skill_ids`, `mastery_for_skills`, `skill_upgrade_ids` (три последних — параллельные списки через `;`) |
| Summon_levels | `SummonLevels` | `id`, `pattern_id`, `level` (уровень, на который переходит саммон), `resource_types/ids/values` |
| Summon_masteries | `SummonMasteries` | composite key `id`+`mastery_level`; `copies_to_upgrade`, `dmg_multiplier`, `reward_types/ids/values`, `frame_id`. До 2026-10-04 лист назывался `Mastery` |
| Skill_promotes | `SkillPromotes` | `id`, `pattern_id`, `level` (уровень, на который переходит скилл), `resource_types/ids/values`, `level_to_unlock` (уровень саммона) |
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
| Skills | `Skills` | `id` + `type` + `parameters`; lookup по numeric id или `type` (`fireball`, `energy`) |

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
