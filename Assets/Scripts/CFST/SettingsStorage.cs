using System;
using System.IO;
using UnityEngine;

namespace CloudflareST.GUI
{
    public static class SettingsStorage
    {
        private const string KEY_PREFIX = "cfst_";

        public static string GetDefaultBaseDir()
        {
#if UNITY_ANDROID || UNITY_IOS
            return Application.persistentDataPath;
#else
            return AppDomain.CurrentDomain.BaseDirectory;
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
            SetBool  ("silent",      o.Silent);
            SetString("onlyipfile",  o.OnlyIpFile);
            SetBool  ("debug",       o.Debug);
            SetInt   ("schedmode",   (int)o.ScheduleMode);
            SetInt   ("interval",    o.IntervalMinutes);
            SetString("dailyat",     o.DailyAt);
            SetString("cronexpr",    o.CronExpression);
            SetString("timezone",    o.TimeZone);
            SetString("hostsdomains",o.HostsDomains);
            SetInt   ("hostsiprank", o.HostsIpRank);
            SetString("hostsfile",   o.HostsFile);
            SetBool  ("hostsdryrun", o.HostsDryRun);
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
            o.Silent          = GetBool  ("silent",      false);
            o.OnlyIpFile      = GetString("onlyipfile",  GetDefaultOnlyIpFile());
            o.Debug           = GetBool  ("debug",       false);
            o.ScheduleMode    = (ScheduleMode)GetInt("schedmode", 0);
            o.IntervalMinutes = GetInt   ("interval",    0);
            o.DailyAt         = GetString("dailyat",     null);
            o.CronExpression  = GetString("cronexpr",    null);
            o.TimeZone        = GetString("timezone",    null);
            o.HostsDomains    = GetString("hostsdomains",null);
            o.HostsIpRank     = GetInt   ("hostsiprank", 1);
            o.HostsFile       = GetString("hostsfile",   null);
            o.HostsDryRun     = GetBool  ("hostsdryrun", false);
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
                "outputfile","outputcount","silent","onlyipfile","debug",
                "schedmode","interval","dailyat","cronexpr","timezone",
                "hostsdomains","hostsiprank","hostsfile","hostsdryrun"
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
            o.Silent          = false;
            o.OnlyIpFile      = GetDefaultOnlyIpFile();
            o.Debug           = false;
            o.ScheduleMode    = ScheduleMode.None;
            o.IntervalMinutes = 0;
            o.DailyAt         = null;
            o.CronExpression  = null;
            o.TimeZone        = null;
            o.HostsDomains    = null;
            o.HostsIpRank     = 1;
            o.HostsFile       = null;
            o.HostsDryRun     = false;
        }

        private static void SetString(string k, string v)
        {
            if (v == null) PlayerPrefs.DeleteKey(KEY_PREFIX + k);
            else           PlayerPrefs.SetString(KEY_PREFIX + k, v);
        }
        private static void SetInt  (string k, int v)   => PlayerPrefs.SetInt  (KEY_PREFIX + k, v);
        private static void SetBool (string k, bool v)  => PlayerPrefs.SetInt  (KEY_PREFIX + k, v ? 1 : 0);
        private static void SetFloat(string k, float v) => PlayerPrefs.SetFloat(KEY_PREFIX + k, v);

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
