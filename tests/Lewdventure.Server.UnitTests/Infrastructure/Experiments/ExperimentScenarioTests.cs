using Server.Infrastructure.Experiments;
using Server.Infrastructure.Mongo.Experiments;

namespace Tests.Unit.Infrastructure.Experiments
{
    [TestFixture]
    public sealed class ExperimentScenarioTests
    {
        private const double Tolerance = 0.01d;

        private readonly ExperimentTestData _data = new();

        [Test]
        public void ScenarioOne_NewPlayersSplitIntoControlTestAndMaster()
        {
            var registry = new ExperimentRegistry();

            registry.Replace(new List<ExperimentDocument>
            {
                _data.CreateRunning("balance", _data.CreateGroup("control", 15, true), _data.CreateGroup("test", 15, true)),
            });

            var newPlayers = _data.Distribute(registry, true, "US");
            var oldPlayers = _data.Distribute(registry, false, "US");

            Assert.That(newPlayers["balance/control"], Is.EqualTo(15).Within(Tolerance));
            Assert.That(newPlayers["balance/test"], Is.EqualTo(15).Within(Tolerance));
            Assert.That(newPlayers[ExperimentTestData.MasterKey], Is.EqualTo(70).Within(Tolerance));
            Assert.That(oldPlayers[ExperimentTestData.MasterKey], Is.EqualTo(100).Within(Tolerance));
            Assert.That(oldPlayers.Count, Is.EqualTo(1));
        }

        [Test]
        public void ScenarioTwo_ParallelExperimentsShareTraffic()
        {
            var registry = new ExperimentRegistry();
            var usOnly = _data.CreateRunning(
                "us",
                _data.CreateGroup("g1", 25, true, "US"),
                _data.CreateGroup("g2", 25, true, "US"),
                _data.CreateGroup("g3", 25, true, "US"));
            var world = _data.CreateRunning("world", _data.CreateGroup("a", 10, true), _data.CreateGroup("b", 10, true));

            registry.Replace(new List<ExperimentDocument> { usOnly, world });

            var american = _data.Distribute(registry, true, "US");
            var german = _data.Distribute(registry, true, "DE");

            Assert.That(american["us/g1"], Is.EqualTo(25).Within(Tolerance));
            Assert.That(american["us/g2"], Is.EqualTo(25).Within(Tolerance));
            Assert.That(american["us/g3"], Is.EqualTo(25).Within(Tolerance));
            Assert.That(american["world/a"], Is.EqualTo(10).Within(Tolerance));
            Assert.That(american["world/b"], Is.EqualTo(10).Within(Tolerance));
            Assert.That(american[ExperimentTestData.MasterKey], Is.EqualTo(5).Within(Tolerance));
            Assert.That(german["world/a"], Is.EqualTo(10).Within(Tolerance));
            Assert.That(german["world/b"], Is.EqualTo(10).Within(Tolerance));
            Assert.That(german[ExperimentTestData.MasterKey], Is.EqualTo(80).Within(Tolerance));
        }

        [Test]
        public void ScenarioTwo_RemovedGroupMovesPlayersToMasterAndStopsRecruiting()
        {
            var registry = new ExperimentRegistry();
            var usOnly = _data.CreateRunning(
                "us",
                _data.CreateGroup("g1", 25, true, "US"),
                _data.CreateGroup("g2", 25, true, "US"),
                _data.CreateGroup("g3", 25, true, "US"));

            usOnly.Groups[2].Status = ExperimentGroupDocument.RemovedStatus;
            registry.Replace(new List<ExperimentDocument> { usOnly });

            var american = _data.Distribute(registry, true, "US");

            Assert.That(american.ContainsKey("us/g3"), Is.False);
            Assert.That(american[ExperimentTestData.MasterKey], Is.EqualTo(50).Within(Tolerance));
            Assert.That(registry.TryGetActiveGroup("us", "g3", out _), Is.False);
            Assert.That(registry.TryGetActiveGroup("us", "g1", out var group), Is.True);
            Assert.That(group!.SnapshotVersion, Is.EqualTo("sha256:g1"));
        }

        [Test]
        public void ScenarioTwo_FrozenGroupsKeepParticipantsButStopRecruiting()
        {
            var registry = new ExperimentRegistry();
            var usOnly = _data.CreateRunning("us", _data.CreateGroup("g1", 25, true, "US"), _data.CreateGroup("g2", 25, true, "US"));
            var world = _data.CreateRunning("world", _data.CreateGroup("a", 10, true), _data.CreateGroup("b", 10, true));

            usOnly.Groups[0].Status = ExperimentGroupDocument.FrozenStatus;
            usOnly.Groups[1].Status = ExperimentGroupDocument.FrozenStatus;
            registry.Replace(new List<ExperimentDocument> { usOnly, world });

            var american = _data.Distribute(registry, true, "US");

            Assert.That(american.ContainsKey("us/g1"), Is.False);
            Assert.That(american["world/a"], Is.EqualTo(10).Within(Tolerance));
            Assert.That(american[ExperimentTestData.MasterKey], Is.EqualTo(80).Within(Tolerance));
            Assert.That(registry.TryGetActiveGroup("us", "g1", out _), Is.True);
        }

        [Test]
        public void SeenExperiment_IsNotOfferedAgain()
        {
            var registry = new ExperimentRegistry();
            var candidates = new List<ExperimentCandidate>();

            registry.Replace(new List<ExperimentDocument>
            {
                _data.CreateRunning("old", _data.CreateGroup("x", 50, false)),
                _data.CreateRunning("fresh", _data.CreateGroup("y", 50, false)),
            });

            registry.CollectCandidates(false, "US", new List<string> { "old" }, candidates);

            Assert.That(candidates.Count, Is.EqualTo(1));
            Assert.That(candidates[0].ExperimentId, Is.EqualTo("fresh"));
        }
    }
}
