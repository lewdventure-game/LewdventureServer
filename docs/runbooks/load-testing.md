[← Runbooks](README.md)

# Нагрузочное тестирование

`tools/Lewdventure.Server.LoadTest` гоняет `replay` (или `simulate`) по всем позитивным golden-кейсам против запущенного сервера и сверяет каждый ответ с эталоном. Проверяет сразу пропускную способность и то, что под нагрузкой бой не меняется.

## Запуск

Сервер без rate limit (Local, compose.local или окружение с `RateLimit__Enabled=false`):

```bash
dotnet run -c Release --project tools/Lewdventure.Server.LoadTest -- \
  --target http://localhost:5000 --concurrency 16 --duration 30 --warmup 3
```

| Параметр | По умолчанию | Описание |
| --- | --- | --- |
| `--target` | `http://localhost:5000` | адрес публичного порта |
| `--cases` | ищется вверх от текущей папки | путь к `Golden/Cases` |
| `--endpoint` | `replay` | `replay` или `simulate` (для `simulate` сверка выключается) |
| `--concurrency` | `16` | число параллельных воркеров |
| `--duration` | `30` | длительность замера, секунды |
| `--warmup` | `3` | прогрев перед замером, секунды |
| `--rps` | `0` | целевой RPS на все воркеры, `0` — без ограничения |
| `--max-error-rate` | `0.01` | допустимая доля ошибок |
| `--max-p95-ms` | `0` | порог p95, `0` — без порога |
| `--no-verify` | — | не сверять ответы с эталонами |

Ошибка — любой ответ не `200`, сетевое исключение или несовпадение с эталоном.

Код выхода: `0` — PASS, `1` — FAIL по порогам, `2` — неверные параметры или нет кейсов.

## Отчёт

```text
requests      42109
throughput    4207.03 rps
errors        0 (0 %)
latency p50   2.01 ms
latency p95   12.17 ms
latency p99   24.01 ms
outcome       200 x 42109
result        PASS
```

Ориентир: Release-сборка на машине разработчика, 16 воркеров, фикстура конфигов — около 4200 rps, p95 12 мс, 0 расхождений (2026-09-17).

## Когда запускать

- CI: `docker-smoke` прогоняет 15 секунд на 8 воркерах с `--max-error-rate 0` против контейнера.
- Вручную: перед релизом изменений в симуляции, DI или хосте; сравнить с ориентиром выше.
- Против stage — только с отключённым rate limit на время теста и вне рабочего времени геймдизайнера; против prod не запускать.

## Профилирование

Метрики процесса во время прогона:

```bash
dotnet-counters monitor -n Lewdventure.Server.Api --counters System.Runtime,Microsoft.AspNetCore.Hosting,Lewdventure.Server
```

Микробенчмарки симуляции без HTTP: `dotnet run -c Release --project tests/Lewdventure.Server.Benchmarks`.
