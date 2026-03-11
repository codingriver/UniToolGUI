using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AIGate.UI
{
    /// <summary>
    /// 工具路径自定义配置管理器
    /// 存储位置：Application.persistentDataPath/gate_tool_paths.json
    ///
    /// 支持用户为每个工具指定：
    ///   - 可执行文件路径（覆盖自动检测）
    ///   - 配置文件路径（覆盖默认路径）
    /// </summary>
    public static class ToolPathConfig
    {
        private const string FileName = "gate_tool_paths.json";

        [Serializable]
        public class ToolPathEntry
        {
            public string toolName;
            public string executablePath;   // 可执行文件路径，空=自动检测
            public string configFilePath;   // 配置文件路径，空=默认路径
        }

        [Serializable]
        private class ToolPathStore
        {
            public List<ToolPathEntry> entries = new();
        }

        private static Dictionary<string, ToolPathEntry> _cache;

        private static string FilePath =>
            Path.Combine(Application.persistentDataPath, FileName);

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>获取工具的自定义路径配置，不存在则返回 null</summary>
        public static ToolPathEntry Get(string toolName)
        {
            EnsureLoaded();
            _cache.TryGetValue(toolName.ToLower(), out var entry);
            return entry;
        }

        /// <summary>获取工具的自定义可执行文件路径</summary>
        public static string GetExecutablePath(string toolName)
            => Get(toolName)?.executablePath;

        /// <summary>获取工具的自定义配置文件路径</summary>
        public static string GetConfigFilePath(string toolName)
            => Get(toolName)?.configFilePath;

        /// <summary>设置工具路径配置并持久化</summary>
        public static void Set(string toolName, string executablePath, string configFilePath)
        {
            EnsureLoaded();
            var key = toolName.ToLower();
            if (!_cache.TryGetValue(key, out var entry))
            {
                entry = new ToolPathEntry { toolName = toolName };
                _cache[key] = entry;
            }
            entry.executablePath = string.IsNullOrWhiteSpace(executablePath) ? null : executablePath.Trim();
            entry.configFilePath = string.IsNullOrWhiteSpace(configFilePath) ? null : configFilePath.Trim();
            Save();
        }

        /// <summary>清除工具的自定义路径（恢复自动检测）</summary>
        public static void Clear(string toolName)
        {
            EnsureLoaded();
            _cache.Remove(toolName.ToLower());
            Save();
        }

        /// <summary>获取所有已自定义路径的工具列表</summary>
        public static List<ToolPathEntry> GetAll()
        {
            EnsureLoaded();
            return new List<ToolPathEntry>(_cache.Values);
        }

        /// <summary>判断工具是否有自定义路径配置</summary>
        public static bool HasCustomPath(string toolName)
        {
            var e = Get(toolName);
            return e != null &&
                   (!string.IsNullOrEmpty(e.executablePath) ||
                    !string.IsNullOrEmpty(e.configFilePath));
        }

        // ── Internal ──────────────────────────────────────────────────────

        private static void EnsureLoaded()
        {
            if (_cache != null) return;
            Load();
        }

        private static void Load()
        {
            _cache = new Dictionary<string, ToolPathEntry>();
            try
            {
                if (!File.Exists(FilePath)) return;
                var json = File.ReadAllText(FilePath);
                var store = JsonUtility.FromJson<ToolPathStore>(json);
                if (store?.entries == null) return;
                foreach (var e in store.entries)
                    if (!string.IsNullOrEmpty(e?.toolName))
                        _cache[e.toolName.ToLower()] = e;
                Debug.Log($"[ToolPathConfig] Loaded {_cache.Count} custom tool paths.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ToolPathConfig] Failed to load: {ex.Message}");
            }
        }

        private static void Save()
        {
            try
            {
                var store = new ToolPathStore();
                store.entries.AddRange(_cache.Values);
                var json = JsonUtility.ToJson(store, prettyPrint: true);
                File.WriteAllText(FilePath, json);
                Debug.Log($"[ToolPathConfig] Saved {_cache.Count} entries to {FilePath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ToolPathConfig] Failed to save: {ex.Message}");
            }
        }
    }
}
