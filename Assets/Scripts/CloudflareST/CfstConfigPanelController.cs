// UTF-8
using CloudflareST.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.Unity.UI
{
    /// <summary>
    /// 配置面板控制器，对应 CfstConfigPanel.uxml
    /// 读写 TestConfig，通过 PlayerPrefs 持久化
    /// </summary>
    public class CfstConfigPanelController
    {
        private readonly VisualElement _root;

        private Toggle    _useIcmp;
        private Toggle    _useTcping;
        private Toggle    _useHttping;
        private TextField _concurrency;
        private TextField _runsPerIp;
        private TextField _ipLimit;
        private TextField _url;
        private TextField _tp;
        private TextField _ipFile;
        private Toggle    _useIpv6;
        private TextField _ipv6File;
        private Toggle    _downloadEnabled;
        private TextField _dn;
        private TextField _dt;
        private TextField _sl;
        private TextField _outputFile;
        private TextField _outputLimit;
        private Toggle    _silent;
        private Toggle    _debug;
        private TextField _hostsExpr;
        private Toggle    _hostsDryRun;
        private Label     _feedback;

        public CfstConfigPanelController(VisualElement root)
        {
            _root = root;
            BindElements();
            LoadFromPrefs();
        }

        public void Refresh() => LoadFromPrefs();

        private void BindElements()
        {
            _useIcmp         = _root.Q<Toggle>("cfg-use-icmp");
            _useTcping       = _root.Q<Toggle>("cfg-use-tcping");
            _useHttping      = _root.Q<Toggle>("cfg-use-httping");
            _concurrency     = _root.Q<TextField>("cfg-concurrency");
            _runsPerIp       = _root.Q<TextField>("cfg-runs-per-ip");
            _ipLimit         = _root.Q<TextField>("cfg-ip-limit");
            _url             = _root.Q<TextField>("cfg-url");
            _tp              = _root.Q<TextField>("cfg-tp");
            _ipFile          = _root.Q<TextField>("cfg-ip-file");
            _useIpv6         = _root.Q<Toggle>("cfg-use-ipv6");
            _ipv6File        = _root.Q<TextField>("cfg-ipv6-file");
            _downloadEnabled = _root.Q<Toggle>("cfg-download-enabled");
            _dn              = _root.Q<TextField>("cfg-dn");
            _dt              = _root.Q<TextField>("cfg-dt");
            _sl              = _root.Q<TextField>("cfg-sl");
            _outputFile      = _root.Q<TextField>("cfg-output-file");
            _outputLimit     = _root.Q<TextField>("cfg-output-limit");
            _silent          = _root.Q<Toggle>("cfg-silent");
            _debug           = _root.Q<Toggle>("cfg-debug");
            _hostsExpr       = _root.Q<TextField>("cfg-hosts-expr");
            _hostsDryRun     = _root.Q<Toggle>("cfg-hosts-dry-run");
            _feedback        = _root.Q<Label>("cfg-feedback");

            // Protocol mutual exclusion
            _useTcping?.RegisterValueChangedCallback(e  => { if (e.newValue) SetProtocol("tcping"); });
            _useHttping?.RegisterValueChangedCallback(e => { if (e.newValue) SetProtocol("httping"); });
            _useIcmp?.RegisterValueChangedCallback(e    => { if (e.newValue) SetProtocol("icmp"); });

            // Auto-save on any change
            RegisterAutoSave();
        }

        private void SetProtocol(string proto)
        {
            if (_useIcmp   != null) _useIcmp.SetValueWithoutNotify(proto == "icmp");
            if (_useTcping != null) _useTcping.SetValueWithoutNotify(proto == "tcping");
            if (_useHttping!= null) _useHttping.SetValueWithoutNotify(proto == "httping");
            SaveToPrefs();
        }

        private void RegisterAutoSave()
        {
            void SaveStr(ChangeEvent<string> _) => SaveToPrefs();
            void SaveBool(ChangeEvent<bool>  _) => SaveToPrefs();
            _concurrency?.RegisterValueChangedCallback(SaveStr);
            _runsPerIp?.RegisterValueChangedCallback(SaveStr);
            _ipLimit?.RegisterValueChangedCallback(SaveStr);
            _url?.RegisterValueChangedCallback(SaveStr);
            _tp?.RegisterValueChangedCallback(SaveStr);
            _ipFile?.RegisterValueChangedCallback(SaveStr);
            _ipv6File?.RegisterValueChangedCallback(SaveStr);
            _dn?.RegisterValueChangedCallback(SaveStr);
            _dt?.RegisterValueChangedCallback(SaveStr);
            _sl?.RegisterValueChangedCallback(SaveStr);
            _outputFile?.RegisterValueChangedCallback(SaveStr);
            _outputLimit?.RegisterValueChangedCallback(SaveStr);
            _hostsExpr?.RegisterValueChangedCallback(SaveStr);
            _useIpv6?.RegisterValueChangedCallback(SaveBool);
            _downloadEnabled?.RegisterValueChangedCallback(SaveBool);
            _silent?.RegisterValueChangedCallback(SaveBool);
            _debug?.RegisterValueChangedCallback(SaveBool);
            _hostsDryRun?.RegisterValueChangedCallback(SaveBool);
        }

        public TestConfig BuildConfig()
        {
            var cfg = new TestConfig
            {
                UseTcping        = _useTcping?.value  ?? false,
                UseHttping       = _useHttping?.value ?? false,
                Concurrency      = ParseInt(_concurrency?.value, 200),
                RunsPerIp        = ParseInt(_runsPerIp?.value, 4),
                IpLimit          = ParseInt(_ipLimit?.value, 0),
                Url              = string.IsNullOrWhiteSpace(_url?.value)
                                   ? "https://speed.cloudflare.com/__down?bytes=52428800"
                                   : _url.value,
                Tp               = ParseInt(_tp?.value, 443),
                UseIpv6          = _useIpv6?.value ?? false,
                DownloadEnabled  = _downloadEnabled?.value ?? true,
                OutputFile       = string.IsNullOrWhiteSpace(_outputFile?.value) ? "result.csv" : _outputFile.value,
                OutputLimit      = ParseInt(_outputLimit?.value, 10),
                Silent           = _silent?.value ?? false,
                Debug            = _debug?.value  ?? false,
                HostsExpr        = _hostsExpr?.value ?? string.Empty,
                HostsDryRun      = _hostsDryRun?.value ?? false,
            };
            var ipFile = _ipFile?.value ?? "ip.txt";
            cfg.IpSourceFiles = string.IsNullOrWhiteSpace(ipFile)
                ? new System.Collections.Generic.List<string> { "ip.txt" }
                : new System.Collections.Generic.List<string> { ipFile };
            return cfg;
        }

        private void SaveToPrefs()
        {
            PlayerPrefs.SetInt("cfst.useTcping",       (_useTcping?.value  ?? false) ? 1 : 0);
            PlayerPrefs.SetInt("cfst.useHttping",      (_useHttping?.value ?? false) ? 1 : 0);
            PlayerPrefs.SetString("cfst.concurrency",  _concurrency?.value  ?? "200");
            PlayerPrefs.SetString("cfst.runsPerIp",    _runsPerIp?.value    ?? "4");
            PlayerPrefs.SetString("cfst.ipLimit",      _ipLimit?.value      ?? "0");
            PlayerPrefs.SetString("cfst.url",          _url?.value          ?? "");
            PlayerPrefs.SetString("cfst.tp",           _tp?.value           ?? "443");
            PlayerPrefs.SetString("cfst.ipFile",       _ipFile?.value       ?? "ip.txt");
            PlayerPrefs.SetInt("cfst.useIpv6",         (_useIpv6?.value     ?? false) ? 1 : 0);
            PlayerPrefs.SetString("cfst.ipv6File",     _ipv6File?.value     ?? "ipv6.txt");
            PlayerPrefs.SetInt("cfst.dlEnabled",       (_downloadEnabled?.value ?? true) ? 1 : 0);
            PlayerPrefs.SetString("cfst.dn",           _dn?.value           ?? "10");
            PlayerPrefs.SetString("cfst.dt",           _dt?.value           ?? "10");
            PlayerPrefs.SetString("cfst.sl",           _sl?.value           ?? "0");
            PlayerPrefs.SetString("cfst.outputFile",   _outputFile?.value   ?? "result.csv");
            PlayerPrefs.SetString("cfst.outputLimit",  _outputLimit?.value  ?? "10");
            PlayerPrefs.SetInt("cfst.silent",          (_silent?.value ?? false) ? 1 : 0);
            PlayerPrefs.SetInt("cfst.debug",           (_debug?.value  ?? false) ? 1 : 0);
            PlayerPrefs.SetString("cfst.hostsExpr",    _hostsExpr?.value    ?? "");
            PlayerPrefs.SetInt("cfst.hostsDryRun",     (_hostsDryRun?.value ?? false) ? 1 : 0);
            PlayerPrefs.Save();
            SetFeedback("已保存", "ok");
        }

        private void LoadFromPrefs()
        {
            SetToggle(_useTcping,  PlayerPrefs.GetInt("cfst.useTcping",  0) == 1);
            SetToggle(_useHttping, PlayerPrefs.GetInt("cfst.useHttping", 0) == 1);
            bool useTcping  = _useTcping?.value  ?? false;
            bool useHttping = _useHttping?.value ?? false;
            SetToggle(_useIcmp, !useTcping && !useHttping);
            SetText(_concurrency,  PlayerPrefs.GetString("cfst.concurrency",  "200"));
            SetText(_runsPerIp,    PlayerPrefs.GetString("cfst.runsPerIp",    "4"));
            SetText(_ipLimit,      PlayerPrefs.GetString("cfst.ipLimit",      "0"));
            SetText(_url,          PlayerPrefs.GetString("cfst.url",          "https://speed.cloudflare.com/__down?bytes=52428800"));
            SetText(_tp,           PlayerPrefs.GetString("cfst.tp",           "443"));
            SetText(_ipFile,       PlayerPrefs.GetString("cfst.ipFile",       "ip.txt"));
            SetToggle(_useIpv6,    PlayerPrefs.GetInt("cfst.useIpv6", 0) == 1);
            SetText(_ipv6File,     PlayerPrefs.GetString("cfst.ipv6File",     "ipv6.txt"));
            SetToggle(_downloadEnabled, PlayerPrefs.GetInt("cfst.dlEnabled", 1) == 1);
            SetText(_dn,           PlayerPrefs.GetString("cfst.dn",           "10"));
            SetText(_dt,           PlayerPrefs.GetString("cfst.dt",           "10"));
            SetText(_sl,           PlayerPrefs.GetString("cfst.sl",           "0"));
            SetText(_outputFile,   PlayerPrefs.GetString("cfst.outputFile",   "result.csv"));
            SetText(_outputLimit,  PlayerPrefs.GetString("cfst.outputLimit",  "10"));
            SetToggle(_silent,     PlayerPrefs.GetInt("cfst.silent",  0) == 1);
            SetToggle(_debug,      PlayerPrefs.GetInt("cfst.debug",   0) == 1);
            SetText(_hostsExpr,    PlayerPrefs.GetString("cfst.hostsExpr",    ""));
            SetToggle(_hostsDryRun,PlayerPrefs.GetInt("cfst.hostsDryRun", 0) == 1);
        }

        private static void SetText(TextField f, string v)   { if (f != null) f.SetValueWithoutNotify(v); }
        private static void SetToggle(Toggle t, bool v)       { if (t != null) t.SetValueWithoutNotify(v); }
        private static int  ParseInt(string s, int def)       => int.TryParse(s, out var r) ? r : def;

        private void SetFeedback(string msg, string kind)
        {
            if (_feedback == null) return;
            _feedback.text = msg;
            _feedback.RemoveFromClassList("cfst-feedback--ok");
            _feedback.RemoveFromClassList("cfst-feedback--err");
            _feedback.RemoveFromClassList("cfst-feedback--info");
            _feedback.AddToClassList($"cfst-feedback--{kind}");
        }
    }
}
