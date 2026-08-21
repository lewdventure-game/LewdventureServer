namespace Server.Battles
{
    [Flags]
    internal enum UnitFlags : byte
    {
        None = 0,
        Summon = 1 << 0,
        Range = 1 << 1,
        Melee = 1 << 2,
        All = byte.MaxValue,
    }
}
