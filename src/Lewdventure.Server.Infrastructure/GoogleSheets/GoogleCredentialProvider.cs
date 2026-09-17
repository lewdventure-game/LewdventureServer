using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;

namespace Server.Infrastructure.GoogleSheets
{
    internal sealed class GoogleCredentialProvider
    {
        public GoogleCredential Create(GoogleSheetsOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.CredentialsJson) == false)
            {
                return CredentialFactory.FromJson<ServiceAccountCredential>(options.CredentialsJson)
                    .ToGoogleCredential()
                    .CreateScoped(SheetsService.Scope.SpreadsheetsReadonly);
            }

            var credentialPath = ResolvePath(options.CredentialsPath);

            if (File.Exists(credentialPath) == false)
                throw new FileNotFoundException($"Google credentials file not found path = {credentialPath}. Set GoogleSheets:CredentialsPath or GoogleSheets:CredentialsJson.");

            return CredentialFactory.FromFile<ServiceAccountCredential>(credentialPath)
                .ToGoogleCredential()
                .CreateScoped(SheetsService.Scope.SpreadsheetsReadonly);
        }

        public string ResolvePath(string credentialsPath)
        {
            if (Path.IsPathRooted(credentialsPath))
                return credentialsPath;

            return Path.Combine(AppContext.BaseDirectory, credentialsPath);
        }
    }
}
