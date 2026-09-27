namespace Server.Api.Security
{
    internal readonly struct AccessToken
    {
        private readonly string _value;
        private readonly DateTime _expiresAt;

        public AccessToken(string value, DateTime expiresAt)
        {
            _value = value;
            _expiresAt = expiresAt;
        }

        public string Value => _value;

        public DateTime ExpiresAt => _expiresAt;
    }
}
