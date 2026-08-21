# Краткий план реализации (battle-скоуп)

Это **обзорный** roadmap. По каждому документу из `Assets/Documents/GDD/` позже — отдельный подробный план с декомпозицией.

## Целевая модель

```text
Конфиги (Sheets) → MapperManagers → BattleSimulator
                                      ↓
                         детерминированный script
                                      ↓
                              Unity-клиент (презентер)
```

Сервер — источник истины. Клиент только играет команды.

## Уже есть

- Загрузка конфигов + managers.
- API `POST /api/battle/simulate`, DTO, seed в response.
- Каркас turn loop, enums фаз/команд.
- Частичный status tick; perk/skill factories (stubs).



## Ограничения реализации

- **Тесты:** не в этом этапе (пока без обязательных test tasks).
- **Логирование:** verbose; теги согласовывать. Уже есть `[Error]`, `[Error][Story][Battle], [Story][Battle]`. Новый тег — только после вопроса владельцу.
- **Никакой static** для gameplay/state.
- Meta (профили, валюты, сейвы) — не реализовывать сейчас; snapshot/входы боя заложить так, чтобы потом не ломать контракт.



## Фазы реализации



### Phase A — Фундамент характеристик

Документы: `04`, частично `06`/`07`.

1. Сборка итоговых характеристик из constants + snapshot bonuses/equipment/character/enemy.
2. Формулы 1–7 (минимум нужные для боя: HP, DMG, ATK_MN, DEF, crit, evasion, combo, counter, energy).
3. Актуализация характеристик на каждое действие.



### Phase B — Ядро атак юнита

Документы: `01`, `02`.

1. Approach / normal attack / return.
2. Crit / miss / damage apply / SetHp / ShowDamage / ShowMiss.
3. Counter → Combo1 → Counter → Combo2 → Counter.
4. Energy gain и EnergySkill.
5. Cooldown Waits из Constants.



### Phase C — Статусы

Документ: `03`.

1. Active status model + apply/stack/expire.
2. StatusTrigger очередь по `proc_order`.
3. burning / poison / strong / bonus_change.
4. Команды Apply/Tick/Remove.



### Phase D — Перки

Документ: `05`.

1. PerkTrigger очередь.
2. Реализация механик по `PerkType` (начиная с уже начатого FireAttack).
3. hit_rewards → status/bonus через reward pipeline.



### Phase E — Саммоны

Документ: `06`.

1. Слоты 1–3: skills → attack → return.
2. Формула ДМГ саммона + крит от юнита стороны.
3. Spawn/Despawn в script при необходимости.



### Phase F — Скиллы сущностей и экипировка

Документы: `06`, `07`.

1. Character/enemy skills в UnitSkill.
2. Equipment bonuses + weapon energy skill wiring.
3. Флаги презентации (is_melee, animation keys).



### Phase G — Триггеры/награды в бою + полировка protocol

Документы: `08`, `02`, spec.

1. Единый reward applicator для battle.
2. Сверка protocolVersion / команд с клиентом.
3. Детерминизм: фиксированный seed → стабильный script.



## Порядок детальных планов (следующий шаг с человеком)

Рекомендуемый порядок углубления:

1. `01-battle-loop` + `02-presentation-contract` (контракт и фазы)
2. `04-characteristics-bonuses`
3. `03-statuses`
4. `05-perks`
5. `06-entities` (саммоны отдельно при необходимости)
6. `07-equipment`
7. `08-triggers-rewards`



## Вне текущего горизонта

- Профили пользователей, авторизация
- Валюты / инвентарь / сейвы
- Полный story run loop с выбором перков на сервере как session owner
- Щит и прочий non-demo контент из GDD

