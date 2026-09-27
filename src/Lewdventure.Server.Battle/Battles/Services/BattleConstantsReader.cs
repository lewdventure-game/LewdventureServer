using System.Globalization;
using Server.Services;

namespace Server.Battles
{
    internal sealed class BattleConstantsReader : IBattleConstantsReader
    {
        private readonly IConfigDistributor _configDistributor;
        private readonly ICoreLog _coreLog;

        public BattleConstantsReader(IConfigDistributor configDistributor, ICoreLog coreLog)
        {
            _configDistributor = configDistributor;
            _coreLog = coreLog;
        }

        public bool TryGet(string constantKey, out float value)
        {
            value = 0f;

            if (_configDistributor.Constants.TryGet(constantKey, out var constant) == false)
                return false;

            if (float.TryParse(constant.ConstantValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value) == false)
            {
                _coreLog.Error($"[Story][Battle]: Constant parse failed, key = {constantKey} value = {constant.ConstantValue}");

                throw new InvalidOperationException($"[Story][Battle]: Constant parse failed, key = {constantKey}");
            }

            return true;
        }

        public float Get(string constantKey)
        {
            if (TryGet(constantKey, out var value) == false)
            {
                _coreLog.Error($"[Error][Story][Battle]: Constant missing key = {constantKey}");

                throw new InvalidOperationException($"[Error][Story][Battle]: Constant missing key = {constantKey}");
            }

            return value;
        }
    }
}
