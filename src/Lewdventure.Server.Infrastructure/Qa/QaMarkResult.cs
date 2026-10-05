namespace Server.Infrastructure.Qa
{
    internal sealed class QaMarkResult
    {
        public QaMarkResult(bool notFound, string error)
        {
            NotFound = notFound;
            Error = error;
        }

        public bool NotFound { get; }

        public string Error { get; }

        public bool Succeeded => NotFound == false && Error.Length == 0;
    }
}
