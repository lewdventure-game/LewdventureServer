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

        public override object? ReadJson(
            JsonReader reader,
            Type objectType,
            object? existingValue,
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

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }

        private static void NormalizeEquipment(UnitSnapshot unitSnapshot, JObject token)
        {
            CheckFields(unitSnapshot);

            var hasEquipmentsProperty = token["equipments"] != null || token["Equipments"] != null;
            var hasEquipmentIdsProperty = token["equipmentIds"] != null || token["EquipmentIds"] != null;

            var equipments = unitSnapshot.Equipments;
            var equipmentIds = unitSnapshot.EquipmentIds;

            if (equipments.Count == 0 && hasEquipmentIdsProperty)
            {
                for (int i = 0; i < equipmentIds.Count; i++)
                {
                    var id = equipmentIds[i];

                    equipments.Add(new EquipmentSnapshot(id, 1));
                }
            }
            else if (equipmentIds.Count == 0 && hasEquipmentsProperty)
            {
                for (int i = 0; i < equipments.Count; i++)
                {
                    var entry = equipments[i];

                    if (entry == null)
                        continue;

                    equipmentIds.Add(entry.Id);
                }
            }
            else if (equipments.Count == 0 && equipmentIds.Count != 0)
            {
                for (int i = 0; i < equipmentIds.Count; i++)
                {
                    var id = equipmentIds[i];

                    equipments.Add(new EquipmentSnapshot(id, 1));
                }
            }
        }

        private static void CheckFields(UnitSnapshot unitSnapshot)
        {
            unitSnapshot.Equipments ??= [];
            unitSnapshot.EquipmentIds ??= [];
            unitSnapshot.ArtifactIds ??= [];
            unitSnapshot.AspectIds ??= [];
            unitSnapshot.ActivePerkIds ??= [];
            unitSnapshot.ActiveSkillIds ??= [];
            unitSnapshot.ActiveStatusIds ??= [];
        }
    }
}
