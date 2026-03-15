using System;
using UnityEngine;

namespace CloudflareST.GUI
{
    /// <summary>
    /// 全局运行状态，驱动 UI 实时刷新
    /// </summary>
    public class AppState
    {
        // ── 单例 ──────────────────────────────────────────────
        public static readonly AppState Instance = new AppState();
        private AppState() { }

        // ── 事件 ──────────────────────────────────────────────
        public event Action OnChanged;
        private void Notify() => OnChanged?.Invoke();

        // ── 运行状态 ──────────────────────────────────────────
        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set { if (_isRunning == value) return; _isRunning = value; Notify(); }
        }

        // ── 导航页 ────────────────────────────────────────────
        private int _currentPage;
        public int CurrentPage
        {
            get => _currentPage;
            set { if (_currentPage == value) return; _currentPage = value; Notify(); }
        }

        // ── 进度 ──────────────────────────────────────────────
        private float _progress;
        public float Progress
        {
            get => _progress;
            set { _progress = Mathf.Clamp01(value); Notify(); }
        }

        // ── 状态文字 ──────────────────────────────────────────
        private string _statusText = "就绪";
        public string StatusText
        {
            get => _statusText;
            set { if (_statusText == value) return; _statusText = value; Notify(); }
        }

        // ── 计数 ──────────────────────────────────────────────
        private int _testedCount;
        public int TestedCount
        {
            get => _testedCount;
            set { if (_testedCount == value) return; _testedCount = value; Notify(); }
        }

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set { if (_totalCount == value) return; _totalCount = value; Notify(); }
        }

        private int _passedCount;
        public int PassedCount
        {
            get => _passedCount;
            set { if (_passedCount == value) return; _passedCount = value; Notify(); }
        }

        // ── 性能指标 ──────────────────────────────────────────
        private float _bestLatency = -1f;
        public float BestLatency
        {
            get => _bestLatency;
            set { _bestLatency = value; Notify(); }
        }

        private float _bestSpeed = -1f;
        public float BestSpeed
        {
            get => _bestSpeed;
            set { _bestSpeed = value; Notify(); }
        }

        // ── 时间 ──────────────────────────────────────────────
        public DateTime StartTime { get; set; }
        public DateTime FinishTime { get; set; }

        private TimeSpan _elapsed;
        public TimeSpan Elapsed
        {
            get => _elapsed;
            set { _elapsed = value; Notify(); }
        }

        // ── 结果角标 ──────────────────────────────────────────
        private int _resultCount;
        public int ResultCount
        {
            get => _resultCount;
            set { if (_resultCount == value) return; _resultCount = value; Notify(); }
        }

        // ── 重置 ──────────────────────────────────────────────
        public void Reset()
        {
            _isRunning    = false;
            _progress     = 0f;
            _statusText   = "就绪";
            _testedCount  = 0;
            _totalCount   = 0;
            _passedCount  = 0;
            _bestLatency  = -1f;
            _bestSpeed    = -1f;
            _elapsed      = TimeSpan.Zero;
            Notify();
        }
    }
}
