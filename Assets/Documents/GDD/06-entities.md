# Сущности: персонажи, мобы, саммоны

Источник: GDD «Персонажи», «Мобы», «Саммоны».  
Бой — [`01-battle-loop.md`](01-battle-loop.md). Визуал — [`02-presentation-contract.md`](02-presentation-contract.md).

## Общее правило

Сервер знает id, уровни, пресеты, скиллы и считает их влияние на бой.  
Клиент подставляет art/анимации/звуки по id.

Прокачка/коллекция на аккаунте — future meta; в battle-скоупе сервер получает **уже собранный snapshot** стороны (или собирает его из конфигов + переданных уровней).

---

## Персонажи

Основной юнит игрока.

### Бой

- Использует **итоговые характеристики аккаунта** на момент действия (см. [`04-characteristics-bonuses.md`](04-characteristics-bonuses.md)).
- `is_melee` в Characters: ближник подходит и возвращается; иначе ranged без возврата.
- Есть пассивные скиллы персонажа (не energy); логика уникальна per-skill.
- Energy-скилл оружия — отдельный этап хода (см. battle loop).

### Визуал (клиент)

Battle sprite, idle/attack/hit/death, 3 типа атаки от оружия, cast energy, meta sprite, reward icon.  
Bundle id: `art_name`.

### Получение и прокачка

- Reward type `character`.
- Первый дроп: доступен + `start_bonus_type` / `start_bonus_value` (value из Characters; тип = bonus id в Bonuses).
- Повторки → прокачки по спискам Characters (ниже); overflow → `character_overflow_resource` (meta).

**Прокачки (GDD, battle build):**

- `upgrade_costs` — список стоимостей; **длина = макс. число прокачек**.
- `upgrade_bonus_types[i]` + `upgrade_bonus_values[i]` — бонус **только** за прокачку `i + 1` (ordinal 1:1 с costs). Не «на каждый лвл заново все пары».
- В simulate: число применённых апгрейдов = `min(level - 1, upgrade_costs.Length)` (дальше cap; type/value короче costs → Warning + stop).
- Сила апгрейда = `upgrade_bonus_values[i]`, не `Bonuses.bonus_value` (таблица Bonuses даёт type/operator/work_mode по id).

Для battle сейчас: character id, `level`, skill presets в snapshot.

---

## Мобы

Вражеские юниты. Конфиг Enemies (+ пресеты характеристик).

### Пресеты и характеристики

Характеристики моба задаются итоговыми значениями в конфиге/пресете (могут модифицироваться эффектами игрока в бою).
`is_melee` в Enemies: ближник подходит и возвращается; иначе ranged без возврата.

### Скиллы мобов

Уникальная логика; сервер исполняет в фазах unit skill / energy (если есть).

### Визуал (клиент)

Отдельный раздел GDD «Визуальные ассеты мобов» — art, анимации; сервер шлёт id и события.

### Мульти-моб

Расстановка 1–2 мобов — презентация; логика выбора цели атак — уточнить в детальном плане (сейчас GDD в атаках говорит «юнит защищающейся стороны» — нужна каноническая цель при 2 мобах).

---

## Саммоны

Второстепенные юниты игрока в бою. Конфиги: Summons, Summon_levels, Mastery.

### Бой

Порядок действий — слоты 1→2→3 в фазе саммонов.

**ДМГ саммона (личный):**

`dmg_on_lvls[level] * mastery.dmg_multiplier`

- Не равен ДМГ аккаунта.
- Крит обычной атаки саммона использует КРИТ_МН **атакующего юнита** стороны.
- Скилл саммона: `skill_id`, значения прокачиваются.
- `is_melee` в Summons: ближник подходит и возвращается; иначе ranged без возврата.
- `attack_cooldown`: после фактической атаки (включая промах) N полных ходов до следующей; 0 = каждый ход; не ударил на готовом ходе — кулдаун не стартует. Атака доступна с первого хода.

### Бонусы аккаунту

При уровнях мастерства (`bonus_mastery_levels` + `bonus_types` → Bonuses).  
В battle: уже должны быть учтены в итоговых характеристиках стороны (если meta ещё нет — приходят в snapshot / предрасчёт).

### Редкость / мастерство

- Rarity: rare / epic / legendary (визуал карточки — клиент).
- Mastery: рамка, max level, dmg multiplier, бонусы.

### Получение и прокачка (meta / future)

- Reward `summon`, ресурсы `summon_exp`, `summon_skill_exp`.
- Level reset / mastery copies — аккаунт.

---

## Серверные задачи (кратко)

1. Сборка `UnitState` / `TeamSimulationState` из snapshot + конфигов (не только base constants).
2. Character/enemy skills в фазах хода.
3. Summon skills + summon attack + формулы.
4. Корректные actorId/targetId в script.
5. Spawn/despawn/kill commands для презентации.
