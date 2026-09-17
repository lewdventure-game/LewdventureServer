using System;
using System.Collections.Generic;
using System.Globalization;
using Server.Configs;

namespace Server.Battles
{
    internal abstract class BaseSkill : ISkill
    {
        private readonly int _id;
        private readonly string _skillKey;
        private readonly SkillType _skillType;
        private readonly float _castDurationSeconds;

        public int Id => _id;

        public string SkillKey => _skillKey;

        public SkillType SkillType => _skillType;

        public float CastDurationSeconds => _castDurationSeconds;

        protected BaseSkill(ISkillMapper mapper)
        {
            _id = mapper.Id;
            _skillKey = mapper.SkillKey;
            _skillType = mapper.SkillType;
            _castDurationSeconds = ResolveCastDuration(mapper);
        }

        public abstract void Execute(ISkillExecutionContext context);

        protected void BeginCast(ISkillExecutionContext context, List<BattleCommand> commands)
        {
            commands.Add(context.BattleCommandFactory.CastSkill(context.Actor.Id, context.Actor.SlotIndex, context.Target.Id, context.Target.SlotIndex, SkillKey));
            commands.Add(context.BattleCommandFactory.PlayAnimation(context.Actor.Id, context.Actor.SlotIndex, "cast"));
            commands.Add(context.BattleCommandFactory.Wait(_castDurationSeconds));

            context.Logger.LogDebug($"[Story][Battle]: Skill cast duration, skillKey = {SkillKey}, unitId = {context.Actor.Id}, duration = {_castDurationSeconds}");
        }

        protected void EndCast(ISkillExecutionContext context, List<BattleCommand> commands, IUnitState target)
        {
            if (context.Phase == BattlePhaseType.UnitSkill)
            {
                context.Logger.LogDebug($"[Story][Battle]: Skill EndCast skip Wait unit phase, skillKey = {SkillKey}, unitId = {context.Actor.Id}");
                context.AddStep(commands, target);

                return;
            }

            commands.Add(context.BattleCommandFactory.Wait(context.Cooldown));
            context.AddStep(commands, target);
        }

        private float ResolveCastDuration(ISkillMapper mapper)
        {
            if (mapper.SkillType == SkillType.Unknown)
                return 0f;

            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            ParserUtils.ParseToDictionary(mapper.Parameters, dictionary);

            if (dictionary.TryGetValue(Keys.DurationKey, out var rawDuration) == false || string.IsNullOrWhiteSpace(rawDuration))
                throw new InvalidOperationException($"[Error][Story][Battle]: Skill duration missing, skillKey = {mapper.SkillKey}");

            var normalizedDuration = rawDuration.Trim().Replace(',', '.');

            if (float.TryParse(normalizedDuration, NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) == false)
                throw new InvalidOperationException($"[Error][Story][Battle]: Skill duration invalid, skillKey = {mapper.SkillKey}, duration = {rawDuration}");

            if (duration <= 0f)
                throw new InvalidOperationException($"[Error][Story][Battle]: Skill duration missing or zero, skillKey = {mapper.SkillKey}, duration = {rawDuration}");

            return duration;
        }

        private sealed class Keys
        {
            internal const string DurationKey = "duration";
        }
    }
}
