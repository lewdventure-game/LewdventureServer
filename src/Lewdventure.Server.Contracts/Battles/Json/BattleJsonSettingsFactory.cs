using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Server.Battles
{
    public sealed class BattleJsonSettingsFactory
    {
        public JsonSerializerSettings Create()
        {
            var serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            };

            serializerSettings.Converters.Add(new TeamSnapshotJsonConverter());
            serializerSettings.Converters.Add(new UnitSnapshotJsonConverter());
            serializerSettings.Converters.Add(new EquipmentSnapshotJsonConverter());
            serializerSettings.Converters.Add(new BonusGrantSnapshotJsonConverter());

            return serializerSettings;
        }
    }
}
