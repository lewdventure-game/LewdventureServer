namespace Server.Infrastructure.Experiments
{
    internal sealed class ExperimentGroupPicker
    {
        public ExperimentCandidate? Pick(List<ExperimentCandidate> candidates, double roll)
        {
            var upperBound = 0d;

            for (int i = 0; i < candidates.Count; i++)
            {
                upperBound += candidates[i].Group.Percent;

                if (roll < upperBound)
                    return candidates[i];
            }

            return null;
        }
    }
}
