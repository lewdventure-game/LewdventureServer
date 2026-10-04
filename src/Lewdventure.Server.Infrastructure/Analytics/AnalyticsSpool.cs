using Microsoft.Extensions.Options;

namespace Server.Infrastructure.Analytics
{
    internal sealed class AnalyticsSpool
    {
        private const string FilePattern = "*.ndjson";
        private const long BytesPerMegabyte = 1048576;

        private readonly ILogger<AnalyticsSpool> _logger;
        private readonly AnalyticsOptions _options;

        public AnalyticsSpool(ILogger<AnalyticsSpool> logger, IOptions<AnalyticsOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        public bool IsEnabled => string.IsNullOrWhiteSpace(_options.SpoolPath) == false;

        public int Write(string payload)
        {
            if (IsEnabled == false)
                return 0;

            Directory.CreateDirectory(_options.SpoolPath);

            var path = Path.Combine(_options.SpoolPath, $"{DateTime.UtcNow.Ticks:D20}-{Guid.NewGuid():N}.ndjson");

            File.WriteAllText(path, payload);

            return TrimToLimit();
        }

        public bool TryReadOldest(out string path, out string payload)
        {
            path = string.Empty;
            payload = string.Empty;

            if (IsEnabled == false || Directory.Exists(_options.SpoolPath) == false)
                return false;

            var files = Directory.GetFiles(_options.SpoolPath, FilePattern);

            if (files.Length == 0)
                return false;

            Array.Sort(files, StringComparer.Ordinal);
            path = files[0];
            payload = File.ReadAllText(path);

            return true;
        }

        public void Delete(string path)
        {
            File.Delete(path);
        }

        public int CountFiles()
        {
            if (IsEnabled == false || Directory.Exists(_options.SpoolPath) == false)
                return 0;

            return Directory.GetFiles(_options.SpoolPath, FilePattern).Length;
        }

        private int TrimToLimit()
        {
            var files = Directory.GetFiles(_options.SpoolPath, FilePattern);
            var limit = _options.SpoolMaxMegabytes * BytesPerMegabyte;
            var total = 0L;
            var removed = 0;

            Array.Sort(files, StringComparer.Ordinal);

            for (int i = 0; i < files.Length; i++)
                total += new FileInfo(files[i]).Length;

            for (int i = 0; i < files.Length && limit < total; i++)
            {
                total -= new FileInfo(files[i]).Length;
                File.Delete(files[i]);
                removed += 1;
            }

            if (0 < removed)
                _logger.LogWarning("[Analytics] spool over limit, oldest batches dropped files = {Files}", removed);

            return removed;
        }
    }
}
