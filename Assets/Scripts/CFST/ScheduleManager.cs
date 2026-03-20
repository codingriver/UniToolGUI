// ============================================================
// ScheduleManager.cs  —  Unity 侧 Cron 定时调度器
//
// 职责：
//   1. 用 Cronos 解析 Cron 表达式，计算下次触发时间
//   2. 在 Unity 协程中等待到触发时间后执行一次测速
//   3. 每次执行顺序：RunPreHook → StartTest → (OnFinished 回调) RunPostHook
//   4. 提供启动、停止、立即触发一次等操作
//   5. 对外暴露状态和下次执行时间供 UI 绑定
// ============================================================
using System;
using System.Collections;
using Cronos;
using UnityEngine;

namespace CloudflareST.GUI
{
    public class ScheduleManager : MonoBehaviour
    {
        // ── 单例 ──────────────────────────────────────────────
        public static ScheduleManager Instance { get; private set; }

        // ── 状态（UI 只读）────────────────────────────────────
        public bool      IsEnabled  { get; private set; }
        public bool      IsWaiting  { get; private set; }  // 等待下次触发中
        public DateTime? NextRunAt  { get; private set; }  // 下次触发（本地时间）
        public int       RunCount   { get; private set; }  // 本次启用后已触发次数
        public string    LastError  { get; private set; }  // 最后一次错误说明

        // ── 事件（UI 刷新用）─────────────────────────────────
        public event Action OnStateChanged;

        // ── 依赖注入（由 MainWindowController 在 InitPages 后设置）
        /// <summary>启动实际测速；由 MainWindowController.StartTest 提供</summary>
        public Action StartTestAction { get; set; }

        /// <summary>测速完成后通知调度器；MainWindowController 在 OnFinished 时调用此委托</summary>
        public Action<int> NotifyTestFinished { get; private set; }

        /// <summary>钩子控制器，调用 RunPreHook / RunPostHook</summary>
        public PageHookController HookController { get; set; }

        /// <summary>日志控制器</summary>
        public PageLogController LogController { get; set; }

        // ── 私有 ────────────────────────────────────────────
        private string         _cronExpression;
        private CronExpression _parsedCron;
        private Coroutine      _loopCoroutine;

        // 用于在协程内等待测速完成的信号
        private bool _testDone;
        private int  _testExitCode;

        // ── Unity 生命周期 ───────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // 绑定供外部回调的委托
            NotifyTestFinished = code =>
            {
                _testExitCode = code;
                _testDone     = true;
            };
        }

        // ── 公开接口 ─────────────────────────────────────────

        /// <summary>
        /// 配置并启动调度循环。
        /// </summary>
        /// <param name="cronExpr">5 段标准 Cron 表达式（分 时 日 月 周）</param>
        /// <returns>解析成功返回 true，否则 false 且 LastError 含错误说明</returns>
        public bool Enable(string cronExpr)
        {
            LastError = null;

            if (!TryParse(cronExpr, out var parsed, out string err))
            {
                LastError = err;
                Log("[SCHEDULE] Cron 解析失败: " + err);
                NotifyChanged();
                return false;
            }

            _cronExpression = cronExpr.Trim();
            _parsedCron     = parsed;

            if (_loopCoroutine != null) StopCoroutine(_loopCoroutine);

            IsEnabled = true;
            RunCount  = 0;
            IsWaiting = false;
            NextRunAt = null;

            _loopCoroutine = StartCoroutine(ScheduleLoop());
            Log("[SCHEDULE] 调度已启动: " + _cronExpression);
            NotifyChanged();
            return true;
        }

        /// <summary>停止调度循环。</summary>
        public void Disable()
        {
            if (_loopCoroutine != null) { StopCoroutine(_loopCoroutine); _loopCoroutine = null; }
            IsEnabled = false;
            IsWaiting = false;
            NextRunAt = null;
            Log("[SCHEDULE] 调度已停止");
            NotifyChanged();
        }

        /// <summary>立即触发一次，不打断已有调度循环。</summary>
        public void TriggerNow()
        {
            Log("[SCHEDULE] 手动立即触发");
            StartCoroutine(RunOnce("立即运行"));
        }

        /// <summary>
        /// 尝试解析表达式，并返回下两次预览时间（本地时间）。
        /// </summary>
        public bool TryPreview(string cronExpr,
                               out DateTime? next1, out DateTime? next2,
                               out string error)
        {
            next1 = next2 = null;
            if (!TryParse(cronExpr, out var parsed, out error)) return false;

            var utcNow = DateTime.UtcNow;
            var n1 = parsed.GetNextOccurrence(utcNow, TimeZoneInfo.Local);
            next1 = n1?.ToLocalTime();
            if (n1.HasValue)
            {
                var n2 = parsed.GetNextOccurrence(n1.Value, TimeZoneInfo.Local);
                next2 = n2?.ToLocalTime();
            }
            return true;
        }

        // ── 调度主循环 ───────────────────────────────────────
        private IEnumerator ScheduleLoop()
        {
            while (IsEnabled)
            {
                var utcNow  = DateTime.UtcNow;
                var nextUtc = _parsedCron.GetNextOccurrence(utcNow, TimeZoneInfo.Local);

                if (!nextUtc.HasValue)
                {
                    LastError = "Cron 表达式无后续触发时间，调度自动停止";
                    Log("[SCHEDULE] " + LastError);
                    IsEnabled = false;
                    IsWaiting = false;
                    NotifyChanged();
                    yield break;
                }

                NextRunAt = nextUtc.Value.ToLocalTime();
                IsWaiting = true;
                NotifyChanged();

                Log("[SCHEDULE] 下次执行: " + NextRunAt.Value.ToString("yyyy-MM-dd HH:mm:ss"));

                // 每秒检查是否到触发时间
                while (DateTime.UtcNow < nextUtc.Value)
                {
                    yield return new WaitForSeconds(1f);
                    if (!IsEnabled) yield break;
                }

                IsWaiting = false;
                NextRunAt = null;
                NotifyChanged();

                yield return StartCoroutine(RunOnce("第 " + (RunCount + 1) + " 次调度"));
            }
        }

        // ── 单次执行（前钩子 → 测速 → 后钩子）──────────────
        private IEnumerator RunOnce(string tag)
        {
            RunCount++;
            Log("[SCHEDULE] [" + tag + "] 开始");

            // ① 前置程序检查（同步，主线程执行）
            if (HookController != null && HookController.IsPreHookEnabled)
            {
                Log("[SCHEDULE] [" + tag + "] 运行前钩子...");
                bool preOk = false;
                try { preOk = HookController.RunPreHook(); }
                catch (Exception ex) { Log("[SCHEDULE] 前钉子异常: " + ex.Message); }
                if (!preOk)
                {
                    Log("[SCHEDULE] [" + tag + "] 前钩子失败，本次跳过");
                    NotifyChanged();
                    yield break;
                }
            }

            // ② 启动测速，等待完成信号
            if (StartTestAction == null)
            {
                Log("[SCHEDULE] StartTestAction 未绑定，跳过");
                yield break;
            }

            _testDone     = false;
            _testExitCode = -1;
            NotifyChanged();

            try { StartTestAction.Invoke(); }
            catch (Exception ex)
            {
                Log("[SCHEDULE] StartTestAction 异常: " + ex.Message);
                yield break;
            }

            // 等待 NotifyTestFinished，最多等待2小时防止永久卡死
            const float MAX_WAIT_SEC = 7200f;
            float waited = 0f;
            while (!_testDone && waited < MAX_WAIT_SEC)
            {
                yield return new WaitForSeconds(0.5f);
                waited += 0.5f;
                if (!IsEnabled) yield break;
            }
            if (!_testDone)
            {
                Log("[SCHEDULE] 等待测速超时（" + MAX_WAIT_SEC + "s），强制继续");
                _testExitCode = -2;
            }

            // ③ 后置程序检查（同步）
            if (HookController != null && HookController.IsPostHookEnabled)
            {
                Log("[SCHEDULE] [" + tag + "] 运行后钩子...");
                try { HookController.RunPostHook(_testExitCode); }
                catch (Exception ex) { Log("[SCHEDULE] 后钉子异常: " + ex.Message); }
            }

            Log("[SCHEDULE] [" + tag + "] 完成，exit=" + _testExitCode);
            NotifyChanged();
        }

        // ── 工具方法 ────────────────────────────────────────
        private static bool TryParse(string expr, out CronExpression result, out string error)
        {
            result = null; error = null;
            if (string.IsNullOrWhiteSpace(expr))
            {
                error = "表达式不能为空"; return false;
            }
            try
            {
                result = CronExpression.Parse(expr.Trim(), CronFormat.Standard);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message; return false;
            }
        }

        private void Log(string msg)
        {
            UnityEngine.Debug.Log(msg);
            LogController?.AppendLog(msg);
        }

        private void NotifyChanged()
        {
            try { OnStateChanged?.Invoke(); } catch { }
        }
    }
}
