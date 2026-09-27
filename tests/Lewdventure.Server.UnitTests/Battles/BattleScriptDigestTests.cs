using Server.Battles;

namespace Tests.Unit.Battles
{
    [TestFixture]
    public sealed class BattleScriptDigestTests
    {
        private readonly BattleScriptDigest _battleScriptDigest = new();

        [Test]
        public void Compute_SameScript_GivesSameDigest()
        {
            var digest = _battleScriptDigest.Compute(CreateScript(OutcomeType.TeamAWin, 7));

            Assert.That(digest, Does.StartWith("sha256:"));
            Assert.That(_battleScriptDigest.Compute(CreateScript(OutcomeType.TeamAWin, 7)), Is.EqualTo(digest));
        }

        [Test]
        public void Compute_ChangedOutcome_GivesOtherDigest()
        {
            var digest = _battleScriptDigest.Compute(CreateScript(OutcomeType.TeamAWin, 7));

            Assert.That(_battleScriptDigest.Compute(CreateScript(OutcomeType.TeamBWin, 7)), Is.Not.EqualTo(digest));
        }

        [Test]
        public void Compute_ChangedCommandValue_GivesOtherDigest()
        {
            var digest = _battleScriptDigest.Compute(CreateScript(OutcomeType.TeamAWin, 7));

            Assert.That(_battleScriptDigest.Compute(CreateScript(OutcomeType.TeamAWin, 8)), Is.Not.EqualTo(digest));
        }

        private IBattleScriptResponse CreateScript(OutcomeType outcomeType, int damage)
        {
            var command = new BattleCommand
            {
                CommandType = CommandType.SetHp,
            };

            command.Parameters["unitId"] = 1;
            command.Parameters["value"] = damage;

            var step = new BattleStep
            {
                Index = 0,
                Turn = 1,
                Phase = BattlePhaseType.NormalAttack,
                ActorId = 1,
                TargetId = 10101,
            };

            step.Commands.Add(command);

            var script = new BattleScriptResponse
            {
                Seed = 42,
                OutcomeType = outcomeType,
            };

            script.Steps.Add(step);

            return script;
        }
    }
}
