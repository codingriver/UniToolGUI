using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace CloudflareST.GUI
{
    public static class SettingsStorage
    {
        private const string KEY_PREFIX = "cfst_";

        [Serializable]
        private class HostsEntryDto
        {
            public string domain;
            public int rank;
        }

        public static string GetDefaultBaseDir()
        {
#if UNITY_ANDROID || UNITY_IOS
            return Application.persistentDataPath;
#else
            return Environment.CurrentDirectory;
#endif
        }

        public static string GetDefaultIpv4File()   => Path.Combine(GetDefaultBaseDir(), "ip.txt");
        public static string GetDefaultIpv6File()   => Path.Combine(GetDefaultBaseDir(), "ipv6.txt");
        public static string GetDefaultOutputFile() => Path.Combine(GetDefaultBaseDir(), "result.csv");
        public static string GetDefaultOnlyIpFile() => Path.Combine(GetDefaultBaseDir(), "onlyip.txt");

        public static bool IsMobilePlatform()
        {
#if UNITY_ANDROID || UNITY_IOS
            return true;
#else
            return false;
#endif
        }

        public static void Save(CfstOptions o)
        {
            SetString("ipv4file",    o.IPv4File);
            SetString("ipv6file",    o.IPv6File);
            SetString("ipranges",    o.IpRanges);
            SetInt   ("iploadlimit", o.IpLoadLimit);
            SetBool  ("allip",       o.AllIp);
            SetInt   ("pingmode",    (int)o.PingMode);
            SetBool  ("forceicmp",   o.ForceIcmp);
            SetInt   ("pingconc",    o.PingConcurrency);
            SetInt   ("pingcount",   o.PingCount);
            SetInt   ("latmax",      o.LatencyMax);
            SetInt   ("latmin",      o.LatencyMin);
            SetFloat ("lossmx",      (float)o.PacketLossMax);
            SetInt   ("httpcode",    o.HttpingCode);
            SetString("cfcolo",      o.CfColo);
            SetBool  ("nodown",      o.DisableDownload);
            SetString("downurl",     o.DownloadUrl);
            SetInt   ("downport",    o.DownloadPort);
            SetInt   ("downcount",   o.DownloadCount);
            SetInt   ("downtimeout", o.DownloadTimeout);
            SetFloat ("speedmin",    (float)o.SpeedMin);
            SetString("outputfile",  o.OutputFile);
            SetInt   ("outputcount", o.OutputCount);
            SetString("onlyipfile",  o.OnlyIpFile);
            SetBool  ("debug",       o.Debug);
            SetBool  ("schedenabled", o.ScheduleEnabled);
            SetString("cronexpr",    o.CronExpression);
            SetBool  ("logtofile",   o.LogToFile);
            SetString("hostsdomains", SerializeHostsDomains(o.HostsDomains));
            PlayerPrefs.DeleteKey(KEY_PREFIX + "hostsiprank");
            PlayerPrefs.DeleteKey(KEY_PREFIX + "hostsentriesjson");
            SetString("hostsfile",   o.HostsFile);
            SetBool  ("hostsdryrun", o.HostsDryRun);
            // ── 前钩子 ──
            SetBool  ("pre_enabled", o.PreHookEnabled);
            SetString("pre_path",    o.PreHookPath);
            SetString("pre_args",    o.PreHookArgs);
            SetInt   ("pre_timeout", o.PreHookTimeoutSec);
            SetBool  ("pre_wait",    o.PreHookWait);
            // ── 后钩子 ──
            SetBool  ("post_enabled",     o.PostHookEnabled);
            SetString("post_path",        o.PostHookPath);
            SetString("post_args",        o.PostHookArgs);
            SetInt   ("post_timeout",     o.PostHookTimeoutSec);
            SetBool  ("post_onlysuccess", o.PostHookOnlySuccess);
            PlayerPrefs.Save();
            Debug.Log("[Settings] Saved");
        }

        public static void Load(CfstOptions o)
        {
            o.IPv4File        = GetString("ipv4file",    GetDefaultIpv4File());
            o.IPv6File        = GetString("ipv6file",    GetDefaultIpv6File());
            o.IpRanges        = GetString("ipranges",    null);
            o.IpLoadLimit     = GetInt   ("iploadlimit", 0);
            o.AllIp           = GetBool  ("allip",       false);
            o.PingMode        = (PingMode)GetInt("pingmode", 0);
            o.ForceIcmp       = GetBool  ("forceicmp",   false);
            o.PingConcurrency = GetInt   ("pingconc",    200);
            o.PingCount       = GetInt   ("pingcount",   4);
            o.LatencyMax      = GetInt   ("latmax",      9999);
            o.LatencyMin      = GetInt   ("latmin",      0);
            o.PacketLossMax   = GetFloat ("lossmx",      1.0f);
            o.HttpingCode     = GetInt   ("httpcode",    0);
            o.CfColo          = GetString("cfcolo",      null);
            o.DisableDownload = GetBool  ("nodown",      false);
            o.DownloadUrl     = GetString("downurl",     "https://speed.cloudflare.com/__down?bytes=52428800");
            o.DownloadPort    = GetInt   ("downport",    443);
            o.DownloadCount   = GetInt   ("downcount",   10);
            o.DownloadTimeout = GetInt   ("downtimeout", 10);
            o.SpeedMin        = GetFloat ("speedmin",    0f);
            o.OutputFile      = GetString("outputfile",  GetDefaultOutputFile());
            o.OutputCount     = GetInt   ("outputcount", 10);
            o.OnlyIpFile      = GetString("onlyipfile",  GetDefaultOnlyIpFile());
            o.Debug           = GetBool  ("debug",       false);
            o.ScheduleEnabled = GetBool  ("schedenabled", false);
            o.CronExpression  = GetString("cronexpr",    null);
            o.LogToFile       = GetBool  ("logtofile",   false);
            o.HostsDomains    = LoadHostsDomains();
            o.HostsFile       = GetString("hostsfile",   null);
            o.HostsDryRun     = GetBool  ("hostsdryrun", false);
            // ── 前钩子 —— 向后兼容：旧存档迁移 pre_script/pre_program -> pre_path ──
            o.PreHookEnabled    = GetBool  ("pre_enabled", false);
            o.PreHookPath       = GetString("pre_path",    null)
                               ?? GetString("pre_program", null)
                               ?? GetString("pre_script",  null);
            o.PreHookArgs       = GetString("pre_args",    null)
                               ?? GetString("pre_programargs", null);
            o.PreHookTimeoutSec = GetInt   ("pre_timeout", 30);
            o.PreHookWait       = GetBool  ("pre_wait",    true);
            // ── 后钩子 —— 向后兼容迁移 ──
            o.PostHookEnabled    = GetBool  ("post_enabled",     false);
            o.PostHookPath       = GetString("post_path",        null)
                                ?? GetString("post_program",     null)
                                ?? GetString("post_script",      null);
            o.PostHookArgs       = GetString("post_args",        null)
                                ?? GetString("post_programargs", null);
            o.PostHookTimeoutSec = GetInt   ("post_timeout",     30);
            o.PostHookOnlySuccess= GetBool  ("post_onlysuccess", false);
#if UNITY_ANDROID || UNITY_IOS
            o.IPv4File   = GetDefaultIpv4File();
            o.IPv6File   = GetDefaultIpv6File();
            o.OutputFile = GetDefaultOutputFile();
            o.OnlyIpFile = GetDefaultOnlyIpFile();
#endif
            Debug.Log("[Settings] Loaded");
        }

        public static void ResetAll(CfstOptions o)
        {
            string[] keys = {
                "ipv4file","ipv6file","ipranges","iploadlimit","allip",
                "pingmode","forceicmp","pingconc","pingcount","latmax","latmin",
                "lossmx","httpcode","cfcolo",
                "nodown","downurl","downport","downcount","downtimeout","speedmin",
                "outputfile","outputcount","onlyipfile","debug",
                "schedmode","interval","dailyat","cronexpr","timezone",
                "hostsdomains","hostsiprank","hostsentriesjson","hostsfile","hostsdryrun",
                "pre_enabled","pre_path","pre_args","pre_timeout","pre_wait",
                "post_enabled","post_path","post_args","post_timeout","post_onlysuccess"
            };
            foreach (var k in keys)
                PlayerPrefs.DeleteKey(KEY_PREFIX + k);
            PlayerPrefs.Save();
            ResetToDefaults(o);
            Debug.Log("[Settings] Reset to defaults");
        }

        public static void ResetToDefaults(CfstOptions o)
        {
            o.IPv4File        = GetDefaultIpv4File();
            o.IPv6File        = GetDefaultIpv6File();
            o.IpRanges        = null;
            o.IpLoadLimit     = 0;
            o.AllIp           = false;
            o.PingMode        = PingMode.IcmpAuto;
            o.ForceIcmp       = false;
            o.PingConcurrency = 200;
            o.PingCount       = 4;
            o.LatencyMax      = 9999;
            o.LatencyMin      = 0;
            o.PacketLossMax   = 1.0;
            o.HttpingCode     = 0;
            o.CfColo          = null;
            o.DisableDownload = false;
            o.DownloadUrl     = "https://speed.cloudflare.com/__down?bytes=52428800";
            o.DownloadPort    = 443;
            o.DownloadCount   = 10;
            o.DownloadTimeout = 10;
            o.SpeedMin        = 0;
            o.OutputFile      = GetDefaultOutputFile();
            o.OutputCount     = 10;
            o.OnlyIpFile      = GetDefaultOnlyIpFile();
            o.Debug           = false;
            o.ScheduleEnabled = false;
            o.CronExpression  = null;
            o.LogToFile       = false;
            o.HostsDomains    = new List<HostDomainEntry>();
            o.HostsFile       = null;
            o.HostsDryRun     = false;
            // ── 前钩子 ──
            o.PreHookEnabled    = false;
            o.PreHookPath       = null;
            o.PreHookArgs       = null;
            o.PreHookTimeoutSec = 30;
            o.PreHookWait       = true;
            // ── 后钩子 ──
            o.PostHookEnabled    = false;
            o.PostHookPath       = null;
            o.PostHookArgs       = null;
            o.PostHookTimeoutSec = 30;
            o.PostHookOnlySuccess= false;
        }

        private static void SetString(string k, string v)
        {
            if (v == null) PlayerPrefs.DeleteKey(KEY_PREFIX + k);
            else           PlayerPrefs.SetString(KEY_PREFIX + k, v);
        }
        private static void SetInt  (string k, int v)   => PlayerPrefs.SetInt  (KEY_PREFIX + k, v);
        private static void SetBool (string k, bool v)  => PlayerPrefs.SetInt  (KEY_PREFIX + k, v ? 1 : 0);
        private static void SetFloat(string k, float v) => PlayerPrefs.SetFloat(KEY_PREFIX + k, v);

        private static string SerializeHostsDomains(List<HostDomainEntry> entries)
        {
            if (entries == null || entries.Count == 0) return null;

            var list = new List<HostsEntryDto>();
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Domain)) continue;
                list.Add(new HostsEntryDto
                {
                    domain = entry.Domain.Trim(),
                    rank = entry.IpRank < 1 ? 1 : entry.IpRank
                });
            }

            if (list.Count == 0) return null;
            var wrapper = new HostsEntriesWrapper { items = list.ToArray() };
            return JsonUtility.ToJson(wrapper);
        }

        private static List<HostDomainEntry> DeserializeHostsDomains(string json)
        {
            var list = new List<HostDomainEntry>();
            if (string.IsNullOrWhiteSpace(json)) return list;

            try
            {
                var wrapper = JsonUtility.FromJson<HostsEntriesWrapper>(json);
                var items = wrapper?.items;
                if (items == null) return list;

                foreach (var item in items)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.domain)) continue;
                    list.Add(new HostDomainEntry
                    {
                        Domain = item.domain.Trim(),
                        IpRank = item.rank < 1 ? 1 : item.rank
                    });
                }
            }
            catch { }

            return list;
        }

        private static List<HostDomainEntry> LoadHostsDomains()
        {
            var storedJson = GetString("hostsdomains", null);
            var hostsDomains = DeserializeHostsDomains(storedJson);
            if (hostsDomains.Count > 0)
                return hostsDomains;

            var legacyEntries = DeserializeLegacyHostsEntries(GetString("hostsentriesjson", null));
            if (legacyEntries.Count > 0)
                return legacyEntries;

            if (!string.IsNullOrWhiteSpace(storedJson) && !storedJson.TrimStart().StartsWith("{"))
            {
                var legacyRank = GetInt("hostsiprank", 1);
                foreach (var domain in storedJson.Split(','))
                {
                    var trimmed = domain.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    hostsDomains.Add(new HostDomainEntry
                    {
                        Domain = trimmed,
                        IpRank = legacyRank < 1 ? 1 : legacyRank
                    });
                }
            }

            return hostsDomains;
        }

        private static List<HostDomainEntry> DeserializeLegacyHostsEntries(string json)
        {
            var list = new List<HostDomainEntry>();
            if (string.IsNullOrWhiteSpace(json)) return list;

            try
            {
                var wrapper = JsonUtility.FromJson<HostsEntriesWrapper>(WrapLegacyJsonArray(json));
                var items = wrapper?.items;
                if (items == null) return list;

                foreach (var item in items)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.domain)) continue;
                    list.Add(new HostDomainEntry
                    {
                        Domain = item.domain.Trim(),
                        IpRank = item.rank < 1 ? 1 : item.rank
                    });
                }
            }
            catch { }

            return list;
        }

        [Serializable]
        private class HostsEntriesWrapper
        {
            public HostsEntryDto[] items;
        }

        private static string WrapLegacyJsonArray(string json)
        {
            return "{\"items\":" + json + "}";
        }

        private static string GetString(string k, string def) =>
            PlayerPrefs.HasKey(KEY_PREFIX + k) ? PlayerPrefs.GetString(KEY_PREFIX + k) : def;
        private static int   GetInt  (string k, int def)   =>
            PlayerPrefs.HasKey(KEY_PREFIX + k) ? PlayerPrefs.GetInt   (KEY_PREFIX + k) : def;
        private static bool  GetBool (string k, bool def)  =>
            PlayerPrefs.HasKey(KEY_PREFIX + k) ? PlayerPrefs.GetInt   (KEY_PREFIX + k) == 1 : def;
        private static float GetFloat(string k, float def) =>
            PlayerPrefs.HasKey(KEY_PREFIX + k) ? PlayerPrefs.GetFloat (KEY_PREFIX + k) : def;
    }
}
