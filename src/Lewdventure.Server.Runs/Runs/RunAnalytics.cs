using Server.Infrastructure.Analytics;
using Server.Infrastructure.Mongo.Runs;

namespace Server.Runs
{
    internal sealed class RunAnalytics
    {
        public const string RunStartedEvent = "run_started";
        public const string RunFinishedEvent = "run_finished";

        private readonly AnalyticsRowFactory _analyticsRowFactory;
        private readonly IAnalyticsSink _analyticsSink;

        public RunAnalytics(AnalyticsRowFactory analyticsRowFactory, IAnalyticsSink analyticsSink)
        {
            _analyticsRowFactory = analyticsRowFactory;
            _analyticsSink = analyticsSink;
        }

        public void TrackStarted(RunDocument run)
        {
            _analyticsSink.TryEnqueue(_analyticsRowFactory.CreateServerEvent(RunStartedEvent, run.UserId, new Dictionary<string, object>
            {
                ["run_id"] = run.Id,
                ["story_level_id"] = run.StoryLevelId,
                ["stages"] = run.Stages.Count,
            }));
        }

        public void TrackFinished(RunDocument run)
        {
            _analyticsSink.TryEnqueue(_analyticsRowFactory.CreateServerEvent(RunFinishedEvent, run.UserId, new Dictionary<string, object>
            {
                ["run_id"] = run.Id,
                ["story_level_id"] = run.StoryLevelId,
                ["status"] = run.Status,
                ["stage_index"] = run.StageIndex,
                ["stages"] = run.Stages.Count,
                ["experience_level"] = run.ExperienceLevel,
                ["perks"] = run.Perks.Count,
                ["duration_seconds"] = (long)(run.UpdatedAt - run.CreatedAt).TotalSeconds,
            }));
        }
    }
}
