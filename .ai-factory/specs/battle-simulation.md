# Battle Simulation Spec (Server)

> Контракт headless симуляции боя. Синхронизировать с Unity-клиентом Lewdventure при изменении protocol.

## Endpoint

- **POST** `/api/battle/simulate`
- **Body:** `BattleSimulationData` (JSON, camelCase)
- **Response:** `BattleScriptResponse`

## Request (`BattleSimulationData`)

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `teamA` | `TeamSnapshot` | yes | Атакующая сторона |
| `teamB` | `TeamSnapshot` | yes | Защищающая сторона |
| `storyLevelId` | `int` | no | ID уровня для лимита ходов из story config |

## Response (`BattleScriptResponse`)

| Field | Type | Description |
| --- | --- | --- |
| `protocolVersion` | `int` | Версия протокола (сейчас `1`) |
| `seed` | `ulong` | Seed RNG для replay на клиенте |
| `outcome` | `BattleOutcome` | Результат боя |
| `turnsPlayed` | `int` | Сколько ходов отыграно |
| `steps` | `List<BattleStep>` | Пошаговый script |

## `BattleOutcome`

`Unknown = 0`, `TeamAWin`, `TeamBWin`, `Draw`, `Timeout`.

## `BattleStep`

| Field | Description |
| --- | --- |
| `index` | Порядковый номер шага в script |
| `turn` | Номер хода |
| `phase` | `BattlePhaseType` |
| `actorId` | ID действующего юнита |
| `targetId` | ID цели |
| `commands` | Список `BattleCommand` для клиентской презентации |

## `BattleCommand`

| Field | Description |
| --- | --- |
| `operation` | `BattleCommandOperation` enum |
| `params` | `JsonObject` — параметры операции для клиента |

## Simulation Loop (high level)

1. Seed RNG (`SeededRandomService`).
2. Build `TeamSimulationState` для A и B.
3. Loop turns до `maxTurns` или смерти main units.
4. Каждый turn: side A acts, then side B (если противник жив).
5. Emit `BattleStep` + `BattleCommand` на каждую фазу (status, perk, skill, attack, cooldown…).
6. Return outcome + full script.

## Extension Recipe

1. Добавить фазу в `BattlePhaseType` (если нужна новая фаза).
2. Добавить operation в `BattleCommandOperation` (если нужна новая клиентская операция).
3. Реализовать logic в `BattleSimulatorService` или dedicated service.
4. Обновить этот spec и клиентский battle spec в Lewdventure.
5. Добавить unit test с фиксированным seed.

## Config Dependencies

Симуляция читает constants, characters, perks, statuses, story levels через `IConfigDistributor`. Перед тестами убедиться, что configs загружены (`/api/config/update` или test fixture).
