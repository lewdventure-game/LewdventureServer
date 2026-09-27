using System.Globalization;

namespace Server.Battles
{
    public sealed class BattleRollTrace : IBattleRollRecorder
    {
        private readonly List<string> _lines = new();

        public IReadOnlyList<string> Lines => _lines;

        public void Clear()
        {
            _lines.Clear();
        }

        public void Record(int rollIndex, string rollName, float value)
        {
            _lines.Add($"{rollIndex.ToString(CultureInfo.InvariantCulture)} {rollName} {BitConverter.SingleToInt32Bits(value).ToString("x8", CultureInfo.InvariantCulture)}");
        }

        public string ToText()
        {
            return string.Join("\n", _lines) + "\n";
        }
    }
}
