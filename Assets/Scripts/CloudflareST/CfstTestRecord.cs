// UTF-8
using System;
using CloudflareST.Core;

namespace CloudflareST.Unity.UI
{
    /// <summary>
    /// 单次测速记录，用于历史面板和结果面板
    /// </summary>
    public class CfstTestRecord
    {
        public DateTime   Timestamp { get; set; }
        public TimeSpan   Duration  { get; set; }
        public string     Protocol  { get; set; } = string.Empty;
        public string     Summary   { get; set; } = string.Empty;
        public bool       Success   { get; set; }
        public string     BestIp    { get; set; } = string.Empty;
        public float      BestLatencyMs { get; set; }
        public float      BestSpeedMbps { get; set; }
        public TestConfig Config    { get; set; }
    }
}
