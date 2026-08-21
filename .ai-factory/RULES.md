# Project Rules

> Короткие аксиомы проекта. Загружаются автоматически в `/aif-implement`.

## Rules

- Новый C# runtime-код: no LINQ, `_camelCase` private fields, `internal sealed`, мелкие методы с guard clauses.
- Mapper configs — data-only; lookup, indexing и caching — в runtime managers.
- DI-зависимости и обязательные config fields — not-null по контракту; без лишних null checks.
- Battle-изменения: читать `.ai-factory/specs/battle-simulation.md` перед правками.
- Не выполнять destructive git-команды без явного запроса.
- Не редактировать Google Sheet IDs и JSON-конфиги без явного запроса.
- В планах (`/aif-plan`, `.ai-factory/plans/`, Cursor plan) **запрещена** секция `## Original Request` — не писать никогда.
