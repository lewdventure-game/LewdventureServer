using Server.Admin.Accounts;
using Server.Admin.Hosting;

namespace Server.Admin
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var accountsCommand = new AdminAccountsCommand();

            if (accountsCommand.IsRequested(args))
                return accountsCommand.Run(args);

            await new AdminHost().RunAsync(args);

            return 0;
        }
    }
}
