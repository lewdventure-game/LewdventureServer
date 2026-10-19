using System.Globalization;
using Server.Battles;
using Server.Logging;
using Server.Runs;
using Server.Services;
using Server.Stories;

namespace Server.Api.Endpoints
{
    internal sealed class BalanceCatalogFactory
    {
        private readonly CheatCatalogFactory _cheatCatalogFactory;

        public BalanceCatalogFactory(CheatCatalogFactory cheatCatalogFactory)
        {
            _cheatCatalogFactory = cheatCatalogFactory;
        }

        public BalanceCatalogResponse Create(string configVersion, IConfigDistributor configDistributor)
        {
            var parameterParser = new BattleComposition(configDistributor, new SilentCoreLog()).BattleParameterParser;
            var response = new BalanceCatalogResponse { Items = _cheatCatalogFactory.Create(configVersion, configDistributor) };
            var levels = configDistributor.StoryLevels.Collection;

            for (int i = 0; i < levels.Count; i++)
                response.StoryLevels.Add(CreateLevel(levels[i], configDistributor, parameterParser));

            var enemies = configDistributor.Enemies.Collection;

            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];

                response.Enemies.Add(new BalanceEnemyResponse
                {
                    Id = enemy.Id,
                    SkinName = enemy.SkinName,
                    IsMelee = enemy.IsMelee,
                    Health = enemy.Health,
                    Damage = enemy.Damage,
                    SkillIds = new List<int>(enemy.SkillIds),
                });
            }

            return response;
        }

        private BalanceStoryLevelResponse CreateLevel(IStoryLevelMapper level, IConfigDistributor configDistributor, IBattleParameterParser parameterParser)
        {
            var response = new BalanceStoryLevelResponse
            {
                Id = level.Id,
                MaxBattleTurns = level.MaxBattleTurns,
                EnemiesHealthMultiplier = level.EnemiesHealthMultiplier,
                EnemiesAttackMultiplier = level.EnemiesAttackMultiplier,
            };

            for (int i = 0; i < level.StageIds.Length; i++)
            {
                if (configDistributor.StoryStages.TryGet(level.StageIds[i], out var stage) == false)
                    continue;

                var stageResponse = new BalanceStageResponse { Id = stage.Id, IsBoss = stage.IsBossLevel, EnemyStatsMultiplier = stage.EnemyStatsMultiplier };

                for (int eventIndex = 0; eventIndex < stage.EventIds.Length; eventIndex++)
                {
                    if (configDistributor.StoryEvents.TryGet(stage.EventIds[eventIndex], out var storyEvent) == false)
                        continue;

                    stageResponse.Events.Add(new BalanceEventResponse
                    {
                        Id = storyEvent.Id,
                        Type = storyEvent.EventType.ToString(),
                        EnemyIds = storyEvent.EventType == StoryEventType.Fight ? ReadEnemies(storyEvent.EventParameters, parameterParser) : new List<int>(),
                    });
                }

                response.Stages.Add(stageResponse);
            }

            return response;
        }

        private List<int> ReadEnemies(string eventParameters, IBattleParameterParser parameterParser)
        {
            var parameters = new Dictionary<string, string>();
            var result = new List<int>();

            parameterParser.ParseKeyValues(eventParameters, parameters);

            if (parameters.TryGetValue(RunEventKeys.Enemies, out var raw) == false)
                return result;

            var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    result.Add(value);
            }

            return result;
        }
    }
}
