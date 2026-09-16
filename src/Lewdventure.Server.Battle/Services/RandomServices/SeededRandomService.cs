using System.Numerics;

namespace Server.Services
{
    internal sealed class SeededRandomService : ISeededRandomService
    {
        private ulong _seed;

        private ulong _state0;
        private ulong _state1;
        private ulong _state2;
        private ulong _state3;

        public ulong Seed => _seed;

        public SeededRandomService()
            : this((uint)Environment.TickCount)
        { }

        public SeededRandomService(ulong seed)
        {
            SetSeed(seed);
        }

        public void SetSeed(ulong seed)
        {
            _seed = seed;

            var splitMix64 = new SplitMix64(seed);

            _state0 = splitMix64.Next();
            _state1 = splitMix64.Next();
            _state2 = splitMix64.Next();
            _state3 = splitMix64.Next();
        }

        public ulong NextULong()
        {
            var result = BitOperations.RotateLeft(_state1 * 5, 7) * 9;

            var temporary = _state1 << 17;

            _state2 ^= _state0;
            _state3 ^= _state1;
            _state1 ^= _state2;
            _state0 ^= _state3;

            _state2 ^= temporary;

            _state3 = BitOperations.RotateLeft(_state3, 45);

            return result;
        }

        public uint NextUInt()
        {
            var value = (uint)NextULong();

            return value;
        }

        public float GetRandomValue()
        {
            var value = (NextUInt() >> 8) * (1.0f / 16777216.0f);

            return value;
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxExclusive, minInclusive);

            var range = (uint)(maxExclusive - minInclusive);
            var limit = uint.MaxValue - uint.MaxValue % range;

            uint value;

            do
            {
                value = NextUInt();
            }
            while (value >= limit);

            return minInclusive + (int)(value % range);
        }

        public float Range(float minInclusive, float maxInclusive)
        {
            var value = minInclusive + GetRandomValue() * (maxInclusive - minInclusive);

            return value;
        }

        public T Choice<T>(T[] options)
        {
            if (options == null || options.Length == 0)
                throw new ArgumentException("Options must not be null or empty", nameof(options));

            var index = Range(0, options.Length);

            return options[index];
        }

        private struct SplitMix64
        {
            private ulong _state;

            public SplitMix64(ulong seed)
            {
                _state = seed;
            }

            public ulong Next()
            {
                _state += 0x9E3779B97F4A7C15UL;

                var value = _state;

                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                value ^= value >> 31;

                return value;
            }
        }
    }
}
