using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.GUI
{
    public class PageLatencyController : MonoBehaviour
    {
        private VisualElement _root;
        private CfstOptions   _opts;

        private RadioButton   _radioIcmp;
        private RadioButton   _radioTcping;
        private RadioButton   _radioHttping;
        private Toggle        _forceIcmpToggle;
        private IntegerField  _pingConcField;
        private IntegerField  _pingCountField;
        private IntegerField  _latencyMaxField;
        private IntegerField  _latencyMinField;
        private FloatField    _packetLossField;
        private VisualElement _httpingGroup;
        private IntegerField  _httpingCodeField;
        private TextField     _cfColoField;
        private Label         _hintLatencyMax;

        public void Init(VisualElement root, CfstOptions opts)
        {
            _root = root;
            _opts = opts;

            _radioIcmp        = root.Q<RadioButton>("radio-icmp");
            _radioTcping      = root.Q<RadioButton>("radio-tcping");
            _radioHttping     = root.Q<RadioButton>("radio-httping");
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

            _radioIcmp?   .RegisterValueChangedCallback(e => { if (e.newValue) SetMode(PingMode.IcmpAuto); });
            _radioTcping? .RegisterValueChangedCallback(e => { if (e.newValue) SetMode(PingMode.TcPing);   });
            _radioHttping?.RegisterValueChangedCallback(e => { if (e.newValue) SetMode(PingMode.Httping);  });

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

            SetMode(PingMode.IcmpAuto);
        }

        private void SetMode(PingMode mode)
        {
            _opts.PingMode = mode;

            bool isHttping = mode == PingMode.Httping;
            bool isIcmp    = mode == PingMode.IcmpAuto;

            _forceIcmpToggle?.SetEnabled(isIcmp);
            if (!isIcmp) { _opts.ForceIcmp = false; if (_forceIcmpToggle != null) _forceIcmpToggle.value = false; }

            if (_httpingGroup != null)
            {
                _httpingGroup.style.display = isHttping
                    ? DisplayStyle.Flex : DisplayStyle.None;
            }
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
