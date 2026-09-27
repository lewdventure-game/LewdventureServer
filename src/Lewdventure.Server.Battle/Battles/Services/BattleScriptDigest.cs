using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Server.Battles
{
    internal sealed class BattleScriptDigest : IBattleScriptDigest
    {
        private const string DigestPrefix = "sha256:";
        private const string Header = "lewdventure-battle-script/v1\n";

        public string Compute(IBattleScriptResponse script)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            Append(hash, Header);
            AppendInt(hash, script.ProtocolVersion);
            AppendULong(hash, script.Seed);
            AppendInt(hash, (int)script.OutcomeType);
            AppendInt(hash, script.Steps.Count);

            for (int i = 0; i < script.Steps.Count; i++)
            {
                var step = script.Steps[i];

                AppendInt(hash, step.Index);
                AppendInt(hash, step.Turn);
                AppendInt(hash, (int)step.Phase);
                AppendInt(hash, step.ActorId);
                AppendInt(hash, step.ActorSlotIndex);
                AppendInt(hash, step.TargetId);
                AppendInt(hash, step.TargetSlotIndex);
                AppendInt(hash, step.Commands.Count);

                for (int j = 0; j < step.Commands.Count; j++)
                {
                    var command = step.Commands[j];

                    AppendInt(hash, (int)command.CommandType);
                    AppendToken(hash, command.Parameters);
                }
            }

            return DigestPrefix + ToLowerHex(hash.GetHashAndReset());
        }

        private void AppendToken(IncrementalHash hash, JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    Append(hash, "o");

                    foreach (var property in ((JObject)token).Properties())
                    {
                        Append(hash, property.Name);
                        Append(hash, "=");
                        AppendToken(hash, property.Value);
                        Append(hash, ";");
                    }

                    return;
                case JTokenType.Array:
                    var jsonArray = (JArray)token;

                    Append(hash, "a");
                    AppendInt(hash, jsonArray.Count);

                    for (int i = 0; i < jsonArray.Count; i++)
                        AppendToken(hash, jsonArray[i]);

                    return;
                case JTokenType.Integer:
                    Append(hash, "i");
                    AppendLong(hash, Convert.ToInt64(((JValue)token).Value, CultureInfo.InvariantCulture));

                    return;
                case JTokenType.Float:
                    Append(hash, "f");
                    AppendFloat(hash, (float)Convert.ToDouble(((JValue)token).Value, CultureInfo.InvariantCulture));

                    return;
                case JTokenType.Boolean:
                    Append(hash, Convert.ToBoolean(((JValue)token).Value, CultureInfo.InvariantCulture) ? "b1" : "b0");

                    return;
                case JTokenType.String:
                    Append(hash, "s");
                    Append(hash, ReadString(token));

                    return;
                case JTokenType.Null:
                case JTokenType.Undefined:
                    Append(hash, "n");

                    return;
                default:
                    Append(hash, "x");
                    AppendInt(hash, (int)token.Type);

                    return;
            }
        }

        private string ReadString(JToken token)
        {
            var value = ((JValue)token).Value;

            return value == null ? string.Empty : value.ToString()!;
        }

        private void AppendFloat(IncrementalHash hash, float value)
        {
            Append(hash, BitConverter.SingleToInt32Bits(value).ToString("x8", CultureInfo.InvariantCulture));
        }

        private void AppendInt(IncrementalHash hash, int value)
        {
            Append(hash, value.ToString("x8", CultureInfo.InvariantCulture));
        }

        private void AppendLong(IncrementalHash hash, long value)
        {
            Append(hash, value.ToString("x16", CultureInfo.InvariantCulture));
        }

        private void AppendULong(IncrementalHash hash, ulong value)
        {
            Append(hash, value.ToString("x16", CultureInfo.InvariantCulture));
        }

        private void Append(IncrementalHash hash, string value)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(value));
        }

        private string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);

            for (int i = 0; i < bytes.Length; i++)
                builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));

            return builder.ToString();
        }
    }
}
