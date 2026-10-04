using System.Security.Cryptography;
using Server.Services;

namespace Server.Runs
{
    internal sealed class RunRandomFactory
    {
        private const ulong Mix = 0x9E3779B97F4A7C15UL;
        private const int BattleSeedKeyBytes = 32;

        public long CreateSeed()
        {
            return BitConverter.ToInt64(RandomNumberGenerator.GetBytes(8));
        }

        public ISeededRandomService Create(long seed, int rollIndex)
        {
            unchecked
            {
                return new SeededRandomService((ulong)seed ^ ((ulong)(rollIndex + 1) * Mix));
            }
        }

        public string CreateBattleSeedKey()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(BattleSeedKeyBytes));
        }

        public ulong CreateBattleSeed(long seed, int stageIndex, string battleSeedKey)
        {
            if (string.IsNullOrEmpty(battleSeedKey))
                return CreateBattleSeed(seed, stageIndex);

            var message = new byte[12];

            BitConverter.TryWriteBytes(message.AsSpan(0, 8), seed);
            BitConverter.TryWriteBytes(message.AsSpan(8, 4), stageIndex);

            return BitConverter.ToUInt64(HMACSHA256.HashData(Convert.FromBase64String(battleSeedKey), message), 0);
        }

        public ulong CreateBattleSeed(long seed, int stageIndex)
        {
            unchecked
            {
                return ((ulong)seed * Mix) ^ (ulong)(stageIndex + 1);
            }
        }

        public int PickWeighted(ISeededRandomService random, int[] options, int[] weights)
        {
            if (options.Length == 0)
                return 0;

            var total = 0;

            for (int i = 0; i < options.Length; i++)
                total += ResolveWeight(weights, i);

            if (total <= 0)
                return options[random.Range(0, options.Length)];

            var roll = random.Range(0, total);
            var accumulated = 0;

            for (int i = 0; i < options.Length; i++)
            {
                accumulated += ResolveWeight(weights, i);

                if (roll < accumulated)
                    return options[i];
            }

            return options[options.Length - 1];
        }

        public List<int> PickDistinct(ISeededRandomService random, int[] options, int[] weights, int count)
        {
            var available = new List<int>(options);
            var availableWeights = new List<int>(options.Length);

            for (int i = 0; i < options.Length; i++)
                availableWeights.Add(ResolveWeight(weights, i));

            var result = new List<int>(count);

            while (result.Count < count && 0 < available.Count)
            {
                var picked = PickWeighted(random, available.ToArray(), availableWeights.ToArray());
                var index = available.IndexOf(picked);

                result.Add(picked);
                available.RemoveAt(index);
                availableWeights.RemoveAt(index);
            }

            return result;
        }

        private int ResolveWeight(int[] weights, int index)
        {
            if (weights.Length <= index)
                return 1;

            return weights[index] < 0 ? 0 : weights[index];
        }
    }
}
