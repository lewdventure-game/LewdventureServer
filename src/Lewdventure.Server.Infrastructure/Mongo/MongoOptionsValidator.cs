using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Server.Infrastructure.Mongo
{
    internal sealed class MongoOptionsValidator : IValidateOptions<MongoOptions>
    {
        public ValidateOptionsResult Validate(string? name, MongoOptions options)
        {
            if (options.Enabled == false)
                return ValidateOptionsResult.Success;

            var failures = new List<string>();

            if (string.IsNullOrWhiteSpace(options.ConnectionString))
                failures.Add("Mongo:ConnectionString is required when Mongo:Enabled is true.");
            else if (IsValidConnectionString(options.ConnectionString) == false)
                failures.Add("Mongo:ConnectionString is not a valid MongoDB connection string.");

            if (IsValidDatabaseName(options.DatabaseName) == false)
                failures.Add("Mongo:DatabaseName must contain only lowercase letters, digits and underscores.");

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }

        private bool IsValidConnectionString(string connectionString)
        {
            try
            {
                MongoUrl.Create(connectionString);

                return true;
            }
            catch (MongoConfigurationException)
            {
                return false;
            }
        }

        private bool IsValidDatabaseName(string databaseName)
        {
            if (string.IsNullOrEmpty(databaseName) || 63 < databaseName.Length)
                return false;

            for (int i = 0; i < databaseName.Length; i++)
            {
                var symbol = databaseName[i];

                if (char.IsAsciiLetterLower(symbol) == false && char.IsAsciiDigit(symbol) == false && symbol != '_')
                    return false;
            }

            return true;
        }
    }
}
