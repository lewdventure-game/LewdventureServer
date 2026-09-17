using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;

namespace Server.Infrastructure.Mongo
{
    internal sealed class MongoConventionsRegistrar
    {
        private const string ConventionPackName = "lewdventure";

        private int _registered;

        public void Register()
        {
            if (Interlocked.Exchange(ref _registered, 1) == 1)
                return;

            var pack = new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new EnumRepresentationConvention(BsonType.String),
                new IgnoreExtraElementsConvention(true),
            };

            ConventionRegistry.Register(ConventionPackName, pack, IsLewdventureType);
        }

        private bool IsLewdventureType(Type type)
        {
            var typeNamespace = type.Namespace;

            return typeNamespace != null && typeNamespace.StartsWith("Server", StringComparison.Ordinal);
        }
    }
}
