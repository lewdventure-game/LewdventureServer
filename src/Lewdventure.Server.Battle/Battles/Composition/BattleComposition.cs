using Server.Bonuses;
using Server.Configs;
using Server.Services;

namespace Server.Battles
{
    internal sealed class BattleComposition
    {
        private readonly IBattleParameterParser _battleParameterParser;
        private readonly IBattleRewardParser _battleRewardParser;
        private readonly IBattleScriptDigest _battleScriptDigest;
        private readonly IBattleSimulationValidator _battleSimulationValidator;
        private readonly IBattleSimulatorService _battleSimulatorService;
        private readonly IBonusWorkModeParser _bonusWorkModeParser;
        private readonly IPerkFactory _perkFactory;
        private readonly ISkillFactory _skillFactory;
        private readonly IUnitStateBuilder _unitStateBuilder;

        public BattleComposition(IConfigDistributor configDistributor, ICoreLog coreLog)
            : this(configDistributor, coreLog, new SeededRandomFactory())
        { }

        public BattleComposition(IConfigDistributor configDistributor, ICoreLog coreLog, ISeededRandomFactory seededRandomFactory)
        {
            var parserUtils = new ParserUtils(coreLog);
            var constantsReader = new BattleConstantsReader(configDistributor, coreLog);
            var teamQuery = new BattleTeamQuery();
            var statusClassifier = new StatusClassifier();
            var commandFactory = new BattleCommandFactory();
            var damageMath = new BattleDamageMath();
            var scriptBuilder = new BattleScriptBuilder(coreLog);
            var bucketApplicator = new CharacteristicBucketApplicator(coreLog);
            var characteristicCalculator = new CharacteristicCalculator(coreLog);
            var statusParametersParser = new StatusParametersParser(coreLog, statusClassifier);
            var bucketsFactory = new UnitBucketsFactory(constantsReader, coreLog, configDistributor);

            _battleParameterParser = new BattleParameterParser();
            _battleRewardParser = new BattleRewardParser(coreLog);
            _battleScriptDigest = new BattleScriptDigest();
            _bonusWorkModeParser = new BonusWorkModeParser(coreLog);
            _skillFactory = new SkillFactory(coreLog, configDistributor, parserUtils, CreateSkillCreators(parserUtils));

            var bonusService = new BattleBonusService(coreLog, commandFactory, _bonusWorkModeParser, bucketApplicator, characteristicCalculator, configDistributor);
            var rewardService = new BattleRewardService(coreLog, bonusService, commandFactory, _battleRewardParser, configDistributor, statusParametersParser);

            _perkFactory = new PerkFactory(coreLog, CreatePerkCreators(coreLog, parserUtils, rewardService));

            var perkSimulator = new BattlePerkSimulator(coreLog, bonusService, commandFactory, damageMath, rewardService, scriptBuilder, configDistributor);
            var skillSimulator = new BattleSkillSimulator(constantsReader, teamQuery, coreLog, bonusService, commandFactory, damageMath, perkSimulator, rewardService, scriptBuilder, configDistributor);
            var statusSimulator = new BattleStatusSimulator(coreLog, bonusService, commandFactory, damageMath, perkSimulator, scriptBuilder, configDistributor, statusClassifier);
            var attackService = new BattleAttackService(constantsReader, teamQuery, coreLog, commandFactory, damageMath, perkSimulator, scriptBuilder, skillSimulator);
            var summonSimulator = new BattleSummonSimulator(constantsReader, teamQuery, coreLog, commandFactory, damageMath, perkSimulator, scriptBuilder, skillSimulator, configDistributor);
            var bonusGranter = new UnitBonusGranter(coreLog, bonusService, _bonusWorkModeParser, configDistributor, bucketsFactory);
            var loadoutBinder = new UnitLoadoutBinder(coreLog, bonusService, configDistributor, _perkFactory, _skillFactory, statusParametersParser);

            _unitStateBuilder = new UnitStateBuilder(coreLog, bonusService, characteristicCalculator, configDistributor, bonusGranter, bucketsFactory, loadoutBinder);
            _battleSimulationValidator = new BattleSimulationValidator(coreLog, configDistributor, _skillFactory);

            var turnPhases = new List<IBattleTurnPhase>
            {
                new StatusTurnPhase(statusSimulator),
                new PerkTurnPhase(perkSimulator),
                new SummonTurnPhase(summonSimulator),
                new MainUnitTurnPhase(attackService),
            };

            _battleSimulatorService = new BattleSimulatorService(
                teamQuery,
                coreLog,
                attackService,
                bonusService,
                commandFactory,
                perkSimulator,
                scriptBuilder,
                statusSimulator,
                turnPhases,
                summonSimulator,
                configDistributor,
                seededRandomFactory,
                _unitStateBuilder);
        }

        public IBattleParameterParser BattleParameterParser => _battleParameterParser;

        public IBattleRewardParser BattleRewardParser => _battleRewardParser;

        public IBattleScriptDigest BattleScriptDigest => _battleScriptDigest;

        public IBattleSimulationValidator BattleSimulationValidator => _battleSimulationValidator;

        public IBattleSimulatorService BattleSimulatorService => _battleSimulatorService;

        public IBonusWorkModeParser BonusWorkModeParser => _bonusWorkModeParser;

        public IPerkFactory PerkFactory => _perkFactory;

        public ISkillFactory SkillFactory => _skillFactory;

        public IUnitStateBuilder UnitStateBuilder => _unitStateBuilder;

        private IReadOnlyList<ISkillCreator> CreateSkillCreators(ParserUtils parserUtils)
        {
            return new List<ISkillCreator>
            {
                new FireballSkillCreator(parserUtils),
                new EnergySkillCreator(parserUtils),
                new SummonVenomStrikeSkillCreator(parserUtils),
                new SummonWarHowlSkillCreator(parserUtils),
                new SummonSoulSiphonSkillCreator(parserUtils),
            };
        }

        private IReadOnlyList<IPerkCreator> CreatePerkCreators(ICoreLog coreLog, ParserUtils parserUtils, IBattleRewardService battleRewardService)
        {
            return new List<IPerkCreator>
            {
                new RewardPerkCreator(battleRewardService, coreLog, CreateReader(coreLog, parserUtils)),
                new ActionRewardPerkCreator(battleRewardService, coreLog, CreateReader(coreLog, parserUtils)),
                new ResurrectionPerkCreator(coreLog, CreateReader(coreLog, parserUtils)),
                new FireAttackPerkCreator(coreLog, CreateReader(coreLog, parserUtils)),
                new EarthAttackPerkCreator(coreLog, CreateReader(coreLog, parserUtils)),
                new AirAttackPerkCreator(coreLog, CreateReader(coreLog, parserUtils)),
                new WaterAttackPerkCreator(coreLog, CreateReader(coreLog, parserUtils)),
            };
        }

        private PerkParameterReader CreateReader(ICoreLog coreLog, ParserUtils parserUtils)
        {
            return new PerkParameterReader(_battleParameterParser, _battleRewardParser, coreLog, parserUtils);
        }
    }
}
