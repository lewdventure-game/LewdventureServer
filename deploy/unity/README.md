# Ядро боя для Unity: как пользоваться

Куда что положить:

| Файл | Куда |
| --- | --- |
| три `.dll` и `link.xml` | `Assets/Plugins/BattleCore/` |
| `UnityCoreLog.cs` | `Assets/Scripts/Core/Common/UnityCoreLog.cs` |

`UnityCoreLog.cs` кладётся в сборку `Core`, потому что ядро создаётся там же, где доступен источник конфигов (`IConfigSource` внутренний для `Core`). В `Assets/Plugins` его класть нельзя: файлы оттуда попадают в отдельную сборку, которую asmdef'ы клиента не видят. Внешняя зависимость одна — пакет `com.unity.nuget.newtonsoft-json` (уже в проекте), Api Compatibility Level — `.NET Standard 2.1`.

| DLL | Что даёт |
| --- | --- |
| `Lewdventure.Server.Contracts` | модели протокола: `BattleReplayData`, `UnitSnapshot`, `BattleStep`, `BattleCommand`, `CommandType`, `OutcomeType` и `BattleJsonSettingsFactory` |
| `Lewdventure.Server.GameConfig` | чтение конфигов: `IConfigDistributor` с мапперами персонажей, мобов, саммонов, экипировки, перков, статусов, скиллов, сюжета; enum'ы `PerkType`, `StatusType`, `BonusType`, `RarityType`; `ICoreLog`, `SilentCoreLog` |
| `Lewdventure.Server.Battle` | симулятор и точка входа `SharedCoreFactory` / `ISharedCore` / `IBattleCore` |

## Создать ядро один раз при старте

```csharp
var result = new SharedCoreFactory().CreateFromBundle(bundleJson, new UnityCoreLog());

if (result.Succeeded == false)
{
    Debug.LogError(string.Join("; ", result.Errors));

    return;
}

var sharedCore = result.SharedCore;
var battleCore = sharedCore.Battle;
```

`bundleJson` — содержимое `Assets/Configs/Core/Json/ConfigBundle.json` как есть; в клиенте его отдаёт `LocalConfigSource.Load()`. `UnityCoreLog` идёт в поставке, писать ничего не нужно; в релизной сборке можно передать `new SilentCoreLog()` из DLL, чтобы бой не сыпал отладкой в консоль. `result.Warnings` стоит логировать: там те же предупреждения, что видит геймдизайнер при публикации конфигов.

Проверить, что данные те же, что у сервера: `sharedCore.ConfigVersion` сравнить с `configVersion` из ответа сервера.

## Проиграть бой

```csharp
var settings = new BattleJsonSettingsFactory().Create();
var input = JsonConvert.DeserializeObject<BattleReplayData>(step.battleInput, settings);
var script = battleCore.Replay(input);

if (battleCore.ComputeDigest(script) != step.battleDigest)
    Debug.LogError("parity mismatch");

foreach (var battleStep in script.Steps)
    Play(battleStep);
```

Сервер присылает либо готовый скрипт (`step.battle`), либо только вход с сидом (`step.battleInput`, если в запрос добавить `battleDelivery: "seed"`). Во втором случае бой считает клиент тем же кодом, что сервер, и сверяет дайджест.

Настройки сериализации брать только из `BattleJsonSettingsFactory` — свои писать нельзя, разойдутся с сервером.

## Характеристики и конфиги для UI

```csharp
var characteristics = battleCore.BuildCharacteristics(unitSnapshot, BattleSide.Attacking, false, storyLevelId, stageId);

if (sharedCore.Configs.Characters.TryGet(characterId, out var characterMapper))
    portrait.Load(characterMapper.ArtName);
```

`BuildCharacteristics` — те же 20 характеристик, что считает бой и что отдаёт `GET /api/player/characteristics`. `Configs` — весь read-model конфигов: арты, редкости, перки, статусы, скиллы, сюжетные уровни, константы.

## Обязательные мелочи

- `link.xml` рядом с DLL: без него IL2CPP вырезает рефлексию Newtonsoft и разбор `BonusType`.
- Api Compatibility Level — `.NET Standard 2.1`.
- Итоги боя брать из ответа сервера (`currentHealth`, `appliedRewards`, `profileRev`), а не из скрипта: скрипт нужен только чтобы показать бой.
- Enum'ы протокола теперь есть в DLL — клиентские копии можно удалить, чтобы числа не разъезжались.
- Механики боя не форкать: реализации внутри ядра `internal`, расширение делается на сервере, иначе расчёты разойдутся.

Подробности, примеры и порядок прогона паритета — `docs/server/battle-core-in-unity.md` в серверном репозитории.
