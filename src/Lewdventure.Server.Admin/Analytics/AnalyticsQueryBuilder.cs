using System.Globalization;
using System.Text;

namespace Server.Admin.Analytics
{
    internal sealed class AnalyticsQueryBuilder
    {
        public const int DefaultDays = 7;
        public const int MaxDays = 400;
        public const int MaxHourlyDays = 14;

        private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";
        private const string MasterGroup = "if(experiment_id = '', '" + AnalyticsFilterValues.MasterLabel + "', concat(experiment_id, '/', group_id))";

        public AnalyticsQuery Build(string database, AnalyticsFilter filter, IReadOnlyList<string> excludedUserIds, DateTime now, List<string> errors)
        {
            var query = new AnalyticsQuery { Table = database + ".events" };

            Normalize(filter);
            ReadPeriod(filter, now, query, errors);

            var where = new StringBuilder("event_time >= {from:DateTime64(3)} AND event_time < {to:DateTime64(3)}");

            query.Parameters["from"] = query.From.ToString(TimeFormat, CultureInfo.InvariantCulture);
            query.Parameters["to"] = query.To.ToString(TimeFormat, CultureInfo.InvariantCulture);

            AddEquals(where, query, "event_type", "event_type", filter.EventType);
            AddEquals(where, query, "user_id", "user_id", filter.UserId);
            AddEquals(where, query, "country", "country", filter.Country.ToUpperInvariant());
            AddEquals(where, query, "source", "source", filter.Source);
            AddEquals(where, query, "group_id", "group_id", filter.GroupId);
            where.Append(BuildExclusion(excludedUserIds, query.Parameters));

            if (string.Equals(filter.ExperimentId, AnalyticsFilterValues.MasterLabel, StringComparison.Ordinal))
                where.Append(" AND experiment_id = ''");
            else
                AddEquals(where, query, "experiment_id", "experiment_id", filter.ExperimentId);

            if (filter.PropertyKey.Length != 0)
            {
                where.Append(" AND (JSONExtractString(event_properties, {property_key:String}) = {property_value:String} OR JSONExtractRaw(event_properties, {property_key:String}) = {property_value:String})");
                query.Parameters["property_key"] = filter.PropertyKey;
                query.Parameters["property_value"] = filter.PropertyValue;
            }

            query.Where = where.ToString();
            query.MetricExpression = BuildMetric(filter, query, errors);
            query.GroupExpression = BuildGroup(filter, query, errors);
            query.BucketExpression = query.IsHourly ? "toStartOfHour(event_time)" : "toStartOfDay(event_time)";

            return query;
        }

        public string BuildExclusion(IReadOnlyList<string> excludedUserIds, Dictionary<string, string> parameters)
        {
            if (excludedUserIds.Count == 0)
                return string.Empty;

            var value = new StringBuilder("[");

            for (int i = 0; i < excludedUserIds.Count; i++)
            {
                if (0 < i)
                    value.Append(',');

                value.Append('\'').Append(excludedUserIds[i].Replace("\\", "\\\\").Replace("'", "\\'")).Append('\'');
            }

            parameters["excluded_users"] = value.Append(']').ToString();

            return " AND user_id NOT IN {excluded_users:Array(String)}";
        }

        public bool IsValidDatabase(string database)
        {
            if (database.Length == 0)
                return false;

            for (int i = 0; i < database.Length; i++)
            {
                var symbol = database[i];

                if (char.IsAsciiLetterLower(symbol) == false && char.IsAsciiDigit(symbol) == false && symbol != '_')
                    return false;
            }

            return true;
        }

        private void Normalize(AnalyticsFilter filter)
        {
            filter.From = Clean(filter.From);
            filter.To = Clean(filter.To);
            filter.Step = Clean(filter.Step);
            filter.EventType = Clean(filter.EventType);
            filter.Metric = Clean(filter.Metric);
            filter.MetricKey = Clean(filter.MetricKey);
            filter.GroupBy = Clean(filter.GroupBy);
            filter.GroupKey = Clean(filter.GroupKey);
            filter.UserId = Clean(filter.UserId);
            filter.ExperimentId = Clean(filter.ExperimentId);
            filter.GroupId = Clean(filter.GroupId);
            filter.Country = Clean(filter.Country);
            filter.Source = Clean(filter.Source);
            filter.PropertyKey = Clean(filter.PropertyKey);
            filter.PropertyValue = Clean(filter.PropertyValue);
        }

        private string Clean(string? value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        private void ReadPeriod(AnalyticsFilter filter, DateTime now, AnalyticsQuery query, List<string> errors)
        {
            var today = now.Date;
            var to = TryReadDate(filter.To, out var parsedTo) ? parsedTo.AddDays(1) : today.AddDays(1);
            var from = TryReadDate(filter.From, out var parsedFrom) ? parsedFrom : to.AddDays(-DefaultDays);

            if (to <= from)
            {
                errors.Add("Дата «по» раньше даты «с», показан период по умолчанию.");
                to = today.AddDays(1);
                from = to.AddDays(-DefaultDays);
            }

            if (MaxDays < (to - from).TotalDays)
            {
                errors.Add($"Период больше {MaxDays} дней, обрезан.");
                from = to.AddDays(-MaxDays);
            }

            query.From = from;
            query.To = to;
            query.IsHourly = string.Equals(filter.Step, AnalyticsFilterValues.StepHour, StringComparison.Ordinal) && (to - from).TotalDays <= MaxHourlyDays;
            filter.From = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            filter.To = to.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private bool TryReadDate(string value, out DateTime date)
        {
            return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);
        }

        private void AddEquals(StringBuilder where, AnalyticsQuery query, string column, string parameter, string value)
        {
            if (value.Length == 0)
                return;

            where.Append(" AND ").Append(column).Append(" = {").Append(parameter).Append(":String}");
            query.Parameters[parameter] = value;
        }

        private string BuildMetric(AnalyticsFilter filter, AnalyticsQuery query, List<string> errors)
        {
            switch (filter.Metric)
            {
                case AnalyticsFilterValues.MetricUsers:
                    return "uniqExact(user_id)";
                case AnalyticsFilterValues.MetricSum:
                case AnalyticsFilterValues.MetricAverage:
                    if (filter.MetricKey.Length == 0)
                    {
                        errors.Add("Для суммы и среднего укажите свойство события.");
                        filter.Metric = AnalyticsFilterValues.MetricEvents;

                        return "count()";
                    }

                    query.Parameters["metric_key"] = filter.MetricKey;

                    return filter.Metric == AnalyticsFilterValues.MetricSum
                        ? "round(sum(JSONExtractFloat(event_properties, {metric_key:String})), 2)"
                        : "round(avg(JSONExtractFloat(event_properties, {metric_key:String})), 2)";
                default:
                    filter.Metric = AnalyticsFilterValues.MetricEvents;

                    return "count()";
            }
        }

        private string BuildGroup(AnalyticsFilter filter, AnalyticsQuery query, List<string> errors)
        {
            switch (filter.GroupBy)
            {
                case AnalyticsFilterValues.GroupExperiment:
                    return MasterGroup;
                case AnalyticsFilterValues.GroupEventType:
                case AnalyticsFilterValues.GroupCountry:
                case AnalyticsFilterValues.GroupAppVersion:
                case AnalyticsFilterValues.GroupPlatform:
                case AnalyticsFilterValues.GroupSource:
                    return "toString(" + filter.GroupBy + ")";
                case AnalyticsFilterValues.GroupProperty:
                    if (filter.GroupKey.Length == 0)
                    {
                        errors.Add("Для группировки по свойству укажите его имя.");
                        filter.GroupBy = AnalyticsFilterValues.GroupNone;

                        return "'" + AnalyticsFilterValues.AllLabel + "'";
                    }

                    query.Parameters["group_key"] = filter.GroupKey;

                    return "trim(BOTH '\"' FROM JSONExtractRaw(event_properties, {group_key:String}))";
                default:
                    filter.GroupBy = AnalyticsFilterValues.GroupNone;

                    return "'" + AnalyticsFilterValues.AllLabel + "'";
            }
        }
    }
}
