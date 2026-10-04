using System.Text.Json;

namespace Server.Admin.Backend
{
    internal sealed class AdminAuditLog
    {
        public const string FileName = "audit.log";

        private readonly string _filePath;
        private readonly ILogger<AdminAuditLog> _logger;
        private readonly object _sync = new();

        public AdminAuditLog(string dataPath, ILogger<AdminAuditLog> logger)
        {
            _filePath = Path.Combine(dataPath, FileName);
            _logger = logger;
        }

        public void Append(string login, string environment, string action, string target, bool succeeded)
        {
            var line = JsonSerializer.Serialize(new
            {
                time = DateTime.UtcNow,
                login,
                environment,
                action,
                target,
                succeeded,
            });

            _logger.LogInformation("[Admin] audit login = {Login} environment = {Environment} action = {Action} target = {Target} succeeded = {Succeeded}", login, environment, action, target, succeeded);

            lock (_sync)
            {
                File.AppendAllText(_filePath, line + Environment.NewLine);
            }
        }
    }
}
