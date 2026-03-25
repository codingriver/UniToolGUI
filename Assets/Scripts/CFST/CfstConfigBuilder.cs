// ============================================================
// CfstConfigBuilder.cs  —  将 GUI 的 CfstOptions 映射到
//                          cfst.dll 的 CloudflareST.Config
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CloudflareST.GUI
{
    public static class CfstConfigBuilder
    {
        /// <summary>
        /// 将 CfstOptions（GUI 数据结构）转换为 CloudflareST.Config（dll 内部配置）。
        /// </summary>
        public static CloudflareST.Config Build(CfstOptions o)
        {
            string baseDir = AppRuntimePaths.GetDesktopDataDir();
            var cfg = new CloudflareST.Config();

            // ── IP 来源 ──────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(o.IpRanges))
            {
                cfg.IpRanges = o.IpRanges.Trim();
            }
            else
            {
                cfg.IpFiles = new List<string>();
                if (!string.IsNullOrWhiteSpace(o.IPv4File))
                    cfg.IpFiles.Add(o.IPv4File.Trim());
                if (!string.IsNullOrWhiteSpace(o.IPv6File))
                    cfg.IpFiles.Add(o.IPv6File.Trim());
                if (cfg.IpFiles.Count == 0)
                {
                    cfg.IpFiles = new List<string>
                    {
                        Path.Combine(baseDir, "ip.txt"),
                        Path.Combine(baseDir, "ipv6.txt")
                    };
                }
            }

            cfg.MaxIpCount = o.IpLoadLimit;
            cfg.AllIp      = o.AllIp;

            // ── 延迟测速方式 ─────────────────────────────────
            switch (o.PingMode)
            {
                case PingMode.TcPing:
                    cfg.TcpPingMode = true;
                    break;
                case PingMode.Httping:
                    cfg.HttpingMode = true;
                    break;
                default: // IcmpAuto
                    cfg.ForceIcmp = o.ForceIcmp;
                    break;
            }

            cfg.PingThreads       = o.PingConcurrency;
            cfg.PingCount         = o.PingCount;
            cfg.DelayThresholdMs  = o.LatencyMax;
            cfg.DelayMinMs        = o.LatencyMin;
            cfg.LossRateThreshold = o.PacketLossMax;
            cfg.HttpingStatusCode = o.HttpingCode;
            cfg.CfColo            = o.CfColo;

            // ── 下载测速 ─────────────────────────────────────
            cfg.DisableSpeedTest       = o.DisableDownload;
            cfg.SpeedUrl               = o.DownloadUrl;
            cfg.Port                   = o.DownloadPort;
            cfg.SpeedNum               = o.DownloadCount;
            cfg.DownloadTimeoutSeconds = o.DownloadTimeout;
            // SpeedMinMbps 在 cfst 内部是 Mbps，GUI 的 SpeedMin 单位是 MB/s
            cfg.SpeedMinMbps           = o.SpeedMin * 8.0;

            // ── 输出 ─────────────────────────────────────────
            cfg.OutputFile = o.OutputFile ?? "result.csv";
            cfg.OutputNum  = o.OutputCount;
            cfg.OnlyIpFile = o.OnlyIpFile ?? "onlyip.txt";
            cfg.Debug      = o.Debug;
            // ShowProgress = true 使 ProgressReporter 发出 JSON 事件
            cfg.ShowProgress = true;

            // 定时调度由 Unity 侧 ScheduleManager 负责，不传给 cfst.dll

            // ── Hosts 更新 ───────────────────────────────────
            cfg.HostEntries   = BuildHostEntries(o);
            cfg.HostsFilePath = o.HostsFile;
            cfg.HostsDryRun   = o.HostsDryRun;

            return cfg;
        }

        private static List<CloudflareST.HostEntry> BuildHostEntries(CfstOptions o)
        {
            var list = new List<CloudflareST.HostEntry>();
            if (o.HostsDomains == null || o.HostsDomains.Count == 0) return list;

            foreach (var entry in o.HostsDomains)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Domain)) continue;
                list.Add(new CloudflareST.HostEntry
                {
                    Domain  = entry.Domain.Trim(),
                    IpIndex = entry.IpRank < 1 ? 1 : entry.IpRank
                });
            }

            return list;
        }
    }
}
