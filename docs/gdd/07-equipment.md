# Снаряжение

Источник: GDD «Снаряжение».  
Влияние на бой — через бонусы и energy-скилл ([`04-characteristics-bonuses.md`](04-characteristics-bonuses.md), [`01-battle-loop.md`](01-battle-loop.md)).  
Визуал — [`02-presentation-contract.md`](02-presentation-contract.md).

## Роль в battle-скоупе

Сервер должен учитывать экипированные предметы при расчёте характеристик и наличии/параметрах скилла оружия (энергия).

В симуляцию приходит equipped set (ids + levels). Мета на сервере (2026-09-30): выдача предметов наградой, прокачка уровня по `lvlup_types` / `lvlup_values`, сброс уровня с возвратом части ресурсов (`equipment_lvl_drop_proportion`) и трансформация по редкости по `merge_requirements` / `merge_group` / `merge_number` — ручки `/api/player/equipment/level`, `/level/reset`, `/merge`. Лист `Equipments` пока пуст, поэтому правила проверены юнит-тестами на подставном конфиге.

## Визуал (клиент)

Отображение оружия/брони на персонаже, анимации атаки зависят от типа оружия (melee / ranged / magic).  
Сервер передаёт факт атаки/скилла; клиент выбирает клип по типу экипировки.

## Виды снаряжений

Из GDD: разные слоты/типы (оружие ближнего боя и др.).  
Тег `is_melee` **боя** живёт на сущности: Characters / Enemies / Summons. Поле `is_melee` на Equipments — тип предмета, не флаг юнита в симуляции.

## Редкости

Влияют на визуал и баланс (конкретные ступени — в конфиге Equipments).  
Прокачка по редкости (трансформация) — мета, реализована: плата по `merge_requirements` (`equipment_id:<id>` и `equipment_rarity:<редкость>` того же `type`), исходный предмет и плата удаляются, выдаётся предмет той же `merge_group` с `merge_number` + 1 и тем же уровнем, экипированный слот переносится на новый предмет.

## Бонусы к характеристикам

Снаряжение даёт бонусы (часто `if_equipped:equipments:id`).  
Сервер суммирует их в итоговые характеристики до/во время боя.

Парсинг Sheets:

- `equip_bonus_type_1..3` — **id** бонуса из Bonuses **или** техническое имя `BonusType` (snake_case / EnumMember); при имени — first match по типу (Warning при 0/>1).
- `equip_bonus_values_1..3` — значения по уровню через `,` (GDD) или `;` (legacy); индекс = `equipment.level`.

## Скиллы снаряжения

Скилл оружия требует энергию:

- `Equipments.skill_id` мержится в skills персонажа при build (только known skill id; unknown → Warning skip);
- энергия копится за успешные обычные атаки (не при промахе);
- каст только если у юнита есть energy skill **и** `энергия >= ЭНЕРГИЯ_МАКС` — в конце действий юнита (после return);
- урон скилла не критует, можно уклониться, не вызывает контратаку.

## Получение и прокачка (meta / future)

- Выдача через reward placements.
- Прокачка редкости (трансформация) и уровня (`lvlup_types` / `lvlup_values`).
- Сброс уровня с возвратом ресурсов (`equipment_lvl_drop_proportion`).
- Break/ресурсы `equip_break` — вне текущего battle runtime.

## Серверные задачи (кратко)

1. Применять equipment bonuses к `CharacteristicState`.
2. Связать energy gain / energy skill с экипированным оружием.
3. Учитывать `is_melee` / тип оружия в командах презентации (animation id / flags).
4. Не реализовывать inventory UX на сервере в этом scope.
