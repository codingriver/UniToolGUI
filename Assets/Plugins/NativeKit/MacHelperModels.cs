using System;
using UnityEngine;

namespace NativeKit
{
    [Serializable]
    public class MacHelperRequest
    {
        public string requestId;
        public string action;
        public string payload;
        public string token;
        public int timeoutSec = 60;
        public string source;

        public string RequestId { get => requestId; set => requestId = value; }
        public string Action { get => action; set => action = value; }
        public string PayloadJson { get => payload; set => payload = value; }
        public string Token { get => token; set => token = value; }
        public int TimeoutSec { get => timeoutSec; set => timeoutSec = value; }
        public string Source { get => source; set => source = value; }

        public string ToJson()
        {
            if (string.IsNullOrEmpty(requestId))
                requestId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrEmpty(payload))
                payload = "{}";
            return JsonUtility.ToJson(this);
        }
    }

    [Serializable]
    public class MacHelperEvent
    {
        public string requestId;
        public string action;
        public string eventType;
        public bool ok;
        public int exitCode;
        public string message;
        public string payloadJson;

        public string RequestId => requestId;
        public string Action => action;
        public string EventType => eventType;
        public bool Ok => ok;
        public int ExitCode => exitCode;
        public string Message => message;
        public string PayloadJson => payloadJson;

        public static MacHelperEvent FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonUtility.FromJson<MacHelperEvent>(json);
            }
            catch
            {
                return new MacHelperEvent
                {
                    requestId = string.Empty,
                    action = string.Empty,
                    eventType = "parse_error",
                    ok = false,
                    exitCode = -1,
                    message = json,
                    payloadJson = "{}"
                };
            }
        }
    }

    [Serializable]
    public class MacHelperStatus
    {
        public bool isInstalled;
        public bool isConnected;
        public string helperBinaryPath;
        public string launchDaemonPath;
        public string trustFilePath;
        public string logDirectory;
        public string packageDirectory;
        public string message;
    }

    [Serializable]
    public class MacShellExecPayload
    {
        public string command;
    }

    [Serializable]
    public class MacHostsUpdatePayload
    {
        public string targetPath;
        public string content;
    }

    [Serializable]
    public class MacTrustRefreshPayload
    {
        public string appExe;
        public string appSha256;
        public string token;
    }
}
