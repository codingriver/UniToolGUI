using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace NativeKit
{
    internal static class MacHelperBridge
    {
        private delegate void NativeEventCallback(IntPtr jsonPtr);

        private static bool _initialized;
        private static NativeEventCallback _callbackRef;
        public static event Action<string> RawEventReceived;

#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
        [DllImport("UniToolXpcBridge")]
        private static extern int UniToolXpc_Connect();

        [DllImport("UniToolXpcBridge")]
        private static extern void UniToolXpc_Disconnect();

        [DllImport("UniToolXpcBridge")]
        private static extern int UniToolXpc_SendJson([MarshalAs(UnmanagedType.LPUTF8Str)] string json);

        [DllImport("UniToolXpcBridge")]
        private static extern void UniToolXpc_SetCallback(NativeEventCallback callback);
#endif

        public static void Initialize()
        {
            if (_initialized)
                return;

            _callbackRef = OnNativeEvent;
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            UniToolXpc_SetCallback(_callbackRef);
#endif
            _initialized = true;
        }

        public static bool Connect()
        {
            Initialize();
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            return UniToolXpc_Connect() != 0;
#else
            return false;
#endif
        }

        public static void Disconnect()
        {
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            UniToolXpc_Disconnect();
#endif
        }

        public static bool SendJson(string json)
        {
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            return UniToolXpc_SendJson(json ?? "{}") != 0;
#else
            return false;
#endif
        }

        private static void OnNativeEvent(IntPtr jsonPtr)
        {
            string json = jsonPtr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(jsonPtr);
            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                RawEventReceived?.Invoke(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MacHelperBridge] 回调处理失败: " + ex.Message);
            }
        }
    }
}
