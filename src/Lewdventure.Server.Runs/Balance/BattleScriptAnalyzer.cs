using System.Globalization;
using Newtonsoft.Json.Linq;
using Server.Battles;

namespace Server.Balance
{
    internal sealed class BattleScriptAnalyzer
    {
        public const string AttackSource = "attack";
        public const string ComboOneSource = "combo1";
        public const string ComboTwoSource = "combo2";
        public const string CounterSource = "counter";
        public const string SkillSource = "skill";
        public const string SummonSource = "summon";
        public const string StatusSource = "status";
        public const string PerkSource = "perk";
        public const string OtherSource = "other";

        public BalanceBattleResult Analyze(IBattleScriptResponse script, IBattleSimulationData data, float heroHealthStart, bool includeLog)
        {
            var hero = data.TeamA.MainUnits[0];
            var enemyIds = new HashSet<int>();
            var summonIds = new HashSet<int>();

            for (int i = 0; i < data.TeamB.MainUnits.Count; i++)
                enemyIds.Add(data.TeamB.MainUnits[i].Id);

            for (int i = 0; i < data.TeamA.Summons.Count; i++)
                summonIds.Add(data.TeamA.Summons[i].Id);

            var result = new BalanceBattleResult
            {
                Seed = script.Seed,
                Outcome = script.OutcomeType.ToString(),
                IsWin = script.OutcomeType == OutcomeType.TeamAWin,
                Steps = script.Steps.Count,
                HeroHealthStart = heroHealthStart,
                HeroHealthEnd = heroHealthStart,
            };

            for (int stepIndex = 0; stepIndex < script.Steps.Count; stepIndex++)
            {
                var step = script.Steps[stepIndex];

                if (result.Turns < step.Turn + 1)
                    result.Turns = step.Turn + 1;

                var source = ResolveSource(step.Phase);
                var commands = step.Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                    Apply(result, step, source, commands[commandIndex], hero.Id, enemyIds, summonIds, includeLog);
            }

            return result;
        }

        private void Apply(BalanceBattleResult result, BattleStep step, string source, BattleCommand command, int heroId, HashSet<int> enemyIds, HashSet<int> summonIds, bool includeLog)
        {
            var parameters = command.Parameters;

            switch (command.CommandType)
            {
                case CommandType.ShowDamage:
                    ApplyDamage(result, step, source, parameters, heroId, enemyIds, summonIds, includeLog);
                    break;
                case CommandType.ShowMiss:
                    if (IsEnemy(ReadInt(parameters, "targetId"), heroId, enemyIds))
                        result.Misses += 1;

                    AddLog(result, step, includeLog, Name(ReadInt(parameters, "actorId"), heroId, enemyIds, summonIds) + " → " + Name(ReadInt(parameters, "targetId"), heroId, enemyIds, summonIds) + ": промах");
                    break;
                case CommandType.ShowHeal:
                    var heal = ReadFloat(parameters, "heal");

                    if (ReadInt(parameters, "targetId") == heroId)
                        result.Healing += heal;

                    AddLog(result, step, includeLog, Name(ReadInt(parameters, "targetId"), heroId, enemyIds, summonIds) + ": лечение +" + Format(heal));
                    break;
                case CommandType.SetHp:
                    if (ReadInt(parameters, "unitId") == heroId && ReadInt(parameters, "slotIndex") == 0)
                        result.HeroHealthEnd = ReadFloat(parameters, "hp");
                    break;
                case CommandType.KillUnit:
                    AddLog(result, step, includeLog, Name(ReadInt(parameters, "unitId"), heroId, enemyIds, summonIds) + " погибает");
                    break;
                case CommandType.CastSkill:
                    AddLog(result, step, includeLog, Name(ReadInt(parameters, "actorId"), heroId, enemyIds, summonIds) + ": скилл " + ReadString(parameters, "skillId"));
                    break;
                case CommandType.TriggerPerk:
                    AddLog(result, step, includeLog, "перк " + ReadInt(parameters, "perkId").ToString(CultureInfo.InvariantCulture));
                    break;
                case CommandType.ApplyStatus:
                    AddLog(result, step, includeLog, Name(ReadInt(parameters, "targetId"), heroId, enemyIds, summonIds) + ": статус " + ReadInt(parameters, "statusId").ToString(CultureInfo.InvariantCulture));
                    break;
            }
        }

        private void ApplyDamage(BalanceBattleResult result, BattleStep step, string source, JObject parameters, int heroId, HashSet<int> enemyIds, HashSet<int> summonIds, bool includeLog)
        {
            var damage = ReadFloat(parameters, "damage");
            var targetId = ReadInt(parameters, "targetId");
            var isCritical = ReadBool(parameters, "isCritical");

            if (IsEnemy(targetId, heroId, enemyIds))
            {
                result.DamageDealt += damage;
                result.DamageBySource[source] = (result.DamageBySource.TryGetValue(source, out var total) ? total : 0f) + damage;

                if (isCritical)
                    result.CriticalHits += 1;
            }
            else if (targetId == heroId || summonIds.Contains(targetId))
            {
                result.DamageTaken += damage;
            }

            AddLog(result, step, includeLog, Name(ReadInt(parameters, "actorId"), heroId, enemyIds, summonIds) + " → " + Name(targetId, heroId, enemyIds, summonIds) + ": " + Format(damage) + (isCritical ? " (крит)" : string.Empty));
        }

        private void AddLog(BalanceBattleResult result, BattleStep step, bool includeLog, string text)
        {
            if (includeLog == false)
                return;

            result.Log.Add(new BalanceLogEntry { Turn = step.Turn + 1, Phase = step.Phase.ToString(), Text = text });
        }

        private bool IsEnemy(int unitId, int heroId, HashSet<int> enemyIds)
        {
            return unitId != heroId && enemyIds.Contains(unitId);
        }

        private string Name(int unitId, int heroId, HashSet<int> enemyIds, HashSet<int> summonIds)
        {
            if (unitId == heroId)
                return "герой";

            if (enemyIds.Contains(unitId))
                return "враг " + unitId.ToString(CultureInfo.InvariantCulture);

            if (summonIds.Contains(unitId))
                return "саммон " + unitId.ToString(CultureInfo.InvariantCulture);

            return unitId.ToString(CultureInfo.InvariantCulture);
        }

        private string ResolveSource(BattlePhaseType phase)
        {
            switch (phase)
            {
                case BattlePhaseType.NormalAttack:
                    return AttackSource;
                case BattlePhaseType.Combo1Attack:
                    return ComboOneSource;
                case BattlePhaseType.Combo2Attack:
                    return ComboTwoSource;
                case BattlePhaseType.CounterAttack:
                    return CounterSource;
                case BattlePhaseType.UnitSkill:
                case BattlePhaseType.EnergySkill:
                    return SkillSource;
                case BattlePhaseType.SummonSkill:
                case BattlePhaseType.SummonAttack:
                    return SummonSource;
                case BattlePhaseType.StatusTrigger:
                    return StatusSource;
                case BattlePhaseType.PerkTrigger:
                    return PerkSource;
                default:
                    return OtherSource;
            }
        }

        private int ReadInt(JObject parameters, string name)
        {
            var token = parameters[name];

            return token == null || token.Type == JTokenType.Null ? -1 : token.Value<int>();
        }

        private float ReadFloat(JObject parameters, string name)
        {
            var token = parameters[name];

            return token == null || token.Type == JTokenType.Null ? 0f : token.Value<float>();
        }

        private bool ReadBool(JObject parameters, string name)
        {
            var token = parameters[name];

            return token != null && token.Type == JTokenType.Boolean && token.Value<bool>();
        }

        private string ReadString(JObject parameters, string name)
        {
            var token = parameters[name];

            return token == null ? string.Empty : token.ToString();
        }

        private string Format(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
