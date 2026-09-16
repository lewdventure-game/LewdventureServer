# GDD (серверная раскладка)

Источник игровой истины для реализации на сервере Lewdventure.  
Исходный монолит: [`Assets/Lewdventure GDD.md`](../../Lewdventure%20GDD.md).

Эти файлы — рабочая раскладка GDD под серверную ответственность.  
По каждому документу позже можно сделать отдельный подробный план с декомпозицией.

Оглавление Documents: [../README.md](../README.md).  
Серверные docs (API/sync): [../Server/README.md](../Server/README.md).

## Оглавление

| Файл | Содержание |
| --- | --- |
| [00-overview.md](00-overview.md) | Роль сервера и клиента, scope, соглашения |
| [01-battle-loop.md](01-battle-loop.md) | Цикл боя, фазы хода, формулы урона атак |
| [02-presentation-contract.md](02-presentation-contract.md) | Что рисует клиент; связь с battle script |
| [03-statuses.md](03-statuses.md) | Статусы и их механики |
| [04-characteristics-bonuses.md](04-characteristics-bonuses.md) | Характеристики, формулы, бонусы |
| [05-perks.md](05-perks.md) | Перки: механики, очередь, выдача |
| [06-entities.md](06-entities.md) | Персонажи, мобы, саммоны |
| [07-equipment.md](07-equipment.md) | Снаряжение |
| [08-triggers-rewards.md](08-triggers-rewards.md) | Триггеры и награды |
| [09-implementation-roadmap.md](09-implementation-roadmap.md) | Краткий план реализации battle-скоупа |
| [10-client-battle-ai.md](10-client-battle-ai.md) | Handoff для ИИ: что реализовать на Unity-клиенте под protocol |

## Правила документов

1. **Сервер** считает бой, хранит/читает конфиги, отдаёт клиенту результат и команды презентации.
2. **Клиент** только показывает: анимации, позиции, flytext, партиклы, UI.
3. Визуальные детали GDD не выкидываются — они живут в `02-presentation-contract.md` и в секциях «Клиент» внутри тематических файлов.
4. Meta (профили, валюты, сохранения) — только как future scope, не как текущая реализация.
5. Технические имена параметров конфигов (`proc_order`, `status_type`, …) сохраняются как в GDD.
