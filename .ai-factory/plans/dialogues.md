# Диалоги: конфиг, награда, запуски в забеге; иконки бонусов

Общий план и ответы ГД — клиентский репозиторий: `Lewdventure/.ai-factory/plans/dialogues.md`. Здесь — серверная часть. Источник: `Lewdventure GDD_new9.pdf`, раздел «Истории» (переименован в **Dialogues**).

Решения и открытые вопросы — в общем плане. Вопросы ГД закрыты: колонки `dialogues_ids`, `dialogues_triggers`; `on_loss` при сдаче в бою — после сцены проигрыша; N в `on_stage_entered:N` с 1.

## Решения

- Момент показа решает сервер; запуск «показан» только по `complete` от клиента.
- Несколько запусков в одном ответе — по порядку: уровень (порядок `dialogues_ids`), затем награды (порядок выдачи).
- Ветка `andelusiy/dialogues` от `master`; деплой ветки на dev для проверки клиента; в `master` сливает владелец. Иконки бонусов — отдельная ветка `andelusiy/bonus-icons` после диалогов.

## 1. Лист `Dialogues`

Новая папка `src/Lewdventure.Server.GameConfig/Dialogues/`:

- `Configs/Mappers/IDialogueStepMapper.cs`, `DialogueStepMapper.cs` — public (netstandard2.1 для клиентского ядра). Колонки: `id`, `step`, `next_step` (`DelimitedIntArrayConverter`, `;`), `is_fade_on_start`, `dialogue_characters_visible` (строка, разбор `спрайт:процент` через `ParserUtils.ParseToDictionary` с обрезкой переносов), `current_character_loc_name`, `backgrounds`, `music`, `sounds`, `localization_keys` (`DelimitedStringArrayConverter`), `text_speed`. Необязательные колонки — `[OptionalColumn]`.
- `Managers/IDialogueStepMapperManager.cs`, `DialogueStepMapperManager.cs` — `TryGet(dialogueId, step)`, `TryGetFirst(dialogueId)`, `Contains(dialogueId)`.

Регистрация:

| Файл | Что |
| --- | --- |
| `GameConfigs/Snapshots/ConfigDomainNames.cs` | константа `Dialogues`, лист обязательный (таблица уже есть) |
| `GameConfigs/Building/GameConfigSetBuilder.cs` | `ParseDomain<DialogueStepMapper>`, `AddList` |
| `GameConfigs/Validation/ConfigSnapshotValidator.cs` | `_mapperTypes` |
| `Services/ConfigDistributor/IConfigDistributor.cs`, `ConfigDistributor.cs` | `Dialogues` — клиент видит `Configs.Dialogues` |
| `src/Lewdventure.Server.Api/appsettings.json` | `ConfigSheets:Sheets` — `Dialogues` + `1FDgmOrBTFPj2Tzf20r4Y87vKpl3Nxj45Fgr63XWykTU` |

Валидация в `ProgressionDataValidator` (ошибки по-русски, «Лист Dialogues, id X шаг Y: …»):

- `step` уникален в диалоге, первый шаг = 1;
- `next_step` ссылается на шаги этого диалога;
- число `next_step` = число `localization_keys` − 1, если вариантов больше нуля; иначе не больше одного;
- из каждого шага достижима концовка (пустой `next_step`); циклы допустимы;
- процент спрайта 0–100, `text_speed` > 0, первый ключ локализации не пустой.

Тесты: фикстуры `tests/Fixtures/config-snapshot.tests.v1.json`, `tests/Lewdventure.Server.GoldenTests/Golden/Fixtures/config-snapshot.v1.json`, `ConfigSnapshotTests` (число доменов), `AppSettingsTests`, юнит-тесты валидатора на пример ГД (диалоги 1–3 проходят; битый `next_step`, расхождение числа выборов, цикл без выхода — ошибки).

## 2. `Story_levels`: диалоги уровня

- `StoryLevelMapper`: `dialogues_ids` (int[], `;`), `dialogues_triggers` (string[], `;`), обе `[OptionalColumn]`. Пример ГД: `1;2` / `on_start;on_loss`, `1;3` / `on_stage_entered:2;on_win`.
- `Stories/Models/DialogueTrigger` — разбор `on_start`, `on_loss`, `on_win`, `on_stage_entered:N`.
- Валидация: длины равны, id есть в `Dialogues`, N от 1 до числа `stage_ids`.

## 3. Награда `dialogue`

| Файл | Что |
| --- | --- |
| `Rewards/BattleRewardType.cs` | `Dialogue` |
| `Rewards/BattleRewardParser.cs` | `"dialogue"` → `Dialogue` |
| `Infrastructure/Players/RewardApplier.cs` | ветка: запуск `reward:{ledgerId}` в `Dialogues.Pending` профиля, ledger-константа |
| `Battles/Services/BattleRewardService.cs` | мета-награда, как `Character`/`Summon` |
| `ProgressionDataValidator` | id диалога в наградах существует |

`RunEngine.ApplyRewardsAsync` отправит тип в `profileRewards` по умолчанию — проверить тестом.

## 4. Профиль

- `Mongo/Players/PlayerDialoguesDocument.cs`: `Pending: List<PlayerDialogueLaunchDocument>` (`LaunchKey`, `DialogueId`, `Source`, `CreatedAt`), `Completed: List<string>` (launch keys). Поле `Dialogues` в `PlayerProfileDocument` с дефолтом `new()` (`docs/architecture/mongo-conventions.md`).
- Ответ: `PlayerProfileResponse.dialogues.pending`, `PlayerResponseFactory`.
- Админка (`PlayerStoryModel` или новый блок), QA (`QaResponseFactory`, `CheatProfileEditor`), экспорт, сброс прогресса, удаление.

## 5. Запуски в забеге

Запуск `level:{levelId}:{dialogueId}:{trigger}` попадает в ответ, если его нет в `Completed`:

| Триггер | Где |
| --- | --- |
| `on_start` | `RunService.StartAsync`; `GET /current` повторяет, пока не подтверждён |
| `on_stage_entered:N` | `RunEngine.AdvanceAsync` при открытии этапа N |
| `on_win` | шаг, закрывший забег победой (`CompleteAsync`) |
| `on_loss` | `FinishBattleAsync` с `RunFailed`, `AbandonAsync` |

Поле `pendingDialogues` в `RunStepResponse`/`RunResponse` (`RunResponseFactory`). Повтор по `requestId` отдаёт тот же список.

## 6. `POST /api/dialogue/complete`

- Тело: `{ requestId, launchKey, picks: int[] }`.
- Переносит `launchKey` из `Pending` в `Completed` (или добавляет в `Completed` для уровневого запуска), идемпотентно по `requestId`, оптимистичная блокировка по `Rev`.
- `picks` — только аналитика в демо; место для будущих платных выборов и наград за выбор.
- `ApiRoutes`, `PlayerEndpoints` (или `DialogueEndpoints`), `400` на неизвестный `launchKey`.

## 7. Аналитика

`DialogueAnalytics` через `AnalyticsRowFactory.CreateServerEvent`: `dialogue_offered` (launchKey, source), `dialogue_completed` (launchKey, picks, длительность с клиента — по желанию).

## 8. Документация

`docs/server/client-api.md` (профиль, забег, новая ручка, конфиг `Dialogues`), `docs/server/runs.md`, `docs/server/config-sync.md`, `docs/runbooks/configuration.md`, `docs/server/player-state.md`, `docs/gdd/08-triggers-rewards.md`. После — копия API клиенту (`Lewdventure/docs/api.md`).

## 9. Проверка и выкладка

`dotnet build`, unit + golden локально; интеграционные на VPS (`docs/runbooks/local-dev.md`); push в master → CI-деплой dev; публикация листа `Dialogues` из таблицы ГД.

---

## Отдельная задача: `is_displayed` и `icon_name` в `Bonuses`

- `Bonuses/Configs/Mappers/IBonusMapper.cs`, `BonusMapper.cs`: `is_displayed` (bool, `[JsonProperty("is_displayed")]`), `icon_name` (string). Решить с ГД, обязательные ли колонки сразу или `[OptionalColumn]` на время перехода.
- Валидация: при `is_displayed = true` `icon_name` не пустой.
- Фикстуры конфигов, `ConfigSnapshotTests`, docs `config-sync.md` / `configuration.md` / `client-api.md`.
- Клиент после деплоя переходит с вшитого `BonusIconAddress` на `icon_name`.
