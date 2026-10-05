# Server docs

API и конфиги LewdventureServer (не GDD).

Оглавление Documents: [../README.md](../README.md).  
GDD: [../gdd/README.md](../gdd/README.md).  
Эксплуатация: [../runbooks/README.md](../runbooks/README.md).

| Файл | Содержание |
| --- | --- |
| [battle-api.md](battle-api.md) | `POST /api/battle/simulate`, `POST /api/battle/replay`, коды ответов, flow, примеры |
| [config-sync.md](config-sync.md) | снапшоты конфигов, публикация, ConfigTool, Google Sheets, managers, источник правды |
| [player-state.md](player-state.md) | аккаунт устройства, токены, профиль игрока, хранение и идемпотентность |
| [runs.md](runs.md) | серверное состояние забега, события, детерминизм, режим авторитета |
| [experiments.md](experiments.md) | A/B-эксперименты на снапшотах конфигов: группы, назначение игроков, admin API |
| [client-analytics.md](client-analytics.md) | для клиентской команды: какие события слать, как реализовать очередь, каталог событий, что важно для A/B |
| [client-qa.md](client-qa.md) | для клиентской команды: показать `userId` и `correlationId`, заголовок версии клиента |
| [qa-tools.md](qa-tools.md) | инструменты QA: тестовые аккаунты, поиск игрока, читы, сброс прогресса, admin API |
| [analytics.md](analytics.md) | аналитика: формат событий для клиента, серверные события, хранение в ClickHouse, дашборд |

Канон protocol: [`.ai-factory/specs/battle-simulation.md`](../../.ai-factory/specs/battle-simulation.md).
