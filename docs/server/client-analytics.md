[← Сервер](README.md) · [API для клиента](client-api.md)

# Аналитика и A/B: что нужно от клиента

Документ для клиентской команды. Серверная часть готова и работает на dev. Клиенту осталось отправлять игровые события и правильно брать конфиги; всё остальное (хранение, контекст игрока, эксперименты, дашборды) делает сервер.

Смотреть, что приходит: `https://admin.lewdventure.online` → «Аналитика» (Grafana, дашборд «Lewdventure: обзор», панель «Последние события»), либо «Игроки» → поиск по `userId`.

## 1. Что сервер уже пишет сам

Эти события клиенту отправлять **не нужно**, сервер знает их точно:

| Событие | Когда | Свойства |
| --- | --- | --- |
| `account_created` | первый `POST /api/auth/device` с новым устройством | `client_version` |
| `experiment_assigned` | игрок попал в группу A/B | `experiment_id`, `group_id`, `new_player` |
| `run_started` | `POST /api/run/start` | `run_id`, `story_level_id`, `stages` |
| `battle_finished` | бой внутри `POST /api/run/advance` | `run_id`, `story_level_id`, `stage_index`, `stage_id`, `event_id`, `outcome`, `steps`, `health_before` |
| `run_finished` | забег пройден, проигран или брошен | `run_id`, `story_level_id`, `status`, `stage_index`, `stages`, `experience_level`, `perks`, `duration_seconds` |

Ко **всем** событиям, и серверным, и клиентским, сервер сам дописывает `user_id`, страну, эксперимент и группу игрока и версию конфигов, на которой он играет. Клиенту про A/B в аналитике думать не нужно.

## 2. Отправка: `POST /api/analytics/events`

Под токеном игрока, как остальные `/api/*` (через `IClient`, с тем же ретраем авторизации). Формат — как в Isekai (Amplitude), но пачкой и с нормальными JSON-типами.

```json
{
  "events": [
    {
      "event_type": "tutorial_step",
      "time": 1759570000123,
      "session_id": 1759569000000,
      "insert_id": "9b2f0c3e5d0a4c1f8e1b7a6d5c4b3a29",
      "platform": "Android",
      "app_version": "0.4.1",
      "device_id": "3f0c…",
      "event_properties": { "step_id": 3, "skipped": false },
      "user_properties": {}
    }
  ]
}
```

| Поле | Как заполнять |
| --- | --- |
| `event_type` | `snake_case`, `[a-z][a-z0-9_.]{0,63}`; иначе событие отклоняется |
| `time` | unix ms момента события (`DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`) |
| `session_id` | unix ms старта сессии, одинаковый у всех событий сессии |
| `insert_id` | новый `Guid.NewGuid().ToString("N")` на каждое событие; при повторной отправке тот же — по нему сервер схлопывает дубли |
| `platform` | `Application.platform.ToString()` |
| `app_version` | `Application.version` |
| `device_id` | `IAuthSession.DeviceId` |
| `event_properties` | объект со свойствами события, до 16 КБ; числа числами, bool — `true/false`, enum — строкой имени |
| `user_properties` | можно не заполнять (`{}`); всё про прогресс сервер знает сам |

`user_id` не передавать: сервер берёт его из токена.

Ответ `202 Accepted`:

```json
{ "accepted": 1, "rejected": 0, "dropped": 0, "stored": true, "errors": [] }
```

| Ситуация | Что делать |
| --- | --- |
| `202` | пачка доставлена, удалить из очереди; `rejected` > 0 — ошибка в коде событий, причины в `errors` (до 10), повторять не нужно |
| `400`, `413` | пачка некорректна, выбросить её, не повторять |
| `401` | обрабатывает `IClient` (refresh/device) |
| `429`, `5xx`, нет сети, таймаут | вернуть пачку в начало очереди, повторить через 5 → 10 → 20 … до 120 секунд |

Лимиты: до 500 событий и 512 КБ на запрос, частота — общий лимит игрока (раз в 15–30 секунд — с большим запасом).

## 3. Как реализовать в клиенте

Требования:

1. **Не блокировать игру.** `Track` только кладёт событие в очередь в памяти; сеть — в фоне.
2. **Очередь переживает перезапуск.** При сворачивании и выходе сохранять очередь (вместе с пачкой «в полёте») в файл в `Application.persistentDataPath`, при старте — читать. Дубли не страшны: их убирает `insert_id`.
3. **Пачки.** Отправлять раз в 20 секунд, сразу при 50 событиях в очереди и при сворачивании. В пачке до 200 событий. Одновременно в полёте одна пачка.
4. **Лимит очереди** — 2000 событий; при переполнении выбрасывать самые старые.
5. **Отправлять только с токеном.** Пока `AccessToken` пуст (до логина), события копятся.
6. **Сессия.** Новая сессия при запуске и после 30 минут в фоне.

В приложении — референсная реализация в стиле проекта (Zenject, `IClient`, `IUpdater`, `ILogService`, `SignalBus`). Она написана по коду клиента на 4 октября 2026, но в Unity не собиралась: сверьте имена API и поправьте под себя. Привязка:

```csharp
Container.BindInterfacesAndSelfTo<AnalyticsService>().AsSingle().NonLazy();
Container.BindInterfacesAndSelfTo<AnalyticsScreenTracker>().AsSingle().NonLazy();
```

В `ApiRoutes` добавить `AnalyticsEvents = Api + "/analytics/events"`, в `LogTag` — значение `Analytics` в конец перечисления.

Использование из игрового кода:

```csharp
_analyticsService.Track("tutorial_step", new Dictionary<string, object> { ["step_id"] = stepId, ["skipped"] = false });
```

## 4. Каталог клиентских событий

То, что знает только клиент. Предложение для демо; имена и свойства можно менять, сервер принимает любые по правилам из раздела 2. Новое событие появляется в Grafana без правок на сервере.

| Событие | Когда | `event_properties` |
| --- | --- | --- |
| `session_start` | запуск или возврат после 30 минут в фоне | `first_launch`, `os`, `device_model`, `memory_mb`, `screen_width`, `screen_height`, `language` |
| `session_end` | выход или новая сессия после долгого фона | `session_seconds` |
| `app_background` / `app_foreground` | сворачивание и возврат | `session_seconds` / `away_seconds` |
| `loading_complete` | закончилась загрузка до главного экрана | `duration_ms`, `from_cache` |
| `screen_open` | показан экран (`ShowScreenSignal`) | `screen` |
| `tutorial_step` | шаг туториала пройден | `step_id`, `skipped` |
| `battle_view_start` / `battle_view_end` | начало и конец проигрывания боя | `run_id`, `stage_index`, `speed`, `skipped`, `watch_seconds` |
| `perk_choice_shown` | показан выбор перков | `run_id`, `options` (массив id) |
| `upgrade_screen_action` | игрок нажал прокачку (саммон, снаряжение) до ответа сервера | `target`, `target_id`, `result` |
| `settings_changed` | изменены настройки | `setting`, `value` |
| `error_shown` | показан экран ошибки | `code`, `screen` |

Результаты действий (выдачи, траты, бои, забеги, прокачка) клиенту дублировать не нужно: истина — на сервере, нужные из них пишем серверными событиями.

## 5. A/B-эксперименты: что важно клиенту

Эксперименты раздают разным игрокам разные снапшоты конфигов. Для клиента правило одно: **конфиги для ядра боя и UI брать с сервера, а не из локального файла**.

- `GET /api/config/bundle` под токеном игрока отдаёт снапшот **этого игрока**: в эксперименте — снапшот группы, в забеге — версию забега, иначе — мастер. `ETag` у разных версий разный.
- Перекачивать бандл после логина и перед стартом забега (`If-None-Match` экономит трафик), а также если `sharedCore.ConfigVersion` не совпал с `run.configVersion`.
- Локальный `ConfigBundle.json` не учитывает эксперименты: если ядро собрано из него, игрок в группе увидит в UI и в предпросмотре боя не те цифры, что считает сервер.

Подробнее о бандле — [client-api.md](client-api.md#конфиги-снапшот-с-сервера), об экспериментах — [experiments.md](experiments.md).

## 6. Как проверить

1. Запустить клиент против dev (`RunMode/Server/Dev`), пройти пару экранов.
2. Через 20–30 секунд открыть Grafana → «Последние события», окружение `analytics_dev`: события с `source = client`, вашим `user_id` и `app_version`.
3. В логах клиента (`LogTag.Analytics`): `Batch sent, events = …`. Если `rejected` > 0 — в логе есть причина.

## Приложение: референсная реализация

Предлагаемое место — `Assets/Scripts/Core/Analytics/`. `ILogService` в проекте без `LogWarning`, поэтому предупреждения идут через `Log`.

### `IAnalyticsService.cs`

```csharp
using System.Collections.Generic;

namespace Core.Analytics
{
    public interface IAnalyticsService
    {
        public void Track(string eventType);

        public void Track(string eventType, Dictionary<string, object> properties);

        public void Flush();
    }
}
```

### `AnalyticsEvents.cs`

```csharp
namespace Core.Analytics
{
    public sealed class AnalyticsEvents
    {
        public const string SessionStart = "session_start";
        public const string SessionEnd = "session_end";
        public const string ScreenOpen = "screen_open";
        public const string AppBackground = "app_background";
        public const string AppForeground = "app_foreground";
    }
}
```

### `AnalyticsEventPayload.cs`

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Core.Analytics
{
    internal sealed class AnalyticsEventPayload
    {
        [JsonProperty("event_type")]
        public string EventType { get; set; } = string.Empty;

        [JsonProperty("time")]
        public long Time { get; set; }

        [JsonProperty("session_id")]
        public long SessionId { get; set; }

        [JsonProperty("insert_id")]
        public string InsertId { get; set; } = string.Empty;

        [JsonProperty("platform")]
        public string Platform { get; set; } = string.Empty;

        [JsonProperty("app_version")]
        public string AppVersion { get; set; } = string.Empty;

        [JsonProperty("device_id")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonProperty("event_properties")]
        public Dictionary<string, object> EventProperties { get; set; } = new();

        [JsonProperty("user_properties")]
        public Dictionary<string, object> UserProperties { get; set; } = new();
    }
}
```

### `AnalyticsBatchRequest.cs`

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Core.Analytics
{
    internal sealed class AnalyticsBatchRequest
    {
        [JsonProperty("events")]
        public List<AnalyticsEventPayload> Events { get; set; } = new();
    }
}
```

### `AnalyticsBatchResponse.cs`

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Core.Analytics
{
    internal sealed class AnalyticsBatchResponse
    {
        [JsonProperty("accepted")]
        public int Accepted { get; set; }

        [JsonProperty("rejected")]
        public int Rejected { get; set; }

        [JsonProperty("dropped")]
        public int Dropped { get; set; }

        [JsonProperty("stored")]
        public bool Stored { get; set; }

        [JsonProperty("errors")]
        public List<string> Errors { get; set; } = new();
    }
}
```

### `AnalyticsService.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Core.Data;
using Core.Json;
using Core.Logging;
using Core.Servers;
using Core.Times;
using Newtonsoft.Json;
using UniRx;
using UnityEngine;
using Zenject;

namespace Core.Analytics
{
    internal sealed class AnalyticsService : IAnalyticsService, IUpdatable, IPausable, IDisposable
    {
        private const string QueueFileName = "analytics-queue.json";
        private const string FirstLaunchKey = "analytics.first_launch_done";
        private const float FlushIntervalSeconds = 20f;
        private const float MinRetryDelaySeconds = 5f;
        private const float MaxRetryDelaySeconds = 120f;
        private const int FlushThreshold = 50;
        private const int MaxBatchSize = 200;
        private const int MaxQueueSize = 2000;
        private const long NewSessionAfterMilliseconds = 30L * 60L * 1000L;

        private readonly IUpdater _updater;
        private readonly IAuthSession _authSession;
        private readonly IClient _client;
        private readonly ILogService _logService;

        private readonly List<AnalyticsEventPayload> _queue = new();
        private readonly List<AnalyticsEventPayload> _sending = new();
        private readonly JsonSerializerSettings _jsonSettings;
        private readonly string _queuePath;
        private readonly string _platform;
        private readonly string _appVersion;

        private IDisposable _sendSubscription;
        private float _untilFlush = FlushIntervalSeconds;
        private float _retryDelay = MinRetryDelaySeconds;
        private long _sessionId;
        private long _sessionStartedAt;
        private long _pausedAt;
        private bool _isPaused;

        [Inject]
        internal AnalyticsService(
            IUpdater updater,
            IAuthSession authSession,
            IApiJson apiJson,
            IClient client,
            ILogService logService)
        {
            _updater = updater;
            _authSession = authSession;
            _client = client;
            _logService = logService;

            _jsonSettings = apiJson.Settings;
            _queuePath = Path.Combine(Application.persistentDataPath, QueueFileName);
            _platform = Application.platform.ToString();
            _appVersion = Application.version;

            LoadQueue();
            StartSession(ConsumeFirstLaunch());

            _updater.AddUpdatable(this);
            _updater.AddPausable(this);
        }

        public void Track(string eventType)
        {
            Enqueue(eventType, new Dictionary<string, object>());
        }

        public void Track(string eventType, Dictionary<string, object> properties)
        {
            Enqueue(eventType, properties == null ? new Dictionary<string, object>() : new Dictionary<string, object>(properties));
        }

        public void Flush()
        {
            _untilFlush = FlushIntervalSeconds;

            if (_sendSubscription != null || _queue.Count == 0 || string.IsNullOrEmpty(_authSession.AccessToken))
                return;

            var count = Math.Min(MaxBatchSize, _queue.Count);

            for (int i = 0; i < count; i++)
            {
                var payload = _queue[i];

                if (string.IsNullOrEmpty(payload.DeviceId))
                    payload.DeviceId = _authSession.DeviceId;

                _sending.Add(payload);
            }

            _queue.RemoveRange(0, count);

            var request = new AnalyticsBatchRequest { Events = new List<AnalyticsEventPayload>(_sending) };

            _sendSubscription = _client.Post<AnalyticsBatchResponse>(ApiRoutes.AnalyticsEvents, request)
                .Subscribe(OnSent, OnFailed);
        }

        public void Tick(float deltaTime)
        {
            _untilFlush -= deltaTime;

            if (_untilFlush <= 0f)
                Flush();
        }

        public void SetPause(bool isPaused)
        {
            if (_isPaused == isPaused)
                return;

            _isPaused = isPaused;

            var now = Now();

            if (isPaused)
            {
                _pausedAt = now;
                Track(AnalyticsEvents.AppBackground, new Dictionary<string, object> { ["session_seconds"] = (now - _sessionStartedAt) / 1000L });
                Flush();
                SaveQueue();

                return;
            }

            var awayMilliseconds = now - _pausedAt;

            if (NewSessionAfterMilliseconds <= awayMilliseconds)
            {
                Track(AnalyticsEvents.SessionEnd, new Dictionary<string, object> { ["session_seconds"] = (_pausedAt - _sessionStartedAt) / 1000L });
                StartSession(false);
            }
            else
            {
                Track(AnalyticsEvents.AppForeground, new Dictionary<string, object> { ["away_seconds"] = awayMilliseconds / 1000L });
            }
        }

        public void Dispose()
        {
            Track(AnalyticsEvents.SessionEnd, new Dictionary<string, object> { ["session_seconds"] = (Now() - _sessionStartedAt) / 1000L });

            _sendSubscription?.Dispose();
            _sendSubscription = null;

            _queue.InsertRange(0, _sending);
            _sending.Clear();

            SaveQueue();

            _updater.RemoveUpdatable(this);
            _updater.RemovePausable(this);
        }

        private void Enqueue(string eventType, Dictionary<string, object> properties)
        {
            if (MaxQueueSize <= _queue.Count)
            {
                _queue.RemoveAt(0);
                _logService.Log(LogTag.Analytics, $"Queue is full, oldest event dropped, size = {_queue.Count}");
            }

            _queue.Add(new AnalyticsEventPayload
            {
                EventType = eventType,
                Time = Now(),
                SessionId = _sessionId,
                InsertId = Guid.NewGuid().ToString("N"),
                Platform = _platform,
                AppVersion = _appVersion,
                DeviceId = _authSession.DeviceId,
                EventProperties = properties,
            });

            if (FlushThreshold <= _queue.Count)
                _untilFlush = 0f;
        }

        private void StartSession(bool isFirstLaunch)
        {
            _sessionStartedAt = Now();
            _sessionId = _sessionStartedAt;

            Track(AnalyticsEvents.SessionStart, new Dictionary<string, object>
            {
                ["first_launch"] = isFirstLaunch,
                ["os"] = SystemInfo.operatingSystem,
                ["device_model"] = SystemInfo.deviceModel,
                ["memory_mb"] = SystemInfo.systemMemorySize,
                ["screen_width"] = Screen.width,
                ["screen_height"] = Screen.height,
                ["language"] = Application.systemLanguage.ToString(),
            });
        }

        private void OnSent(AnalyticsBatchResponse response)
        {
            _sendSubscription = null;
            _retryDelay = MinRetryDelaySeconds;

            if (0 < response.Rejected || 0 < response.Dropped)
                _logService.Log(LogTag.Analytics, $"Batch sent with losses, accepted = {response.Accepted}, rejected = {response.Rejected}, dropped = {response.Dropped}, errors = {string.Join("; ", response.Errors)}");
            else
                _logService.Log(LogTag.Analytics, $"Batch sent, events = {_sending.Count}, stored = {response.Stored}");

            _sending.Clear();

            if (FlushThreshold <= _queue.Count)
                _untilFlush = 0f;
        }

        private void OnFailed(Exception exception)
        {
            _sendSubscription = null;

            if (exception is ServerResponseException serverException && IsPermanent(serverException.Status))
            {
                _logService.Log(LogTag.Analytics, $"Batch rejected, events dropped = {_sending.Count}, status = {serverException.Status}");
                _sending.Clear();

                return;
            }

            _queue.InsertRange(0, _sending);
            _sending.Clear();

            _untilFlush = _retryDelay;
            _retryDelay = Math.Min(_retryDelay * 2f, MaxRetryDelaySeconds);

            _logService.Log(LogTag.Analytics, $"Batch failed, retry in {_untilFlush} s, queued = {_queue.Count}, error = {exception.Message}");
        }

        private bool IsPermanent(ServerStatus status)
        {
            return status == ServerStatus.BadRequest || status == ServerStatus.PayloadTooLarge || status == ServerStatus.UnprocessableEntity;
        }

        private void LoadQueue()
        {
            if (File.Exists(_queuePath) == false)
                return;

            try
            {
                var stored = JsonConvert.DeserializeObject<List<AnalyticsEventPayload>>(File.ReadAllText(_queuePath), _jsonSettings);

                if (stored != null)
                    _queue.AddRange(stored);

                _logService.Log(LogTag.Analytics, $"Queue restored, events = {_queue.Count}");
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException)
            {
                _logService.Log(LogTag.Analytics, $"Queue file is unreadable and ignored, error = {exception.Message}");
            }
        }

        private void SaveQueue()
        {
            var events = new List<AnalyticsEventPayload>(_queue.Count + _sending.Count);

            events.AddRange(_sending);
            events.AddRange(_queue);

            try
            {
                File.WriteAllText(_queuePath, JsonConvert.SerializeObject(events, _jsonSettings));
            }
            catch (IOException exception)
            {
                _logService.Log(LogTag.Analytics, $"Queue save failed, error = {exception.Message}");
            }
        }

        private bool ConsumeFirstLaunch()
        {
            if (PlayerPrefs.GetInt(FirstLaunchKey, 0) == 1)
                return false;

            PlayerPrefs.SetInt(FirstLaunchKey, 1);
            PlayerPrefs.Save();

            return true;
        }

        private long Now()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
```

### `AnalyticsScreenTracker.cs`

```csharp
using System;
using System.Collections.Generic;
using Core.Common;
using Core.Signals;
using Zenject;

namespace Core.Analytics
{
    internal sealed class AnalyticsScreenTracker : IInitializableService, IDisposable
    {
        private readonly SignalBus _signalBus;
        private readonly IAnalyticsService _analyticsService;

        public int Priority => 13;

        [Inject]
        internal AnalyticsScreenTracker(SignalBus signalBus, IAnalyticsService analyticsService)
        {
            _signalBus = signalBus;
            _analyticsService = analyticsService;
        }

        public void Init()
        {
            _signalBus.Subscribe<ShowScreenSignal>(OnShow);
        }

        public void Dispose()
        {
            _signalBus.Unsubscribe<ShowScreenSignal>(OnShow);
        }

        private void OnShow(ShowScreenSignal signal)
        {
            _analyticsService.Track(AnalyticsEvents.ScreenOpen, new Dictionary<string, object> { ["screen"] = signal.ScreenType.ToString() });
        }
    }
}
```
