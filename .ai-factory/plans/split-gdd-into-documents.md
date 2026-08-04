# Implementation Plan: Разложить GDD по документам в Assets/Documents

Branch: master
Created: 2026-08-04

## Original Request

@Assets/Lewdventure GDD.md 
проанализируй документ
давай с тобой составим план
в отдельной папке раскидай несколько документов
в идеале ещё сделать краткий план по реализации
в будущем по каждому документу пройдёмся и сделаем максимально подробный план со всем декомпозом
я ожидаю, что сервер - источник истины, знает о конфигах, отдаёт данные клиенту
клиент - всегда только презентер, слушает сервер и получает от него истину
пока что на сервере реализуем битву и всё, что с ней связано
в будущем будем хранить профили пользователей, валюты, сохранения и т.д.

поэтому в своих документах не указывай, что LewdventureServer — ASP.NET Core 9 Web API для headless симуляции боёв Lewdventure
этот проект - просто сервер для Lewdventure

не в .ai-factory/gdd/, а в Assets/Documents
режим Full
тесты нет
логирование в будущем да, пока что максимально, при этом мне нужны теги, посмотри как я уже сделал, прежде, чем ставить тег, спроси у меня какой
никакой статики
пункт 5 - если нужен, то да
6 - оставить, чтобы понимать обязанности клиента

## Settings
- Testing: no
- Logging: verbose
- Docs: yes

## Constraints
- Документы: `Assets/Documents/`
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
