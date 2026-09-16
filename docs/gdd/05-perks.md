# Перки

Источник: GDD «Перки».  
Очередь в бою — [`01-battle-loop.md`](01-battle-loop.md). Визуал/UI выбора — [`02-presentation-contract.md`](02-presentation-contract.md).

## Общее

Перки — улучшения, дающие бонусы к характеристикам и/или боевые механики.

Конфиги:

- **Perks** — конкретный перк (rarity, механика, `perk_parameters`, локализация, `proc_order`, icon).
- **Perk_groups** — группы для выдачи (ids, шансы, `choice_type`, `random_perks_count`, `choice_count`).

Сервер владеет механикой срабатывания в бою и (в будущем) выдачей в забеге.  
Клиент показывает иконки/плашки и UI выбора.

## Выдача (контекст режимов)

Перки могут выдаваться за:

- повышение уровня сюжетного прохождения;
- события сюжетного режима.

Полный run/meta flow — future; для battle нужен уже выбранный набор активных перков на стороне.

## Визуал (клиент)

- Маленькая иконка: art + задник rarity.
- Большая плашка: art + задник + рамка rarity.
- Rarity → цвет: common/grey, rare/blue, legendary/gold, mythic/red.
- `icon_art` в конфиге Perks.

## Механики (сервер)

Числовые значения — в `perk_parameters`, парсятся по механике.

### reward

При получении перка выдаёт награды (обычно bonus).

Параметр: `rewards` (reward format).

### fire_attack / earth_attack / air_attack / water_attack

«Повторяющийся удар» стихией (в GDD — отдельные секции на огонь/землю/воздух/воду).

Общая идея:

- срабатывает в очереди перков по ходу / при условиях механики;
- наносит удар / накладывает статусы через `hit_rewards` (часто `status`);
- параметры шансов и урона — в `perk_parameters`.

**Серверная каноника (battle):**

- `proc_rounds` обязателен; пустой список → перк не триггерится.
- Fire/Earth: шанс награды = `rewards_chance` (Z). Если на цели есть poison (fire) / burn (earth) → `Z * debuff_rewards_chance` (A; поле `A` в GDD отсутствует, канон = `debuff_rewards_chance`). Один apply `hit_rewards` при успехе; chance > 1 clamp к 1.
- Air/Water: на hit нет плоского apply; при стаках burn (air) / poison (water) — ролл `rewards_chance`, при успехе cleanse всех стаков семьи + `hit_rewards` за каждый снятый стак.

### reward_on_target_action

Награда при целевом действии (удар и т.п.).

### resurrection

Воскрешение (особая механика; взаимодействие с «смерть = конец битвы» уточнить в детальном плане).

## Очередность в бою

Активные перки атакующей стороны, которые срабатывают по ходам и доступны сейчас:

- сортировка по `proc_order` возрастанию;
- пауза `perks_cooldown` между срабатываниями.

## Режимы выдачи (`choice_type`)

### Ручной выбор

Клиентский UX: blur, плашки, multi-select до `choice_count`, confirm.

Сервер (когда будет run-flow) отвечает за:

- формирование пула из `perk_ids` группы;
- `random_perks_count` уникальных перков;
- запрет уже активных в забеге;
- применение выбранных перков после confirm.

## Серверные задачи (кратко)

1. Загрузка perk config → runtime perk instances на стороне.
2. Фаза PerkTrigger в симуляторе.
3. Реализация механик по `PerkType` (сейчас частично stub: FireAttack).
4. Награды/статусы через общий reward pipeline.
5. Protocol: `TriggerPerk` + связанные damage/status commands.

Детальный декомпоз — отдельный план по этому документу.
