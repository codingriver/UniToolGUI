using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

// Ensures the scheduling mode default is always set to "Not enabled" on startup.
// This guards against runtime logic that might clear the selection on first launch.
public class PageScheduleInitializer : MonoBehaviour
{
    [SerializeField] private int uiInitMaxFrames = 120;
    [SerializeField] private bool enableDiagnostics = true;

    void Start()
    {
        var uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            var found = FindObjectOfType<UIDocument>();
            if (found != null)
                uiDocument = found;
        }

        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.Log("[PageScheduleInitializer] UIDocument not found or rootVisualElement is null. Aborting initialization.");
            return;
        }

        var root = uiDocument.rootVisualElement;
        if (enableDiagnostics) Debug.Log("[PageScheduleInitializer] UIDocument and rootVisualElement discovered. Preparing init.");

        StartCoroutine(InitializeDefaultWithTimeout(root));
    }

    void OnEnable()
    {
        // Disabled: re-running on every OnEnable resets all RadioButtonGroups across the entire UI
        // (including hook page type selectors), causing unexpected state changes.
        // Initial default is handled once in Start().
    }

    IEnumerator InitializeDefaultWithTimeout(VisualElement root)
    {
        int attempts = 0;
        bool diag = enableDiagnostics;
        while (attempts < uiInitMaxFrames)
        {
            var schedGroup = root.Q<RadioButtonGroup>("sched-mode-group");
            if (schedGroup != null)
            {
                schedGroup.value = 0; // int index for default option
                if (diag) Debug.Log("[PageScheduleInitializer] sched-mode-group found on attempt " + attempts + ", set to 0");
                yield break;
            }

            var radios = root.Query<RadioButton>().ToList();
            if (radios.Count > 0)
            {
                // Only touch RadioButtons that are descendants of the schedule page
                var schedPage = root.Q<VisualElement>("page-schedule");
                if (schedPage != null)
                {
                    var schedRadios = schedPage.Query<RadioButton>().ToList();
                    if (schedRadios.Count > 0)
                    {
                        foreach (var rb in schedRadios) rb.value = false;
                        schedRadios[0].value = true;
                        if (diag) Debug.Log("[PageScheduleInitializer] No group found; selected first schedule RadioButton as fallback on attempt " + attempts);
                        yield break;
                    }
                }
                if (diag) Debug.Log("[PageScheduleInitializer] schedule page not found, skipping RadioButton fallback");
                yield break;
            }

            attempts++;
            yield return null;
        }

        // Timeout reached - final fallback: only touch sched-mode-group specifically
        if (enableDiagnostics) Debug.Log("[PageScheduleInitializer] Timeout: sched-mode-group not found, skipping fallback to avoid touching other RadioButtons");
    }
}
