namespace Server.Api.Http
{
    internal sealed class ClientCountryReader
    {
        public const string HeaderName = "CF-IPCountry";
        public const string UnknownCountry = "XX";

        public string Read(HttpRequest request)
        {
            var value = request.Headers[HeaderName].ToString().Trim();

            if (value.Length != 2)
                return UnknownCountry;

            var country = value.ToUpperInvariant();

            if (IsLatinLetter(country[0]) == false || IsLatinLetter(country[1]) == false)
                return UnknownCountry;

            return country;
        }

        private bool IsLatinLetter(char symbol)
        {
            return 'A' <= symbol && symbol <= 'Z';
        }
    }
}
