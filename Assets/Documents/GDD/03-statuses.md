# Статусы

Источник: живая вкладка GDD «Статусы» (`t.juxy3pjvil6u`).  
Конфиг: Statuses (Sheets / `Statuses.json`). Очередь в бою — [`01-battle-loop.md`](01-battle-loop.md). Визуал — [`02-presentation-contract.md`](02-presentation-contract.md). Бонусы — вкладка 04, не этот документ.

Wire enum из Sheets не меняем: `status_type` 1–5, `status_target` 1=caster / 2=enemy.

## Общее

Статус — эффект на **main юните стороны** (персонаж / моб), не на саммоне.

Сервер:

- резолвит носителя по `status_target` в живой main стороны;
- для DoT хранит id main наложившей стороны и считает тик с его live-статов;
- эмитит `ApplyStatus` / `TickStatus` / `RemoveStatus` только для висящих DoT;
- `bonus_change` не висит: только выдача бонусов в момент apply.

Клиент: иконка по `icon_art_name`, цифра стаков при `> 1`, VFX тика в центре модели по `status_type`.

Парсер `parameters`: named keys, без numeric fallback (`damage_ratio=0`, `max_stacks=1` и т.п.). Нет ключа / пусто / мусор / неизвестный ключ → ERROR, apply нет.

## Иконка

- Параметр `icon_art_name` в Statuses.
- При получении **висящего** статуса — иконка у HP-бара. Спрайт ищется по строке `icon_art_name`, не по `statusId`.
- Несколько стаков одного `statusId` → цифра рядом с иконкой.
- Пустой / неизвестный `icon_art_name` → ERROR, соседний id не подставлять.
- `bonus_change` иконку не показывает: статуса на юните нет.

## Цель статуса (`status_target`)

«Сторона» = main юнит стороны. Саммон-кастер не носитель.

| Значение | Смысл |
| --- | --- |
| `caster` (1) | Main стороны источника |
| `enemy` (2) | Main стороны противника |

Unknown `status_target` / нет живого main → ERROR, apply нет. Default на enemy / target / summon запрещён.

## Типы статусов (`status_type`)

`parameters` — **named keys**, не ordinal. Пример DoT: `damage_ratio:0.1;damage_length:3;max_stacks:5`.

### burning (1) / poison (3)

DoT. Формулы и cap **свои на каждый status id**. `burning_strong` **не** делит `max_stacks` с `burning`. Poison — свой VFX, не огонь.

- Урон стака: live ДМГ **main наложившей стороны** × `damage_ratio` × `(1 - ЗАЩИТА носителя)`
- Крит от live КРИТ_ШАНС / КРИТ_МН того же main
- Уклониться нельзя
- Длится `damage_length` срабатываний урона, затем стак спадает
- Макс стаков: `max_stacks` (> 0); при переполнении новый стак не вешается
- Несколько стаков одного `statusId` суммируют урон за один тик хода
- Стаки независимы по оставшимся тикам

Обязательные ключи: `damage_ratio`, `damage_length` (> 0), `max_stacks` (> 0).

Нет живого main-источника на тике → ERROR, урон 0, **не** статы носителя. `damage_length` всё равно тратится.

### burning_strong (2) / poison_strong (4)

Как соответствующий DoT + каждый активный стак пассивно меняет бонусы носителя.

Ключи DoT плюс непустой `bonuses`. Список троек `bonus:id:count`, разделитель между тройками — **`;`**. Несколько троек в значении — в `[]`, чтобы top-level `;` parameters не рвал список.

`poison_strong` бонусы — про яд, не про ожог.

### bonus_change (5)

Мгновенная выдача бонусов на main-носителе в момент apply. Статус **не висит**: нет duration, cap, `ActiveStatus`, `ApplyStatus` / `TickStatus` / `RemoveStatus`, нет persistent иконки.

Обязателен непустой `bonuses`. Разделитель троек — **`,`**.

Lifetime выданных бонусов — только `work_mode` конфига Bonuses (вкладка 04). Снятия «в конце боя потому что это статус» нет: статуса нет.

Seed snapshot с `bonus_change` как висящим статусом → ERROR.

## Очередь срабатывания

Для статусов с тиком по ходу: очередь **на main атакующей стороны** (слоты по возрастанию), внутри юнита `proc_order` по возрастанию. Это не очередь перков.

Пауза `statuses_cooldown` (Constants) после каждого сработавшего статуса, включая последнее срабатывание юнита. Пустая очередь — без wait. Нет маппера статуса в очереди / нет `statuses_cooldown` → ERROR (не silent skip и не Wait(0) вместо константы).

Если тик убивает носителя: оставшиеся статусы этого юнита не срабатывают. Очередь переходит к следующему живому main или, если живых main нет, к другой стороне. Длительность (`damage_length`) списывается только с тикнувших стаков.

Саммоны в очередь тиков не входят; статус на них не вешается.

## Клиент (playback)

- `ApplyStatus` — иконка по `icon_art_name`, стаки.
- `TickStatus` — one-shot VFX в `Renderer.bounds.center`: burning / burning_strong → fire, poison / poison_strong → poison. Чужой VFX не подменять.
- `SetHp` — если статус изменил HP. Не после каждого статуса и не «статусы SetHp не шлют».
- `bonus_change` этих команд не получает.

## Клиентский `StatusType`

Значения 0–5: `Unknown`, `Burning`, `BurningStrong`, `Poison`, `PoisonStrong`, `BonusChange`. Отдельного Resurrection-статуса нет (воскрешение — перк, не статус).
