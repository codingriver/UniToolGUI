using System;
using System.Collections.Generic;

namespace CloudflareST.GUI
{
    /// <summary>
    /// 单个 IP 的测速结果
    /// </summary>
    [Serializable]
    public class IPInfo
    {
        public int    Rank          { get; set; }
        public string IP            { get; set; }
        public float  PacketLoss    { get; set; }   // 0.0 ~ 1.0
        public float  AvgDelay      { get; set; }   // ms
        public float  DownloadSpeed { get; set; }   // MB/s
        public string DataCenter    { get; set; }
        public string Region        { get; set; }
    }

    /// <summary>
    /// 全局测速结果集
    /// </summary>
    public class TestResult
    {
        public static readonly TestResult Instance = new TestResult();
        private TestResult() { }

        public event Action OnResultUpdated;

        private readonly List<IPInfo> _ipList = new List<IPInfo>();
        public IReadOnlyList<IPInfo> IpList => _ipList;

        /// <summary>Hosts 写入后的回显内容（IP + 域名行列表）</summary>
        public List<string> HostsWriteLines { get; } = new List<string>();
        public bool HostsWriteSuccess { get; set; }
        public bool HostsPermissionDenied { get; set; }

        public void SetResults(List<IPInfo> list)
        {
            _ipList.Clear();
            if (list != null) _ipList.AddRange(list);
            OnResultUpdated?.Invoke();
        }

        public void Clear()
        {
            _ipList.Clear();
            HostsWriteLines.Clear();
            HostsWriteSuccess = false;
            HostsPermissionDenied = false;
            OnResultUpdated?.Invoke();
        }
    }
}
