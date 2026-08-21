# Implementation Plan: Разложить GDD по документам в Assets/Documents

Branch: master
Created: 2026-08-04

## Settings
- Testing: no
- Logging: verbose
- Docs: yes

## Constraints
- Документы: `Assets/Documents/GDD/`; серверные API/sync — `Assets/Documents/Server/`)
- Сервер = источник истины; клиент = презентер
- Scope сейчас: битва и связанное; профили/валюты/сейвы — future
- Не описывать сервер как «только headless battle sim API»
- Лог-теги: смотреть существующие (`[Error]`, `[Error][Story][Battle]`); **перед новым тегом — спросить**
- Никакой статики в коде (когда дойдём до реализации)
- Тесты не добавлять

## Commit Plan
- **Commit 1** (после tasks 1–4): каркас папки + overview + battle + presentation
- **Commit 2** (после tasks 5–8): statuses, characteristics, perks, entities
- **Commit 3** (после tasks 9–11): equipment, triggers/rewards, roadmap + index

## Tasks

### Phase 1: Каркас и ядро
- [x] Task 1: Создать `Assets/Documents/README.md` — оглавление и правила папки
- [x] Task 2: Создать `00-overview.md` — роль сервера/клиента, scope, лог-теги, запрет static
- [x] Task 3: Создать `01-battle-loop.md` — логика боя из GDD (без Unity-таймингов анимаций)
- [x] Task 4: Создать `02-presentation-contract.md` — обязанности клиента + mapping на battle script/commands

### Phase 2: Боевые подсистемы
- [x] Task 5: Создать `03-statuses.md`
- [x] Task 6: Создать `04-characteristics-bonuses.md`
- [x] Task 7: Создать `05-perks.md`
- [x] Task 8: Создать `06-entities.md` (персонажи, мобы, саммоны)

### Phase 3: Остальное + roadmap
- [x] Task 9: Создать `07-equipment.md`
- [x] Task 10: Создать `08-triggers-rewards.md`
- [x] Task 11: Создать `09-implementation-roadmap.md` — краткий план реализации battle-скоупа
