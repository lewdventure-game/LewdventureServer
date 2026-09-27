using System;
using Server.Logging;

namespace Core.Common
{
    internal sealed class UnityCoreLog : ICoreLog
    {
        public void Debug(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        public void Information(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        public void Warning(string message)
        {
            UnityEngine.Debug.LogWarning(message);
        }

        public void Warning(Exception exception, string message)
        {
            UnityEngine.Debug.LogWarning($"{message} {exception.Message}");
        }

        public void Error(string message)
        {
            UnityEngine.Debug.LogError(message);
        }

        public void Error(Exception exception, string message)
        {
            UnityEngine.Debug.LogError($"{message} {exception.Message}");
        }
    }
}
