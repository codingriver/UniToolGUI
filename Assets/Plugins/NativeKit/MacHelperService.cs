using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace NativeKit
{
    public static class MacHelperService
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, Action<MacHelperEvent>> PendingRequests =
            new Dictionary<string, Action<MacHelperEvent>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Action<MacHelperEvent>> ActionCallbacks =
            new Dictionary<string, Action<MacHelperEvent>>(StringComparer.Ordinal);

        private static bool _initialized;
        private static bool _connected;

        public static event Action<MacHelperEvent> OnEvent;

        public static void Initialize()
        {
            if (_initialized)
                return;

            MacHelperBridge.Initialize();
            MacHelperBridge.RawEventReceived += HandleRawEvent;
            _initialized = true;
        }

        public static bool Connect()
        {
            Initialize();
            _connected = MacHelperBridge.Connect();
            if (_connected)
                FileLogger.Log("[MacHelper] XPC 连接已建立");
            else
                FileLogger.LogWarning("[MacHelper] XPC 连接建立失败");
            try
            {
                Debug.Log(_connected ? "[MacHelper] XPC 连接已建立" : "[MacHelper] XPC 连接建立失败");
            }
            catch { }
            return _connected;
        }

        public static void Disconnect()
        {
            if (!_initialized)
                return;
            MacHelperBridge.Disconnect();
            _connected = false;
            try
            {
                FileLogger.Log("[MacHelper] XPC 连接已断开");
                Debug.Log("[MacHelper] XPC 连接已断开");
            }
            catch { }
        }

        public static bool IsConnected => _connected;

        public static void RegisterActionCallback(string action, Action<MacHelperEvent> callback)
        {
            if (string.IsNullOrWhiteSpace(action))
                return;

            lock (SyncRoot)
            {
                if (callback == null)
                    ActionCallbacks.Remove(action);
                else
                    ActionCallbacks[action] = callback;
            }
        }

        public static void UnregisterActionCallback(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return;

            lock (SyncRoot)
                ActionCallbacks.Remove(action);
        }

        public static string Submit(string action, string payloadJson = "{}", int timeoutSec = 60, string source = null, Action<MacHelperEvent> onPerEvent = null)
        {
            var request = new MacHelperRequest
            {
                requestId = Guid.NewGuid().ToString("N"),
                action = action,
                payload = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
                timeoutSec = timeoutSec <= 0 ? 60 : timeoutSec,
                source = string.IsNullOrWhiteSpace(source) ? Application.productName : source,
                token = MacHelperInstallService.TrustToken
            };

            if (onPerEvent != null)
            {
                lock (SyncRoot)
                    PendingRequests[request.requestId] = onPerEvent;
            }

            Submit(request);
            return request.requestId;
        }

        public static string Submit(MacHelperRequest request)
        {
            Initialize();
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.requestId))
                request.requestId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(request.payload))
                request.payload = "{}";
            if (request.timeoutSec <= 0)
                request.timeoutSec = 60;
            if (string.IsNullOrWhiteSpace(request.source))
                request.source = Application.productName;
            if (string.IsNullOrWhiteSpace(request.token))
                request.token = MacHelperInstallService.TrustToken;

            if (!Connect())
                throw new InvalidOperationException("无法连接 macOS Root Helper");

            try
            {
                FileLogger.Log("[MacHelper] Submit action=" + request.action
                               + " requestId=" + request.requestId
                                + " timeoutSec=" + request.timeoutSec
                               + " token=" + (request.token ?? "")
                               + " payload=" + (request.payload ?? ""));
                Debug.Log("[MacHelper] Submit action=" + request.action
                          + " requestId=" + request.requestId
                          + " timeoutSec=" + request.timeoutSec
                          + " token=" + (request.token ?? "")
                          + " payload=" + (request.payload ?? ""));
            }
            catch { }

            string json = request.ToJson();
            if (!MacHelperBridge.SendJson(json))
            {
                try
                {
                    FileLogger.LogWarning("[MacHelper] SendJson failed action=" + request.action
                                          + " requestId=" + request.requestId);
                    Debug.LogWarning("[MacHelper] SendJson failed action=" + request.action
                                     + " requestId=" + request.requestId);
                }
                catch { }
                throw new InvalidOperationException("Root Helper 请求发送失败");
            }

            return request.requestId;
        }

        public static bool SubmitAndWait(MacHelperRequest request, out MacHelperEvent finalEvent, int timeoutMs = 65000)
        {
            finalEvent = null;
            if (string.IsNullOrWhiteSpace(request.requestId))
                request.requestId = Guid.NewGuid().ToString("N");

            MacHelperEvent capturedFinalEvent = null;
            using (var wait = new ManualResetEventSlim(false))
            {
                Action<MacHelperEvent> handler = evt =>
                {
                    if (evt == null || evt.RequestId != request.requestId)
                        return;
                    if (evt.EventType == "completed" || evt.EventType == "failed")
                    {
                        capturedFinalEvent = evt;
                        wait.Set();
                    }
                };

                lock (SyncRoot)
                    PendingRequests[request.requestId] = handler;

                try
                {
                    try
                    {
                        FileLogger.Log("[MacHelper] SubmitAndWait start action=" + request.action
                                       + " requestId=" + request.requestId
                                       + " timeoutMs=" + timeoutMs);
                        Debug.Log("[MacHelper] SubmitAndWait start action=" + request.action
                                  + " requestId=" + request.requestId
                                  + " timeoutMs=" + timeoutMs);
                    }
                    catch { }

                    Submit(request);
                    if (!wait.Wait(timeoutMs))
                    {
                        capturedFinalEvent = new MacHelperEvent
                        {
                            requestId = request.requestId,
                            action = request.action,
                            eventType = "failed",
                            ok = false,
                            exitCode = 124,
                            message = "等待 Root Helper 响应超时",
                            payloadJson = "{}"
                        };
                        try
                        {
                            FileLogger.LogWarning("[MacHelper] SubmitAndWait timeout action=" + request.action
                                                  + " requestId=" + request.requestId);
                            Debug.LogWarning("[MacHelper] SubmitAndWait timeout action=" + request.action
                                             + " requestId=" + request.requestId);
                        }
                        catch { }
                        finalEvent = capturedFinalEvent;
                        return false;
                    }

                    finalEvent = capturedFinalEvent;
                    try
                    {
                        FileLogger.Log("[MacHelper] SubmitAndWait done action=" + request.action
                                       + " requestId=" + request.requestId
                                       + " ok=" + (finalEvent != null && finalEvent.Ok));
                        Debug.Log("[MacHelper] SubmitAndWait done action=" + request.action
                                  + " requestId=" + request.requestId
                                  + " ok=" + (finalEvent != null && finalEvent.Ok));
                    }
                    catch { }
                    return finalEvent != null && finalEvent.Ok;
                }
                finally
                {
                    lock (SyncRoot)
                        PendingRequests.Remove(request.requestId);
                }
            }
        }

        public static bool SubmitHostsUpdate(string targetPath, string content, Action<string> log, out string errorMessage)
        {
            errorMessage = null;
            var payload = new MacHostsUpdatePayload
            {
                targetPath = targetPath,
                content = content
            };
            var request = new MacHelperRequest
            {
                requestId = Guid.NewGuid().ToString("N"),
                action = "hosts.update",
                payload = JsonUtility.ToJson(payload),
                timeoutSec = 60,
                source = Application.productName
            };

            if (!SubmitAndWait(request, out var result))
            {
                errorMessage = result?.Message ?? "Root Helper 无响应";
                log?.Invoke("[MacHelper] hosts.update 失败: " + errorMessage);
                return false;
            }

            log?.Invoke("[MacHelper] hosts.update 成功");
            return true;
        }

        public static bool QueryStatus(out MacHelperEvent statusEvent, out string errorMessage)
        {
            statusEvent = null;
            errorMessage = null;

            var request = new MacHelperRequest
            {
                requestId = Guid.NewGuid().ToString("N"),
                action = "helper.status",
                payload = "{}",
                timeoutSec = 15,
                source = Application.productName
            };

            if (!SubmitAndWait(request, out statusEvent, 20000))
            {
                errorMessage = statusEvent?.Message ?? "helper.status 无响应";
                try
                {
                    FileLogger.LogWarning("[MacHelper] helper.status failed: " + errorMessage
                                          + " exit=" + (statusEvent?.ExitCode ?? -1)
                                          + " payload=" + (statusEvent?.PayloadJson ?? ""));
                    Debug.LogWarning("[MacHelper] helper.status failed: " + errorMessage
                                     + " exit=" + (statusEvent?.ExitCode ?? -1)
                                     + " payload=" + (statusEvent?.PayloadJson ?? ""));
                }
                catch { }
                return false;
            }

            try
            {
                FileLogger.Log("[MacHelper] helper.status ok: " + (statusEvent?.Message ?? "")
                               + " exit=" + (statusEvent?.ExitCode ?? 0)
                               + " payload=" + (statusEvent?.PayloadJson ?? ""));
                Debug.Log("[MacHelper] helper.status ok: " + (statusEvent?.Message ?? "")
                          + " exit=" + (statusEvent?.ExitCode ?? 0)
                          + " payload=" + (statusEvent?.PayloadJson ?? ""));
            }
            catch { }
            return true;
        }

        public static bool Ping(out MacHelperEvent pingEvent, out string errorMessage)
        {
            pingEvent = null;
            errorMessage = null;

            var request = new MacHelperRequest
            {
                requestId = Guid.NewGuid().ToString("N"),
                action = "helper.ping",
                payload = "{}",
                timeoutSec = 10,
                source = Application.productName
            };

            if (!SubmitAndWait(request, out pingEvent, 15000))
            {
                errorMessage = pingEvent?.Message ?? "helper.ping 无响应";
                try
                {
                    FileLogger.LogWarning("[MacHelper] helper.ping failed: " + errorMessage
                                          + " exit=" + (pingEvent?.ExitCode ?? -1)
                                          + " payload=" + (pingEvent?.PayloadJson ?? ""));
                    Debug.LogWarning("[MacHelper] helper.ping failed: " + errorMessage
                                     + " exit=" + (pingEvent?.ExitCode ?? -1)
                                     + " payload=" + (pingEvent?.PayloadJson ?? ""));
                }
                catch { }
                return false;
            }

            try
            {
                FileLogger.Log("[MacHelper] helper.ping ok: " + (pingEvent?.Message ?? "")
                               + " exit=" + (pingEvent?.ExitCode ?? 0)
                               + " payload=" + (pingEvent?.PayloadJson ?? ""));
                Debug.Log("[MacHelper] helper.ping ok: " + (pingEvent?.Message ?? "")
                          + " exit=" + (pingEvent?.ExitCode ?? 0)
                          + " payload=" + (pingEvent?.PayloadJson ?? ""));
            }
            catch { }
            return true;
        }

        public static string SubmitShellCommand(string command, Action<MacHelperEvent> onPerEvent = null)
        {
            return Submit(
                "shell.exec",
                JsonUtility.ToJson(new MacShellExecPayload { command = command }),
                60,
                Application.productName,
                onPerEvent);
        }

        private static void HandleRawEvent(string json)
        {
            var evt = MacHelperEvent.FromJson(json);
            if (evt == null)
                return;

            try
            {
                string line = "[MacHelperEvent]"
                    + " action=" + (evt.Action ?? "")
                    + " type=" + (evt.EventType ?? "")
                    + " ok=" + evt.Ok
                    + " exitCode=" + evt.ExitCode
                    + " requestId=" + (evt.RequestId ?? "")
                    + " message=" + (evt.Message ?? "")
                    + " payload=" + (evt.PayloadJson ?? "");
                FileLogger.Log(line);
                Debug.Log(line);
            }
            catch
            {
                // ignore logging errors to avoid breaking event flow
            }

            Action<MacHelperEvent> requestCallback = null;
            Action<MacHelperEvent> actionCallback = null;

            lock (SyncRoot)
            {
                if (!string.IsNullOrEmpty(evt.RequestId) && PendingRequests.TryGetValue(evt.RequestId, out requestCallback))
                {
                    if (evt.EventType == "completed" || evt.EventType == "failed")
                        PendingRequests.Remove(evt.RequestId);
                }

                if (!string.IsNullOrEmpty(evt.Action))
                    ActionCallbacks.TryGetValue(evt.Action, out actionCallback);
            }

            if (requestCallback != null)
            {
                try { requestCallback(evt); } catch { }
            }

            if (actionCallback != null)
            {
                try { actionCallback(evt); } catch { }
            }

            if (evt.EventType == "connection_opened")
                _connected = true;
            else if (evt.EventType == "connection_closed" || evt.EventType == "connection_error")
                _connected = false;

            UnityMainThreadDispatcher.Enqueue(() =>
            {
                try { OnEvent?.Invoke(evt); } catch (Exception ex) { Debug.LogException(ex); }
            });
        }
    }
}
