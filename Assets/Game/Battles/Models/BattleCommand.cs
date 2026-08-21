using Newtonsoft.Json.Linq;

namespace Server.Battles
{
    internal sealed class BattleCommand
    {
        public CommandType CommandType { get; set; }

        public JObject Parameters { get; set; } = new();
    }
}
