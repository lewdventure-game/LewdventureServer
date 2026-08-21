using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Server.Battles
{
    internal sealed class UnitSnapshotJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(IUnitSnapshot);
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var token = JObject.Load(reader);
            var unitSnapshot = new UnitSnapshot();
            serializer.Populate(token.CreateReader(), unitSnapshot);
            NormalizeEquipment(unitSnapshot, token);

            return unitSnapshot;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }

        private static void NormalizeEquipment(UnitSnapshot unitSnapshot, JObject token)
        {
            if (unitSnapshot.Equipment == null)
                unitSnapshot.Equipment = new List<IEquipmentSnapshot>();

            if (unitSnapshot.EquipmentIds == null)
                unitSnapshot.EquipmentIds = new List<int>();

            if (unitSnapshot.ArtifactIds == null)
                unitSnapshot.ArtifactIds = new List<int>();

            if (unitSnapshot.AspectIds == null)
                unitSnapshot.AspectIds = new List<int>();

            if (unitSnapshot.ActivePerkIds == null)
                unitSnapshot.ActivePerkIds = new List<int>();

            if (unitSnapshot.ActiveSkillIds == null)
                unitSnapshot.ActiveSkillIds = new List<string>();

            if (unitSnapshot.ActiveStatusIds == null)
                unitSnapshot.ActiveStatusIds = new List<int>();

            var hasEquipmentProperty = token["equipment"] != null || token["Equipment"] != null;
            var hasEquipmentIdsProperty = token["equipmentIds"] != null || token["EquipmentIds"] != null;

            if (unitSnapshot.Equipment.Count == 0 && hasEquipmentIdsProperty)
            {
                for (int i = 0; i < unitSnapshot.EquipmentIds.Count; i++)
                {
                    unitSnapshot.Equipment.Add(new EquipmentSnapshot
                    {
                        Id = unitSnapshot.EquipmentIds[i],
                        Level = 1,
                    });
                }
            }
            else if (unitSnapshot.EquipmentIds.Count == 0 && hasEquipmentProperty)
            {
                for (int i = 0; i < unitSnapshot.Equipment.Count; i++)
                {
                    var entry = unitSnapshot.Equipment[i];

                    if (entry == null)
                        continue;

                    unitSnapshot.EquipmentIds.Add(entry.Id);
                }
            }
            else if (unitSnapshot.Equipment.Count == 0 && unitSnapshot.EquipmentIds.Count != 0)
            {
                for (int i = 0; i < unitSnapshot.EquipmentIds.Count; i++)
                {
                    unitSnapshot.Equipment.Add(new EquipmentSnapshot
                    {
                        Id = unitSnapshot.EquipmentIds[i],
                        Level = 1,
                    });
                }
            }
        }
    }
}
