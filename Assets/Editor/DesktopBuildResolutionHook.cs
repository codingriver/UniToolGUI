using CloudflareST.GUI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloudflareST.GUI.Editor
{
    public sealed class DesktopBuildResolutionHook : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;
            if (!IsDesktopStandaloneTarget(target))
                return;

            Scene tempScene = default;
            try
            {
                if (!TryResolveMainWindowController(out var controller, out tempScene))
                {
                    Debug.LogWarning("[BuildResolution] 未找到 MainWindowController，保留当前 PlayerSettings 默认分辨率不变");
                    return;
                }

                var resolution = controller.GetConfiguredDesktopResolution(target);
                if (PlayerSettings.defaultScreenWidth != resolution.width)
                    PlayerSettings.defaultScreenWidth = resolution.width;
                if (PlayerSettings.defaultScreenHeight != resolution.height)
                    PlayerSettings.defaultScreenHeight = resolution.height;

                AssetDatabase.SaveAssets();

                Debug.Log(
                    $"[BuildResolution] target={target} -> {resolution.width}x{resolution.height}, " +
                    $"macPreset={controller.MacWindowPreset}, windowsPreset={controller.WindowsWindowPreset}");
            }
            finally
            {
                if (tempScene.IsValid() && tempScene.isLoaded)
                    EditorSceneManager.CloseScene(tempScene, true);
            }
        }

        private static bool IsDesktopStandaloneTarget(BuildTarget target)
        {
            return target == BuildTarget.StandaloneOSX ||
                   target == BuildTarget.StandaloneWindows ||
                   target == BuildTarget.StandaloneWindows64;
        }

        private static bool TryResolveMainWindowController(out MainWindowController controller, out Scene tempScene)
        {
            controller = FindControllerInLoadedScenes();
            if (controller != null)
            {
                tempScene = default;
                return true;
            }

            foreach (var sceneSetting in EditorBuildSettings.scenes)
            {
                if (!sceneSetting.enabled)
                    continue;

                var loadedScene = EditorSceneManager.OpenScene(sceneSetting.path, OpenSceneMode.Additive);
                controller = FindControllerInScene(loadedScene);
                if (controller != null)
                {
                    tempScene = loadedScene;
                    return true;
                }

                EditorSceneManager.CloseScene(loadedScene, true);
            }

            tempScene = default;
            return false;
        }

        private static MainWindowController FindControllerInLoadedScenes()
        {
            var allControllers = Resources.FindObjectsOfTypeAll<MainWindowController>();
            foreach (var controller in allControllers)
            {
                if (controller == null || EditorUtility.IsPersistent(controller))
                    continue;

                var scene = controller.gameObject.scene;
                if (scene.IsValid() && scene.isLoaded)
                    return controller;
            }

            return null;
        }

        private static MainWindowController FindControllerInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null)
                    continue;

                var controller = root.GetComponentInChildren<MainWindowController>(true);
                if (controller != null)
                    return controller;
            }

            return null;
        }
    }
}
