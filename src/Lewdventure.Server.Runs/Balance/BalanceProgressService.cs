using Server.Infrastructure.Mongo.Players;
using Server.Services;

namespace Server.Balance
{
    internal sealed class BalanceProgressService
    {
        public const int SampleLimit = 500;

        private const int MaxQaAccounts = 1000;

        private readonly BalanceService _balanceService;
        private readonly BalanceSetupReader _balanceSetupReader;
        private readonly BalanceStatistics _balanceStatistics;
        private readonly PlayerProfileRepository _playerProfileRepository;
        private readonly UserRepository _userRepository;

        public BalanceProgressService(
            BalanceService balanceService,
            BalanceSetupReader balanceSetupReader,
            BalanceStatistics balanceStatistics,
            PlayerProfileRepository playerProfileRepository,
            UserRepository userRepository)
        {
            _balanceService = balanceService;
            _balanceSetupReader = balanceSetupReader;
            _balanceStatistics = balanceStatistics;
            _playerProfileRepository = playerProfileRepository;
            _userRepository = userRepository;
        }

        public async Task<BalanceProgressResult?> FindTypicalAsync(int storyLevelId, IConfigDistributor configDistributor, CancellationToken cancellationToken)
        {
            var result = new BalanceProgressResult { StoryLevelId = storyLevelId, NextStoryLevelId = FindNextLevelId(storyLevelId, configDistributor) };
            var qaUsers = await _userRepository.ListQaAsync(MaxQaAccounts, cancellationToken);
            var qaIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < qaUsers.Count; i++)
                qaIds.Add(qaUsers[i].Id);

            var profiles = await _playerProfileRepository.ListByStoryProgressAsync(storyLevelId, result.NextStoryLevelId, SampleLimit, cancellationToken);
            var setups = new List<BalanceSetup>(profiles.Count);
            var userIds = new List<string>(profiles.Count);

            result.Sampled = profiles.Count;

            for (int i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];

                if (qaIds.Contains(profile.Id) || profile.Loadout.CharacterId <= 0)
                    continue;

                setups.Add(_balanceSetupReader.Read(profile));
                userIds.Add(profile.Id);
            }

            var powers = _balanceService.MeasurePower(setups, storyLevelId, configDistributor);
            var validPowers = new List<float>(powers.Count);
            var promoteLevels = new List<float>(powers.Count);
            var summonLevels = new List<float>(powers.Count);
            var equipmentLevels = new List<float>(powers.Count);
            var accountBonuses = new List<float>(powers.Count);

            for (int i = 0; i < powers.Count; i++)
            {
                if (powers[i] < 0f)
                    continue;

                var setup = setups[i];

                validPowers.Add(powers[i]);
                promoteLevels.Add(setup.PromoteLevel);
                summonLevels.Add(AverageSummonLevel(setup));
                equipmentLevels.Add(AverageEquipmentLevel(setup));
                accountBonuses.Add(CountBonuses(setup));
            }

            result.Players = validPowers.Count;

            if (validPowers.Count == 0)
                return result;

            result.Power = _balanceStatistics.Distribute(new List<float>(validPowers));
            result.PromoteLevel = _balanceStatistics.Distribute(promoteLevels);
            result.SummonLevel = _balanceStatistics.Distribute(summonLevels);
            result.EquipmentLevel = _balanceStatistics.Distribute(equipmentLevels);
            result.AccountBonuses = _balanceStatistics.Distribute(accountBonuses);

            var picked = FindClosest(powers, result.Power.Median);

            result.UserId = userIds[picked];
            result.PickedPower = powers[picked];
            result.Setup = setups[picked];

            return result;
        }

        private int FindNextLevelId(int storyLevelId, IConfigDistributor configDistributor)
        {
            var levels = configDistributor.StoryLevels.Collection;

            for (int i = 0; i < levels.Count - 1; i++)
            {
                if (levels[i].Id == storyLevelId)
                    return levels[i + 1].Id;
            }

            return 0;
        }

        private int FindClosest(List<float> powers, float target)
        {
            var best = -1;
            var bestDistance = float.MaxValue;

            for (int i = 0; i < powers.Count; i++)
            {
                if (powers[i] < 0f)
                    continue;

                var distance = MathF.Abs(powers[i] - target);

                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private float AverageSummonLevel(BalanceSetup setup)
        {
            if (setup.Summons.Count == 0)
                return 0f;

            var sum = 0f;

            for (int i = 0; i < setup.Summons.Count; i++)
                sum += setup.Summons[i].Level;

            return sum / setup.Summons.Count;
        }

        private float AverageEquipmentLevel(BalanceSetup setup)
        {
            if (setup.Equipment.Count == 0)
                return 0f;

            var sum = 0f;

            for (int i = 0; i < setup.Equipment.Count; i++)
                sum += setup.Equipment[i].Level;

            return sum / setup.Equipment.Count;
        }

        private float CountBonuses(BalanceSetup setup)
        {
            var count = 0;

            for (int i = 0; i < setup.AccountBonuses.Count; i++)
                count += setup.AccountBonuses[i].Count;

            return count;
        }
    }
}
