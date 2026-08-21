# Триггеры и награды

Источник: GDD «Триггеры», «Награды».  
Связь с перками/статусами/бонусами — [`05-perks.md`](05-perks.md), [`03-statuses.md`](03-statuses.md), [`04-characteristics-bonuses.md`](04-characteristics-bonuses.md).

## Триггеры

Триггер — условие работы механики. Несколько триггеров на одну механику → **все** должны быть True (AND).

Известные типы (GDD + код `StoryLevelTriggerType`):

| trigger_type | Смысл |
| --- | --- |
| `always_available` | Всегда разрешено (первый уровень и т.п.) |
| story level completed / not completed | Зависимость от прохождения уровней |

Полная таблица `trigger_type` / `trigger_value` — сверить построчно с GDD при детальном плане (исходник содержит доп. строки после заголовка).

### Где нужны сейчас

- Доступность story levels / событий — уже близко к story config на сервере.
- Условия срабатывания перков/скиллов внутри боя — часть battle runtime.

Meta-триггеры прогрессии аккаунта — future.

## Награды

Универсальный формат в параметрах:

`тип:id:кол-во;тип:id:кол-во;...`  
(в части мест GDD — через запятую; при реализации зафиксировать один parser contract).

Частые `reward_type`:

| type | id | value |
| --- | --- | --- |
| `bonus` | id из Bonuses | сколько раз выдать |
| `status` | id из Statuses | кол-во/стаки (по контракту механики) |
| `character` | id Characters | кол-во выдач |
| `summon` | id Summons | кол-во |
| `resource` | ключ ресурса (string) или int id | кол-во |

### Battle-relevant награды

Внутри боя сервер должен уметь применять сразу:

- `bonus` с `work_mode`, влияющим на текущий бой;
- `status` (наложение через perk hit_rewards и т.п.).

### Future (аккаунт / забег)

- character / summon / resource на профиль;
- постоянные bonus (`permanent`, `end_of_game`); run-session re-grant; `next_battles` внутри одного simulate уже декрементируется на battle end;
- выбор перков из группы как награда события.

## Серверные задачи (кратко)

1. Единый reward parser + applicator.
2. Разделение: apply-now-in-battle vs grant-to-account (account — later).
3. Trigger evaluation для story и для combat procs.
4. Не дублировать логику наград на клиенте.
