using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.GUI
{
    public enum MainWindowLayoutPreset
    {
        Auto = 0,
        Desktop = 1,
        MobilePhone = 2,
        MobileTablet = 3,
    }

    public enum MainWindowLayoutKind
    {
        Desktop = 0,
        Phone = 1,
        Tablet = 2,
    }

    public readonly struct MainWindowLayoutDecision
    {
        public readonly VisualTreeAsset WindowAsset;
        public readonly MainWindowLayoutKind LayoutKind;
        public readonly bool IsMobileStructure;

        public MainWindowLayoutDecision(VisualTreeAsset windowAsset, MainWindowLayoutKind layoutKind, bool isMobileStructure)
        {
            WindowAsset = windowAsset;
            LayoutKind = layoutKind;
            IsMobileStructure = isMobileStructure;
        }
    }

    public static class MainWindowLayoutResolver
    {
        private const int TabletShortSideThreshold = 900;
        private const int PhoneShortSideThreshold = 600;

        public static MainWindowLayoutDecision Resolve(
            MainWindowLayoutPreset preset,
            VisualTreeAsset desktopAsset,
            VisualTreeAsset mobileAsset)
        {
            int shortSide = Mathf.Min(Screen.width, Screen.height);
            var effectivePreset = ResolveEditorPresetOverride(preset, shortSide);
            bool prefersMobileStructure = ShouldUseMobileStructure(effectivePreset);

            MainWindowLayoutKind kind;
            switch (effectivePreset)
            {
                case MainWindowLayoutPreset.Desktop:
                    kind = MainWindowLayoutKind.Desktop;
                    break;
                case MainWindowLayoutPreset.MobileTablet:
                    kind = MainWindowLayoutKind.Tablet;
                    break;
                case MainWindowLayoutPreset.MobilePhone:
                    kind = MainWindowLayoutKind.Phone;
                    break;
                default:
                    kind = ResolveAutoLayoutKind(prefersMobileStructure, shortSide);
                    break;
            }

            var picked = prefersMobileStructure ? mobileAsset : desktopAsset;
            if (picked == null)
                picked = desktopAsset != null ? desktopAsset : mobileAsset;

            return new MainWindowLayoutDecision(picked, kind, prefersMobileStructure);
        }

        private static MainWindowLayoutPreset ResolveEditorPresetOverride(MainWindowLayoutPreset preset, int shortSide)
        {
#if UNITY_EDITOR
            // Editor 下如果 GameView 太小，强制用移动布局避免桌面布局被挤乱
            if (preset == MainWindowLayoutPreset.Desktop && shortSide > 0 && shortSide < PhoneShortSideThreshold)
                return MainWindowLayoutPreset.MobilePhone;
#endif
            return preset;
        }

        public static bool ShouldUseMobileStructure(MainWindowLayoutPreset preset)
        {
            switch (preset)
            {
                case MainWindowLayoutPreset.Desktop:
                    return false;
                case MainWindowLayoutPreset.MobilePhone:
                case MainWindowLayoutPreset.MobileTablet:
                    return true;
                default:
                    return Application.isMobilePlatform;
            }
        }

        private static MainWindowLayoutKind ResolveAutoLayoutKind(bool mobileStructure, int shortSide)
        {
            if (!mobileStructure)
                return MainWindowLayoutKind.Desktop;

            if (shortSide >= TabletShortSideThreshold)
                return MainWindowLayoutKind.Tablet;

            if (shortSide < PhoneShortSideThreshold)
                return MainWindowLayoutKind.Phone;

            return MainWindowLayoutKind.Tablet;
        }
    }
}
