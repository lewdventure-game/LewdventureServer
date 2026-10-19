using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Runs;

namespace Server.Runs
{
    internal sealed class RunRecords
    {
        public bool IsNewRecord(PlayerProfileDocument profile, RunDocument run)
        {
            var record = FindRecord(profile, run.StoryLevelId);
            var best = record == null ? 0 : record.BestStageIndex;

            return best < run.StageIndex;
        }

        public void StoreRecord(PlayerProfileDocument profile, RunDocument run)
        {
            var record = FindRecord(profile, run.StoryLevelId);

            if (record == null)
            {
                profile.Story.Records.Add(new PlayerStoryRecordDocument { StoryLevelId = run.StoryLevelId, BestStageIndex = run.StageIndex });

                return;
            }

            if (record.BestStageIndex < run.StageIndex)
                record.BestStageIndex = run.StageIndex;
        }

        private PlayerStoryRecordDocument? FindRecord(PlayerProfileDocument profile, int storyLevelId)
        {
            var records = profile.Story.Records;

            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].StoryLevelId == storyLevelId)
                    return records[i];
            }

            return null;
        }
    }
}
