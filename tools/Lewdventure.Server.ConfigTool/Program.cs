namespace Server.ConfigTool
{
    internal sealed class Program
    {
        private static async Task<int> Main(string[] args)
        {
            return await new ConfigToolApplication().RunAsync(args);
        }
    }
}
