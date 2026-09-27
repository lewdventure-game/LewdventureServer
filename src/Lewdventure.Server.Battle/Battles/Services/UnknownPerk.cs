using Server.Perks;

namespace Server.Battles
{
    internal sealed class UnknownPerk : BasePerk
    {
        private readonly ICoreLog _coreLog;

        internal UnknownPerk(IPerkMapper mapper, ICoreLog coreLog)
            : base(mapper)
        {
            _coreLog = coreLog;
        }

        public override bool CanTrigger(int currentTurn)
        {
            return false;
        }

        public override void Trigger(IPerkExecutionContext context)
        {
            _coreLog.Error($"[Story][Battle]: Perk unsupported trigger, id = {Id}, type = {PerkType}");
        }
    }
}
