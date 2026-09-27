# Переносимое ядро боя в Unity

Клиент не считает бой сам и не повторяет формулы: он подключает те же сборки, что считают бой на сервере, и переигрывает бой по сиду. Один и тот же код даёт те же числа, поэтому расхождений между сервером и клиентом быть не может.

## Что подключается

Три сборки под `netstandard2.1`:

| Сборка | Что внутри |
| --- | --- |
| `Lewdventure.Server.Contracts.dll` | модели протокола боя: `BattleReplayData`, `BattleSimulationData`, `UnitSnapshot`, `BattleStep`, `BattleCommand`, `CommandType`, `OutcomeType` |
| `Lewdventure.Server.GameConfig.dll` | чтение конфигов: `IConfigDistributor` и мапперы персонажей, мобов, саммонов, экипировки, перков, статусов, скиллов, сюжета; enum'ы `PerkType`, `StatusType`, `BonusType`, `RarityType` |
| `Lewdventure.Server.Battle.dll` | симулятор боя и публичный фасад `BattleCoreFactory` / `IBattleCore` |

Внешняя зависимость одна — `Newtonsoft.Json` (в Unity это пакет `com.unity.nuget.newtonsoft-json`). ASP.NET, Mongo, Google, контейнера внедрения зависимостей и логгера Microsoft в ядре нет.

Сборка сборок:

```bash
bash deploy/scripts/build-unity-core.sh
```

Результат ложится в `out/unity-core`, оттуда — в `Assets/Plugins/BattleCore` клиента.

## Первый запуск: три строки

```csharp
var coreLog = new UnityCoreLog();
var result = new BattleCoreFactory().CreateFromBundle(bundleJson, coreLog);

if (result.Succeeded == false)
{
    Debug.LogError($"[BattleCore] configs rejected: {string.Join("; ", result.Errors)}");

    return;
}

var battleCore = result.BattleCore;
```

`bundleJson` — это содержимое `Assets/Configs/Core/Json/ConfigBundle.json` как есть: фасад читает формат `{"Configs":[{"Name":"Constants","Content":"[...]"}]}` сам. Если конфиги приходят другим путём, есть `CreateFromDomains(IReadOnlyList<CoreConfigDomain>, ICoreLog)`, где `CoreConfigDomain` — имя листа плюс JSON строк.

`result.Warnings` стоит выводить в консоль: там те же предупреждения, что видит геймдизайнер при публикации конфигов (незнакомый тип перка, опечатка в параметрах, отсутствующая колонка).

## Логирование

```csharp
internal sealed class UnityCoreLog : ICoreLog
{
    public void Debug(string message)
    {
        UnityEngine.Debug.Log(message);
    }

    public void Information(string message)
    {
        UnityEngine.Debug.Log(message);
    }

    public void Warning(string message)
    {
        UnityEngine.Debug.LogWarning(message);
    }

    public void Warning(Exception exception, string message)
    {
        UnityEngine.Debug.LogWarning($"{message} {exception.Message}");
    }

    public void Error(string message)
    {
        UnityEngine.Debug.LogError(message);
    }

    public void Error(Exception exception, string message)
    {
        UnityEngine.Debug.LogError($"{message} {exception.Message}");
    }
}
```

В релизной сборке можно передать `new SilentCoreLog()` из ядра: бой пишет много отладочных строк на каждый удар.

## Бой по сиду

```csharp
var settings = new BattleJsonSettingsFactory().Create();
var replayData = JsonConvert.DeserializeObject<BattleReplayData>(responseJson, settings);
var script = battleCore.Replay(replayData);

for (int i = 0; i < script.Steps.Count; i++)
{
    var step = script.Steps[i];

    for (int j = 0; j < step.Commands.Count; j++)
        Play(step.Commands[j]);
}
```

`BattleJsonSettingsFactory` — те же настройки сериализации, что использует сервер, вместе с конвертерами снапшотов. Отдельные настройки на клиенте писать не нужно: расхождение в них означало бы расхождение в бою.

`script.OutcomeType` — исход, `script.Seed` — сид, `script.ProtocolVersion` — версия протокола; на ней стоит падать в явную ошибку, если сервер прислал другую.

Проверить входные данные до симуляции:

```csharp
if (battleCore.TryValidate(simulationData, out var error) == false)
    Debug.LogError($"[BattleCore] {error}");
```

## Режим сида: сервер присылает вход, клиент считает бой

Если в запрос забега добавить `battleDelivery: "seed"`, сервер не пришлёт скрипт — в шаге будет только вход боя и дайджест:

```csharp
var step = runResponse.Step;
var script = battleCore.Replay(step.BattleInput);

if (battleCore.ComputeDigest(script) != step.BattleDigest)
{
    Debug.LogError("[BattleCore] parity mismatch, показываем бой по серверному скрипту");

    return;
}

Play(script);
```

Дайджест — `sha256` от скрипта, сериализованного `BattleJsonSettingsFactory`; сервер считает его тем же кодом. Это дешёвая проверка паритета в бою: если рантаймы разойдутся, клиент это увидит до показа, а не через неверный урон.

Экономия трафика на длинном бое — примерно шестнадцатикратная после gzip (3.9 KB скрипта против 236 B входа).

Итоги боя всё равно берутся из ответа сервера: `currentHealth`, `status`, `appliedRewards`, `profileRev`. Скрипт нужен только чтобы это показать.

## Характеристики для UI

```csharp
var characteristics = battleCore.BuildCharacteristics(unitSnapshot, BattleSide.Attacking, false, storyLevelId, stageId);

healthBar.SetMax(characteristics.MaxHealth);
critLabel.text = characteristics.CriticalChance.ToString("P0");
```

Это тот же расчёт, которым бой собирает юнита: бонусы экипировки, саммонов, перков и статусов уже учтены. Сервер отдаёт те же числа в `GET /api/player/characteristics`, так что экран персонажа можно рисовать и локально, и по ответу сервера — значения совпадут.

## Конфиги для визуала

```csharp
if (battleCore.Configs.Characters.TryGet(characterId, out var characterMapper))
    portrait.Load(characterMapper.ArtName);

if (battleCore.Configs.Enemies.TryGet(enemyId, out var enemyMapper))
    enemyView.Load(enemyMapper.SkinName);
```

Через `battleCore.Configs` доступны все мапперы: персонажи, мобы, саммоны и их уровни, мастерство, экипировка, перки и группы перков, статусы, скиллы, сюжетные уровни, этапы и события, константы, бонусы, паттерны опыта. Enum'ы `PerkType`, `StatusType`, `BonusType`, `BonusOperatorType`, `RarityType` берутся оттуда же — их больше не нужно дублировать в клиенте вручную.

## Версия конфигов

`battleCore.ConfigVersion` считается тем же алгоритмом, что версия снапшота на сервере: sha256 по именам листов и строкам. Если клиентский бандл собран из той же таблицы, версия совпадёт с той, что отдаёт `GET /api/config/status`. Разошлись версии — клиент играет на других данных, и это видно до боя, а не по странному урону.

## IL2CPP

`Newtonsoft.Json` читает модели по `[JsonProperty]`, а разбор `BonusType` идёт по `EnumMemberAttribute` — и то, и другое через рефлексию. Без `link.xml` стриппинг выкидывает нужные члены, и данные разбираются в пустоту:

```xml
<linker>
  <assembly fullname="Lewdventure.Server.Contracts" preserve="all" />
  <assembly fullname="Lewdventure.Server.GameConfig" preserve="all" />
  <assembly fullname="Lewdventure.Server.Battle" preserve="all" />
</linker>
```

## Ручной прогон паритета

Пока бой не проигран клиентским кодом ни разу, все гарантии остаются серверными. Набор для прогона собирается командой:

```bash
dotnet test tests/Lewdventure.Server.GoldenTests -c Release --filter ParityKitExportTests
```

В `out/parity-kit` появятся `ConfigBundle.json` (фикстура в клиентском формате), по четыре файла на кейс (`*.request.json`, `*.expected.json`, `*.rolls.txt`) и `README.md` с шагами. Прогон состоит из шести шагов:

1. Собрать ядро (`deploy/scripts/build-unity-core.sh`), положить три DLL в клиент, добавить `link.xml`.
2. Создать ядро из `ConfigBundle.json` и убедиться, что `ConfigVersion` совпал с версией из `README.md` — иначе сравнивать нечего, данные разные.
3. Прочитать `*.request.json` в `BattleReplayData` настройками `BattleJsonSettingsFactory` и вызвать `Replay`.
4. Сериализовать результат теми же настройками и сравнить строку с `*.expected.json` посимвольно.
5. При расхождении сравнить свою трассу бросков с `*.rolls.txt`: первая разошедшаяся строка называет бросок и место.
6. Повторить в сборке IL2CPP, а не только в редакторе: стриппинг и рантайм там другие.

Пройдены все четыре кейса в редакторе и в IL2CPP — можно переключать клиент на `battleDelivery: "seed"`.

## Чем гарантируется совпадение

- Эталоны: 60 кейсов проигрываются через публичный фасад и сравниваются с ответом HTTP-эндпоинта байт в байт (`BattleCoreFacadeGoldenTests`), плюс 436 обычных эталонов боя.
- Числовая политика: тесты запрещают в боевом коде `Pow`, `Sqrt`, тригонометрию, `FusedMultiplyAdd`, `double`, обращения ко времени, `Guid` и зависимость от порядка обхода словарей.
- Обе сборки (`net10.0` и `netstandard2.1`) собираются в CI на каждом коммите.

## Авторитет остаётся на сервере

Ядро в клиенте считает бой и рисует его, но ничего не выдаёт. Разделение такое:

| Что | Где живёт | Может ли клиент это подделать |
| --- | --- | --- |
| Симуляция боя, команды скрипта, `GrantReward` внутри скрипта | ядро, есть у клиента | да, у себя на экране — и это ни на что не влияет |
| Выдача ресурсов, персонажей, саммонов, экипировки, запись в ledger, `rev` профиля | `Lewdventure.Server.Infrastructure`, `Lewdventure.Server.Runs` — в клиент не поставляются | нет |
| Состав боя (юниты, мобы, бонусы, сид, версия конфигов) | сервер строит сам в `RunSnapshotBuilder` | нет, запрос боя клиент не присылает |

Правила, из которых это следует:

- Сервер никогда не принимает от клиента исход боя. Ран двигается через `POST /api/run/advance`, и сервер сам пересчитывает бой из своего сида и запиненной версии конфигов.
- Награды выдаются не по скрипту боя, а по параметрам сюжетного события из конфигов. Команды `GrantReward` в скрипте — это анимация, а не источник истины.
- Истину про награды клиент берёт из ответа сервера: `step.appliedRewards` (что реально записано в профиль) и `step.profileRev` (ревизия профиля после записи). Скрипт боя для этого использовать не нужно.
- `activeSkillIds` в снапшоте юнита сервер не заполняет: скиллы приходят из конфигов персонажа, мобов, саммонов и экипировки. Клиентский выбор скиллов сервер бы всё равно не принял.
- Повторную выдачу закрывает `requestId` с TTL, конкурентную запись — `rev` профиля, историю — append-only ledger.

Поэтому ядро в клиенте — это проигрыватель и калькулятор для UI. Отдельный интерфейс с реализацией «через API» писать не нужно: состояние игрока и наград клиент получает обычными HTTP-запросами, а `IBattleCore` отвечает только за показ боя и характеристик.

## Границы

- Реализации внутри ядра остались `internal`: клиент работает через `IBattleCore`, `IConfigDistributor` и модели протокола. Расширять бой нужно на сервере, а не в клиенте — иначе расчёты разойдутся.
- Граф сервисов боя собирается одним конструктором (`BattleComposition`), и сервер, и клиент получают одинаковый порядок фаз хода. Своего контейнера внедрения зависимостей ядру не нужно.
- Бой синхронный: ни `async`, ни потоков внутри нет, `Replay` можно звать из корутины или из фонового потока.
