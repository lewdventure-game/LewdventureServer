namespace Server.Admin.Accounts
{
    internal sealed class AdminAccountsCommand
    {
        private const string CommandName = "accounts";
        private const string DataPathVariable = "AdminPanel__DataPath";
        private const string DefaultDataPath = "data";
        private const int MaxLoginLength = 32;

        private readonly AdminPasswordHasher _passwordHasher = new();
        private readonly AdminRoles _roles = new();

        public bool IsRequested(string[] args)
        {
            return 0 < args.Length && string.Equals(args[0], CommandName, StringComparison.Ordinal);
        }

        public int Run(string[] args)
        {
            var dataPath = Environment.GetEnvironmentVariable(DataPathVariable);
            var store = new AdminAccountStore(string.IsNullOrEmpty(dataPath) ? DefaultDataPath : dataPath);
            var action = 1 < args.Length ? args[1] : string.Empty;

            switch (action)
            {
                case "list":
                    return List(store);
                case "add":
                    return Add(store, args);
                case "reset":
                    return Reset(store, args);
                case "disable":
                    return SetDisabled(store, args, true);
                case "enable":
                    return SetDisabled(store, args, false);
                default:
                    Console.Error.WriteLine("usage: accounts list | add <login> <admin|tester> | reset <login> | disable <login> | enable <login>");

                    return 2;
            }
        }

        private int List(AdminAccountStore store)
        {
            var accounts = store.Load();

            for (int i = 0; i < accounts.Count; i++)
            {
                var account = accounts[i];

                Console.WriteLine($"{account.Login}\t{account.Role}\t{(account.IsDisabled ? "disabled" : "active")}\tcreated={account.CreatedAt:yyyy-MM-dd}\tlastLogin={account.LastLoginAt:yyyy-MM-dd HH:mm}");
            }

            return 0;
        }

        private int Add(AdminAccountStore store, string[] args)
        {
            if (args.Length < 4 || IsValidLogin(args[2]) == false || _roles.IsKnown(args[3]) == false)
            {
                Console.Error.WriteLine($"usage: accounts add <login: a-z 0-9 . _ - up to {MaxLoginLength}> <admin|tester>");

                return 2;
            }

            if (store.Find(args[2]) != null)
            {
                Console.Error.WriteLine($"account {args[2]} already exists, use reset");

                return 1;
            }

            var account = new AdminAccount
            {
                Login = args[2],
                Role = args[3],
                CreatedAt = DateTime.UtcNow,
            };
            var password = _passwordHasher.GeneratePassword();

            _passwordHasher.SetPassword(account, password);
            store.Upsert(account);

            Console.WriteLine($"{account.Login}\t{account.Role}\t{password}");

            return 0;
        }

        private int Reset(AdminAccountStore store, string[] args)
        {
            var account = args.Length < 3 ? null : store.Find(args[2]);

            if (account == null)
            {
                Console.Error.WriteLine("account not found");

                return 1;
            }

            var password = _passwordHasher.GeneratePassword();

            _passwordHasher.SetPassword(account, password);
            account.IsDisabled = false;
            store.Upsert(account);

            Console.WriteLine($"{account.Login}\t{account.Role}\t{password}");

            return 0;
        }

        private int SetDisabled(AdminAccountStore store, string[] args, bool isDisabled)
        {
            var account = args.Length < 3 ? null : store.Find(args[2]);

            if (account == null)
            {
                Console.Error.WriteLine("account not found");

                return 1;
            }

            account.IsDisabled = isDisabled;
            store.Upsert(account);

            Console.WriteLine($"{account.Login}\t{(isDisabled ? "disabled" : "enabled")}");

            return 0;
        }

        private bool IsValidLogin(string login)
        {
            if (login.Length == 0 || MaxLoginLength < login.Length)
                return false;

            for (int i = 0; i < login.Length; i++)
            {
                var symbol = login[i];
                var isAllowed = (char.IsAsciiLetterLower(symbol) || char.IsAsciiDigit(symbol)) || symbol == '.' || symbol == '_' || symbol == '-';

                if (isAllowed == false)
                    return false;
            }

            return true;
        }
    }
}
