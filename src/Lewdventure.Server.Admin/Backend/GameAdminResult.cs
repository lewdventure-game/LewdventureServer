namespace Server.Admin.Backend
{
    internal sealed class GameAdminResult<T>
    {
        public bool IsSuccess { get; set; }

        public int StatusCode { get; set; }

        public T? Data { get; set; }

        public List<string> Errors { get; } = new();

        public List<string> Warnings { get; } = new();
    }
}
