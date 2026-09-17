[← Runbooks](README.md)

# Golden-тесты

Страховка механик боя: ответы `/api/battle/replay` сравниваются байт-в-байт с эталонами. Любая правка, меняющая бой (формулы, порядок RNG, поля JSON, порядок команд), роняет тесты.

## Что внутри

```text
tests/Lewdventure.Server.GoldenTests/
  Golden/Fixtures/config-snapshot.v1.json   замороженный снапшот конфигов
  Golden/Cases/<NNN-name>/request.json      тело запроса без seed
  Golden/Cases/<NNN-name>/seed-<seed>.response.json
  Golden/Cases/<NNN-name>/expected-status.txt  ожидаемый HTTP-код, если не 200
  Infrastructure/                           хост (WebApplicationFactory), каталог кейсов, сверка
```

- Большинство кейсов — позитивные сценарии. Кейсы с `expected-status.txt` фиксируют код, отличный от 200: `041`–`049` — ошибки валидации (неизвестные id, пустая команда), `035`, `040` — 400, потому что в фикстуре нет story level 6 и 3, `95x` — невалидные тела (пустое, битый JSON, пустой объект), `901` — известный баг с непустым `equipments` (500, см. KB H9). Исправление бага поменяет эталон этого кейса.
- Seeds: `1`, `42`, `1337`, `123456789`, `18446744073709551615`.
- Хост поднимает API в окружении `Testing` на `File`-источнике конфигов с фикстурой.

| Тест | Что проверяет |
| --- | --- |
| `ReplayGoldenTests` | каждый кейс × seed совпадает с эталоном |
| `SimulateReplayRoundTripTests` | `simulate` и `replay` с тем же seed дают одинаковый бой |
| `GoldenOrderIndependenceTests` | порядок запросов не влияет на ответы |
| `CultureInvarianceTests` | `ru-RU` и `en-US` дают одинаковые байты |
| `ParallelReplayTests` | параллельные replay не мешают друг другу |
| `ConfigSwapUnderLoadTests` | подмена конфигов во время нагрузки не портит ответы |
| `ConfigFixtureCaptureTests` | explicit: снятие фикстуры из живых таблиц |

## Запуск

```bash
dotnet test tests/Lewdventure.Server.GoldenTests -c Release
```

При расхождении дифф пишется в `tests/Lewdventure.Server.GoldenTests/TestResults/golden-diff/`.

## Осознанное изменение механики

1. Сделать правку механики.
2. Убедиться, что упали именно ожидаемые кейсы, посмотреть дифф.
3. Перезаписать эталоны:

```bash
LEWD_GOLDEN_UPDATE=1 dotnet test tests/Lewdventure.Server.GoldenTests -c Release
```

4. Просмотреть `git diff tests/Lewdventure.Server.GoldenTests/Golden/Cases` — изменились только ожидаемые ответы.
5. Отдельный коммит «Бой: обновление эталонов — <причина>» вместе с правкой механики или сразу после неё. Синхронизировать клиент, если поменялся протокол.

## Новый кейс

1. Папка `Golden/Cases/<следующий номер>-<описание>/request.json` — тело запроса без `seed`.
2. `LEWD_GOLDEN_UPDATE=1 dotnet test tests/Lewdventure.Server.GoldenTests -c Release` создаст `seed-*.response.json`.
3. Проверить ответы глазами, закоммитить.

## Обновление фикстуры конфигов

Фикстура заморожена, чтобы правки таблиц не ломали тесты. Обновлять, только когда нужно покрыть новые данные:

1. `dotnet test tests/Lewdventure.Server.GoldenTests -c Release --filter "FullyQualifiedName~ConfigFixtureCaptureTests"` с ключом Google (explicit-тест).
2. Перегенерировать эталоны (`LEWD_GOLDEN_UPDATE=1`), просмотреть дифф.
3. Отдельный коммит «Тесты: новая фикстура конфигов».

Фикстурой пользуются также Benchmarks, LoadTest, CI smoke и docker smoke.
