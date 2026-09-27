namespace Server.Battles
{
    internal sealed class BattleDamageMath : IBattleDamageMath
    {
        public float RoundDamage(float damage)
        {
            if (damage <= 0f)
                return 0f;

            return MathF.Round(damage, MidpointRounding.AwayFromZero);
        }
    }
}
