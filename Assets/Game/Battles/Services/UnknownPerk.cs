using Microsoft.Extensions.Logging;
using Server.Perks;

namespace Server.Battles
{
    internal sealed class UnknownPerk : BasePerk
    {
        private readonly ILogger _logger;

        internal UnknownPerk(IPerkMapper mapper, ILogger logger)
            : base(mapper)
        {
            _logger = logger;
        }

        public override bool CanTrigger(int currentTurn)
        {
            return false;
        }

        public override void Trigger(IPerkExecutionContext context)
        {
            _logger.LogError($"[Story][Battle] perk unsupported trigger id = {Id}, type = {PerkType}");
        }
    }
}
