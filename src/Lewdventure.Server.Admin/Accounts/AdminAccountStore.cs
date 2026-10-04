using System.Text.Json;

namespace Server.Admin.Accounts
{
    internal sealed class AdminAccountStore
    {
        public const string FileName = "admin-accounts.json";

        private readonly string _filePath;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
        private readonly object _sync = new();

        public AdminAccountStore(string dataPath)
        {
            _filePath = Path.Combine(dataPath, FileName);
        }

        public string FilePath => _filePath;

        public List<AdminAccount> Load()
        {
            lock (_sync)
            {
                return Read();
            }
        }

        public AdminAccount? Find(string login)
        {
            lock (_sync)
            {
                var accounts = Read();

                for (int i = 0; i < accounts.Count; i++)
                {
                    if (string.Equals(accounts[i].Login, login, StringComparison.OrdinalIgnoreCase))
                        return accounts[i];
                }

                return null;
            }
        }

        public void Upsert(AdminAccount account)
        {
            lock (_sync)
            {
                var accounts = Read();
                var replaced = false;

                for (int i = 0; i < accounts.Count; i++)
                {
                    if (string.Equals(accounts[i].Login, account.Login, StringComparison.OrdinalIgnoreCase) == false)
                        continue;

                    accounts[i] = account;
                    replaced = true;

                    break;
                }

                if (replaced == false)
                    accounts.Add(account);

                Write(accounts);
            }
        }

        private List<AdminAccount> Read()
        {
            if (File.Exists(_filePath) == false)
                return new List<AdminAccount>();

            var json = File.ReadAllText(_filePath);
            var accounts = JsonSerializer.Deserialize<List<AdminAccount>>(json, _jsonOptions);

            return accounts ?? new List<AdminAccount>();
        }

        private void Write(List<AdminAccount> accounts)
        {
            var directory = Path.GetDirectoryName(_filePath);

            if (string.IsNullOrEmpty(directory) == false)
                Directory.CreateDirectory(directory);

            var temporaryPath = _filePath + ".tmp";

            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(accounts, _jsonOptions));
            File.Move(temporaryPath, _filePath, true);
        }
    }
}
