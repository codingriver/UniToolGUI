using UnityEngine;
using System.Collections.Generic;
using System.Collections;


/// <summary>
/// 托盘图标功能全面测试用例
/// 演示：普通菜单、勾选项、动态菜单、分隔线、退出项等
/// 支持 Windows 和 Mac 平台。
/// </summary>
public class TrayIconTest : MonoBehaviour
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX
    [Header("测试配置")]
    public string tooltip = "Unity 托盘测试工具";
    public bool simulateInEditor = true;   // 在编辑器中模拟托盘行为（仅日志）

    // 菜单项定义
    private TrayMenuItem _showWindowItem;
    private TrayMenuItem _aboutItem;
    private TrayMenuItem _enableFeatureItem;
    private TrayMenuItem _dynamicMenuItem;
    private TrayMenuItem _exitItem;
    private TrayMenuItem _toggleAutoRefresh;
    private TrayMenuItem _addMenuDemoItem;

    // 动态菜单文本更新协程
    private Coroutine _dynamicMenuCoroutine;
    private bool _autoRefreshEnabled = true;

    // 模拟状态
    private bool _featureEnabled = false;

    private void Awake()
    {
        Debug.Log("[TrayIconTest] 托盘测试组件已加载");
    }

    private void Start()
    {
        InitializeTray();
    }

    private void OnDestroy()
    {
        ShutdownTray();
    }

    /// <summary>
    /// 初始化托盘并注册菜单
    /// </summary>
    private void InitializeTray()
    {
        // 创建菜单项
        CreateMenuItems();

        if (Application.isEditor && !simulateInEditor)
        {
            Debug.Log("[TrayTest] 编辑器下不创建实际托盘图标");
            return;
        }

        // 初始化托盘服务（真实或模拟）
        TrayIconService.Instance.Initialize();
        TrayIconService.Instance.SetTooltip(tooltip);
        TrayIconService.Instance.RegisterMenuItems(new TrayMenuItem[]
        {
            _showWindowItem,
            _aboutItem,
            new TrayMenuItem { IsSeparator = true },      // 分隔线
            _enableFeatureItem,
            _toggleAutoRefresh,
            new TrayMenuItem { IsSeparator = true },
            _dynamicMenuItem,                              // 动态显示文本的菜单项
            _addMenuDemoItem,
            new TrayMenuItem { IsSeparator = true },
            _exitItem
        });

        // 启动动态菜单更新（如果启用）
        if (_autoRefreshEnabled)
        {
            _dynamicMenuCoroutine = StartCoroutine(UpdateDynamicMenuItem());
        }

        Debug.Log("[TrayTest] 托盘菜单注册完成");

    }

    private void ShutdownTray()
    {
        if (_dynamicMenuCoroutine != null)
            StopCoroutine(_dynamicMenuCoroutine);


        if (Application.isEditor && !simulateInEditor)
            return;

        // 清理托盘（通常在应用退出时自动执行，这里显式调用以演示）
        TrayIconService.Instance.Shutdown();

    }

    /// <summary>
    /// 创建所有菜单项实例
    /// </summary>
    private void CreateMenuItems()
    {
        // 1. 显示窗口（调用托盘服务的显示方法）
        _showWindowItem = new TrayMenuItem
        {
            Text = "显示主窗口",
            Callback = () =>
            {
                Debug.Log("[TrayTest] 点击显示窗口");
                TrayIconService.Instance.ShowMainWindow(); // 需要公开此方法
            }
        };

        // 2. 关于
        _aboutItem = new TrayMenuItem
        {
            Text = "关于",
            Callback = () =>
            {
                Debug.Log("[TrayTest] 点击关于");
                WindowsMessageBox.Info("Unity 托盘测试工具\n版本 1.0\n作者: 示例");
            }
        };

        // 3. 勾选项：启用某项功能
        _enableFeatureItem = new TrayMenuItem
        {
            Text = "启用测试功能",
            IsToggle = true,
            Checked = false,
            Callback = () =>
            {
                _featureEnabled = _enableFeatureItem.Checked; // 状态已自动切换
                Debug.Log($"[TrayTest] 测试功能状态: {_featureEnabled}");
                // 这里可以执行实际的功能开关逻辑，例如启用/禁用某个组件
            }
        };

        // 4. 勾选项：自动刷新动态菜单
        _toggleAutoRefresh = new TrayMenuItem
        {
            Text = "自动刷新动态菜单",
            IsToggle = true,
            Checked = true,
            Callback = () =>
            {
                _autoRefreshEnabled = _toggleAutoRefresh.Checked;
                Debug.Log($"[TrayTest] 自动刷新状态变更为: {_autoRefreshEnabled}");

                if (_autoRefreshEnabled && _dynamicMenuCoroutine == null)
                {
                    _dynamicMenuCoroutine = StartCoroutine(UpdateDynamicMenuItem());
                }
                else if (!_autoRefreshEnabled && _dynamicMenuCoroutine != null)
                {
                    StopCoroutine(_dynamicMenuCoroutine);
                    _dynamicMenuCoroutine = null;
                }
                Debug.Log($"[TrayTest] 自动刷新: {_autoRefreshEnabled}");
            }
        };

        // 5. 动态菜单项（文本会变化）
        _dynamicMenuItem = new TrayMenuItem
        {
            Text = "当前时间: " + System.DateTime.Now.ToLongTimeString(),
            Callback = () =>
            {
                Debug.Log("[TrayTest] 点击动态菜单 - 显示当前时间");
                WindowsMessageBox.Info("当前系统时间: " + System.DateTime.Now.ToLongTimeString(), "时间");
            }
        };

        // 6. 添加/移除一个演示菜单项（动态添加示例）
        _addMenuDemoItem = new TrayMenuItem
        {
            Text = "添加演示菜单",
            Callback = () =>
            {
                Debug.Log("[TrayTest] 点击添加演示菜单，将在下方插入新项");
                AddDemoMenuItem();
            }
        };

        // 7. 退出
        _exitItem = new TrayMenuItem
        {
            Text = "退出",
            Callback = () =>
            {
                Debug.Log("[TrayTest] 点击退出");
                var confirm = WindowsMessageBox.Confirm("确定要退出程序吗？", "确认退出");
                if (confirm)
                {
                    ShutdownTray();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif                    

                }
            }
        };
    }

    /// <summary>
    /// 动态更新菜单项的文本（每秒一次）
    /// </summary>
    private IEnumerator UpdateDynamicMenuItem()
    {
        Debug.Log("[TrayTest] 动态菜单刷新协程启动");
        while (_autoRefreshEnabled)
        {
            string newText = "当前时间: " + System.DateTime.Now.ToLongTimeString();
            if (_dynamicMenuItem.Text != newText) // 仅当变化时才更新
            {
                _dynamicMenuItem.Text = newText;
                Debug.Log($"[TrayTest] 菜单文本更新为: {newText}");
                TrayIconService.Instance.RefreshMenu();
            }
            yield return new WaitForSeconds(1f);
        }
        Debug.Log("[TrayTest] 动态菜单刷新协程结束");
        _dynamicMenuCoroutine = null;
    }

    /// <summary>
    /// 演示动态添加菜单项
    /// </summary>
    private void AddDemoMenuItem()
    {
        TrayMenuItem demoItem = null;  // 先声明
        demoItem = new TrayMenuItem
        {
            Text = "我是动态添加的菜单",
            Callback = () =>
            {
                Debug.Log("[TrayTest] 动态菜单被点击");
                WindowsMessageBox.Info("这是一个动态添加的菜单项，点击后会移除自己", "动态菜单");
                // 点击后移除自己
                TrayIconService.Instance.UnregisterMenuItems(new[] { demoItem });
            }
        };
        TrayIconService.Instance.RegisterMenuItems(new[] { demoItem });
    }    

    // 为了演示，添加一个公共方法用于在外部触发托盘显示
    public void ShowMainWindow()
    {
        TrayIconService.Instance.ShowMainWindow();
    }

#else
    [Header("测试配置")]
    public string tooltip = "Unity 托盘测试工具";
    public bool simulateInEditor = true;

    private void Awake()
    {
        Debug.Log("[TrayIconTest] 托盘功能仅在 Windows 和 Mac 平台有效，当前平台已跳过");
    }

    public void ShowMainWindow() { }
#endif
}