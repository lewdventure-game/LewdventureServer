using System.Globalization;
using Server.Infrastructure.Mongo.Experiments;

namespace Server.Infrastructure.Experiments
{
    internal sealed class ExperimentAllocationValidator
    {
        public const double MaxPercent = 100d;
        public const string AnyCountry = "*";

        private const double Tolerance = 0.0001d;
        private const int MaxIdLength = 40;

        public void ValidateStructure(ExperimentDocument experiment, List<string> errors)
        {
            if (IsValidId(experiment.Id) == false)
                errors.Add($"Experiment id '{experiment.Id}' must be 1-{MaxIdLength} chars of a-z, 0-9, '_' or '-'.");

            if (experiment.Groups.Count == 0)
                errors.Add("Experiment must have at least one group.");

            var groupIds = new List<string>(experiment.Groups.Count);

            for (int i = 0; i < experiment.Groups.Count; i++)
            {
                var group = experiment.Groups[i];

                if (IsValidId(group.Id) == false)
                    errors.Add($"Group id '{group.Id}' must be 1-{MaxIdLength} chars of a-z, 0-9, '_' or '-'.");
                else if (groupIds.Contains(group.Id))
                    errors.Add($"Group id '{group.Id}' is duplicated.");

                groupIds.Add(group.Id);

                if (group.Percent <= 0d || MaxPercent < group.Percent)
                    errors.Add($"Group '{group.Id}' percent must be in (0, 100].");

                if (string.IsNullOrWhiteSpace(group.SnapshotVersion))
                    errors.Add($"Group '{group.Id}' snapshotVersion is required.");

                ValidateCountries(group, errors);
            }
        }

        public void ValidateAllocation(ExperimentDocument experiment, IReadOnlyList<ExperimentDocument> running, List<string> errors)
        {
            var groups = new List<ExperimentGroupDocument>();

            for (int i = 0; i < running.Count; i++)
            {
                if (string.Equals(running[i].Id, experiment.Id, StringComparison.Ordinal) == false)
                    CollectRecruiting(running[i], groups);
            }

            CollectRecruiting(experiment, groups);

            var countries = new List<string> { AnyCountry };

            for (int i = 0; i < groups.Count; i++)
            {
                var groupCountries = groups[i].Filter.Countries;

                for (int j = 0; j < groupCountries.Count; j++)
                {
                    if (countries.Contains(groupCountries[j]) == false)
                        countries.Add(groupCountries[j]);
                }
            }

            for (int i = 0; i < countries.Count; i++)
            {
                var total = SumForCountry(groups, countries[i]);

                if (MaxPercent + Tolerance < total)
                    errors.Add($"Recruiting groups for country {countries[i]} take {total.ToString("0.##", CultureInfo.InvariantCulture)}% of players, max is 100%.");
            }
        }

        private void CollectRecruiting(ExperimentDocument experiment, List<ExperimentGroupDocument> groups)
        {
            for (int i = 0; i < experiment.Groups.Count; i++)
            {
                if (string.Equals(experiment.Groups[i].Status, ExperimentGroupDocument.RecruitingStatus, StringComparison.Ordinal))
                    groups.Add(experiment.Groups[i]);
            }
        }

        private double SumForCountry(List<ExperimentGroupDocument> groups, string country)
        {
            var total = 0d;

            for (int i = 0; i < groups.Count; i++)
            {
                var groupCountries = groups[i].Filter.Countries;

                if (groupCountries.Count == 0 || groupCountries.Contains(country))
                    total += groups[i].Percent;
            }

            return total;
        }

        private void ValidateCountries(ExperimentGroupDocument group, List<string> errors)
        {
            var countries = group.Filter.Countries;

            for (int i = 0; i < countries.Count; i++)
            {
                if (IsCountryCode(countries[i]) == false)
                    errors.Add($"Group '{group.Id}' country '{countries[i]}' must be an ISO 3166 alpha-2 code in upper case.");
            }
        }

        private bool IsCountryCode(string country)
        {
            return country.Length == 2 && IsUpperLatin(country[0]) && IsUpperLatin(country[1]);
        }

        private bool IsUpperLatin(char symbol)
        {
            return 'A' <= symbol && symbol <= 'Z';
        }

        private bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id) || MaxIdLength < id.Length)
                return false;

            for (int i = 0; i < id.Length; i++)
            {
                var symbol = id[i];
                var isAllowed = ('a' <= symbol && symbol <= 'z') || ('0' <= symbol && symbol <= '9') || symbol == '_' || symbol == '-';

                if (isAllowed == false)
                    return false;
            }

            return true;
        }
    }
}
