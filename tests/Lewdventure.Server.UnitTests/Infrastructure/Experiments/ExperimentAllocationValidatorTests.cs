using Server.Infrastructure.Experiments;
using Server.Infrastructure.Mongo.Experiments;

namespace Tests.Unit.Infrastructure.Experiments
{
    [TestFixture]
    public sealed class ExperimentAllocationValidatorTests
    {
        private readonly ExperimentTestData _data = new();
        private readonly ExperimentAllocationValidator _validator = new();

        [Test]
        public void ValidateAllocation_ScenarioTwo_Fits()
        {
            var running = new List<ExperimentDocument>
            {
                _data.CreateRunning("us", _data.CreateGroup("g1", 25, true, "US"), _data.CreateGroup("g2", 25, true, "US"), _data.CreateGroup("g3", 25, true, "US")),
            };
            var world = _data.CreateRunning("world", _data.CreateGroup("a", 10, true), _data.CreateGroup("b", 10, true));
            var errors = new List<string>();

            _validator.ValidateAllocation(world, running, errors);

            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void ValidateAllocation_CountryOverflow_IsRejected()
        {
            var running = new List<ExperimentDocument>
            {
                _data.CreateRunning("us", _data.CreateGroup("g1", 40, true, "US"), _data.CreateGroup("g2", 40, true, "US")),
            };
            var world = _data.CreateRunning("world", _data.CreateGroup("a", 30, false));
            var errors = new List<string>();

            _validator.ValidateAllocation(world, running, errors);

            Assert.That(errors.Count, Is.EqualTo(1));
            Assert.That(errors[0], Does.Contain("country US").And.Contain("110"));
        }

        [Test]
        public void ValidateAllocation_FrozenAndRemovedGroupsDoNotTakeTraffic()
        {
            var us = _data.CreateRunning("us", _data.CreateGroup("g1", 60, true, "US"), _data.CreateGroup("g2", 40, true, "US"));

            us.Groups[0].Status = ExperimentGroupDocument.FrozenStatus;
            us.Groups[1].Status = ExperimentGroupDocument.RemovedStatus;

            var world = _data.CreateRunning("world", _data.CreateGroup("a", 100, false));
            var errors = new List<string>();

            _validator.ValidateAllocation(world, new List<ExperimentDocument> { us }, errors);

            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void ValidateStructure_ReportsBadIdsPercentsAndCountries()
        {
            var experiment = _data.CreateRunning(
                "Bad Id",
                _data.CreateGroup("a", 0, false),
                _data.CreateGroup("a", 120, false, "usa"));
            var errors = new List<string>();

            experiment.Groups[1].SnapshotVersion = string.Empty;

            _validator.ValidateStructure(experiment, errors);

            Assert.That(errors, Has.Some.Contains("Experiment id"));
            Assert.That(errors, Has.Some.Contains("duplicated"));
            Assert.That(errors, Has.Some.Contains("percent"));
            Assert.That(errors, Has.Some.Contains("snapshotVersion"));
            Assert.That(errors, Has.Some.Contains("country 'usa'"));
        }

        [Test]
        public void Picker_UsesCumulativeRanges()
        {
            var picker = new ExperimentGroupPicker();
            var candidates = new List<ExperimentCandidate>
            {
                new("e", _data.CreateGroup("first", 10, false)),
                new("e", _data.CreateGroup("second", 20, false)),
            };

            Assert.That(picker.Pick(candidates, 0)!.Group.Id, Is.EqualTo("first"));
            Assert.That(picker.Pick(candidates, 9.99)!.Group.Id, Is.EqualTo("first"));
            Assert.That(picker.Pick(candidates, 10)!.Group.Id, Is.EqualTo("second"));
            Assert.That(picker.Pick(candidates, 29.99)!.Group.Id, Is.EqualTo("second"));
            Assert.That(picker.Pick(candidates, 30), Is.Null);
        }
    }
}
