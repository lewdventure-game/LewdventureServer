namespace Server.Api.Endpoints
{
    internal sealed class EntityTagReader
    {
        private const string WeakPrefix = "W/";
        private const string AnyTag = "*";

        private readonly string[] _encodingSuffixes = { "-gzip", "-br", "-zstd", "-deflate" };

        public bool Matches(string headerValue, string entityTagValue)
        {
            if (string.IsNullOrEmpty(headerValue))
                return false;

            var candidates = headerValue.Split(',');

            for (int i = 0; i < candidates.Length; i++)
            {
                if (MatchesCandidate(candidates[i], entityTagValue))
                    return true;
            }

            return false;
        }

        private bool MatchesCandidate(string candidate, string entityTagValue)
        {
            var value = candidate.Trim();

            if (value.Length == 0)
                return false;

            if (string.Equals(value, AnyTag, StringComparison.Ordinal))
                return true;

            if (value.StartsWith(WeakPrefix, StringComparison.OrdinalIgnoreCase))
                value = value.Substring(WeakPrefix.Length).TrimStart();

            value = value.Trim('"');
            value = RemoveEncodingSuffix(value);

            return string.Equals(value, entityTagValue, StringComparison.Ordinal);
        }

        private string RemoveEncodingSuffix(string value)
        {
            for (int i = 0; i < _encodingSuffixes.Length; i++)
            {
                var suffix = _encodingSuffixes[i];

                if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return value.Substring(0, value.Length - suffix.Length);
            }

            return value;
        }
    }
}
