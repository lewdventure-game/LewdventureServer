namespace Server.LoadTest
{
    internal sealed class Program
    {
        private static async Task<int> Main(string[] args)
        {
            return await new LoadTestApplication().RunAsync(args);
        }
    }
}
