namespace Server.Bonuses
{
    internal sealed class BonusWorkMode
    {
        private readonly List<BonusWorkModePart> _parts;

        public BonusWorkMode(List<BonusWorkModePart> parts)
        {
            _parts = parts;
        }

        public IReadOnlyList<BonusWorkModePart> Parts => _parts;

        public bool Contains(BonusWorkModeKind kind)
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                if (_parts[i].Kind == kind)
                    return true;
            }

            return false;
        }

        public bool TryGetPart(BonusWorkModeKind kind, out BonusWorkModePart part)
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                if (_parts[i].Kind != kind)
                    continue;

                part = _parts[i];

                return true;
            }

            part = new BonusWorkModePart(BonusWorkModeKind.Unknown, string.Empty, 0, 0);

            return false;
        }

        public string Format()
        {
            if (_parts.Count == 0)
                return BonusWorkModeKind.Unknown.ToString();

            var result = _parts[0].Kind.ToString();

            for (int i = 1; i < _parts.Count; i++)
                result = result + ";" + _parts[i].Kind;

            return result;
        }
    }
}
