using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.GUI
{
    public class PageLatencyController : MonoBehaviour
    {
        private VisualElement    _root;
        private CfstOptions      _opts;

        private RadioButtonGroup _pingModeGroup;
        private Toggle           _forceIcmpToggle;
        private IntegerField     _pingConcField;
        private IntegerField     _pingCountField;
        private IntegerField     _latencyMaxField;
        private IntegerField     _latencyMinField;
        private FloatField       _packetLossField;
        private VisualElement    _httpingGroup;
        private IntegerField     _httpingCodeField;
        private TextField        _cfColoField;
        private Label            _hintLatencyMax;

        public void Init(VisualElement root, CfstOptions opts)
        {
            if (root == null)
            {
                Debug.LogError("[UI] PageLatencyController.Init root is null");
                return;
            }

            _root = root;
            _opts = opts;

            _pingModeGroup    = root.Q<RadioButtonGroup>("ping-mode-group");
            _forceIcmpToggle  = root.Q<Toggle>("toggle-forceicmp");
            _pingConcField    = root.Q<IntegerField>("field-pingconcurrency");
            _pingCountField   = root.Q<IntegerField>("field-pingcount");
            _latencyMaxField  = root.Q<IntegerField>("field-latencymax");
            _latencyMinField  = root.Q<IntegerField>("field-latencymin");
            _packetLossField  = root.Q<FloatField>("field-packetlossmax");
            _httpingGroup     = root.Q<VisualElement>("httping-group");
            _httpingCodeField = root.Q<IntegerField>("field-httpingcode");
            _cfColoField      = root.Q<TextField>("field-cfcolo");
            _hintLatencyMax   = root.Q<Label>("hint-latencymax");

            // RadioButtonGroup: value = index of selected (0=ICMP, 1=TCPing, 2=HTTPing)
            _pingModeGroup?.RegisterValueChangedCallback(e =>
            {
                switch (e.newValue)
                {
                    case 1: SetMode(PingMode.TcPing);   break;
                    case 2: SetMode(PingMode.Httping);  break;
                    default: SetMode(PingMode.IcmpAuto); break;
                }
            });

            _forceIcmpToggle?.RegisterValueChangedCallback(e => _opts.ForceIcmp = e.newValue);

            _pingConcField?  .RegisterValueChangedCallback(e => _opts.PingConcurrency = Clamp(e.newValue, 1, 1000));
            _pingCountField? .RegisterValueChangedCallback(e => _opts.PingCount        = Clamp(e.newValue, 1, 20));
            _latencyMaxField?.RegisterValueChangedCallback(e => ValidateLatencyRange());
            _latencyMinField?.RegisterValueChangedCallback(e => ValidateLatencyRange());
            _packetLossField?.RegisterValueChangedCallback(e =>
                _opts.PacketLossMax = UnityEngine.Mathf.Clamp(e.newValue, 0f, 100f) / 100f);

            _httpingCodeField?.RegisterValueChangedCallback(e => _opts.HttpingCode = e.newValue);
            _cfColoField?     .RegisterValueChangedCallback(e =>
            {
                var raw = e.newValue?.Trim().ToUpperInvariant();
                _opts.CfColo = string.IsNullOrEmpty(raw) ? null : raw;
            });

            // 与 SettingsStorage.Load 后的 _opts 对齐；RadioButtonGroup 在「目标索引与当前 value 相同」时
            // 往往不会刷新子 Radio 的 :checked 样式（同值赋值不触发内部刷新），需先切到临时索引再设回。
            int modeIndex = Mathf.Clamp((int)_opts.PingMode, 0, 2);
            ApplyPingModeGroupSelection(_pingModeGroup, modeIndex);
            SetMode((PingMode)modeIndex);
        }

        /// <summary>
        /// 供 MainWindow 等在 InitPages 之后输出诊断；仅 Editor / Development Build 有日志。
        /// </summary>
        public static void LogPingModeGroupFromRoot(VisualElement root)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogPingModeGroupDiagnostics(root?.Q<RadioButtonGroup>("ping-mode-group"));
#endif
        }

        /// <summary>
        /// 解决 RadioButtonGroup 在 UXML 已设 value 且运行时再次设为同一索引时，子项不显示选中态的问题。
        /// </summary>
        private static void ApplyPingModeGroupSelection(RadioButtonGroup group, int index)
        {
            if (group == null) return;
            index = Mathf.Clamp(index, 0, 2);
            if (group.value == index)
            {
                int temp = index == 0 ? 1 : 0;
                group.SetValueWithoutNotify(temp);
            }
            group.SetValueWithoutNotify(index);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void LogPingModeGroupDiagnostics(RadioButtonGroup group)
        {
            if (group == null) return;
            var sb = new StringBuilder();
            sb.Append("[UI] ping-mode-group value=").Append(group.value);
            int i = 0;
            foreach (var child in group.Children())
            {
                if (child is RadioButton rb)
                    sb.Append(" | rb[").Append(i++).Append("].value=").Append(rb.value);
            }
            Debug.Log(sb.ToString());
        }
#endif

        private void SetMode(PingMode mode)
        {
            _opts.PingMode = mode;

            bool isHttping = mode == PingMode.Httping;
            bool isIcmp    = mode == PingMode.IcmpAuto;

            _forceIcmpToggle?.SetEnabled(isIcmp);
            if (!isIcmp) { _opts.ForceIcmp = false; if (_forceIcmpToggle != null) _forceIcmpToggle.value = false; }

            if (_httpingGroup != null)
                _httpingGroup.style.display = isHttping ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ValidateLatencyRange()
        {
            int max = _latencyMaxField?.value ?? 9999;
            int min = _latencyMinField?.value ?? 0;
            bool valid = max > min;
            _latencyMaxField?.EnableInClassList("field-int--error", !valid);
            if (_hintLatencyMax != null)
                _hintLatencyMax.text = valid ? "" : "延迟上限必须大于下限";
            if (valid)
            {
                _opts.LatencyMax = max;
                _opts.LatencyMin = min;
            }
        }

        private static int Clamp(int v, int min, int max) =>
            v < min ? min : v > max ? max : v;
    }
}
