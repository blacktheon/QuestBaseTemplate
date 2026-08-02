using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Immrsiv.QuestSetup.Editor
{
    public class QuestSetupWindow : EditorWindow
    {
        List<SetupCheck> _checks;
        readonly Dictionary<SetupCheck, (bool ok, string details)> _results =
            new Dictionary<SetupCheck, (bool, string)>();
        readonly Dictionary<SetupCheck, string> _fixErrors =
            new Dictionary<SetupCheck, string>();
        Vector2 _scroll;

        [MenuItem("Immrsiv/Quest Setup")]
        public static void Open() => GetWindow<QuestSetupWindow>("Quest Setup");

        void OnEnable()
        {
            _checks = SetupCheckRegistry.CreateAll();
            RefreshAll();
        }

        void OnFocus()
        {
            // Checks re-evaluate only here and after fixes, not on every repaint -
            // some of them touch the asset database.
            if (_checks != null) RefreshAll();
            Repaint();
        }

        void RefreshAll()
        {
            _results.Clear();
            int failing = 0;
            foreach (var check in _checks)
            {
                bool ok;
                string details;
                try { ok = check.Evaluate(out details); }
                catch (System.Exception e) { ok = false; details = e.Message; }
                _results[check] = (ok, details);
                if (!ok && !check.IsAdvisory) failing++;
                if (ok) _fixErrors.Remove(check);
            }
        }

        void OnGUI()
        {
            if (_checks == null) return;
            EditorGUILayout.HelpBox(
                "Immrsiv Quest Setup - project requirements for Meta Quest. Nothing changes without a click " +
                "(one exception: evaluating the Meta checklist registers our documented render-mode policy ignore in Meta's settings).\n" +
                "Click Fix All once - the window resumes itself across the build-target switch and sample-import reloads.",
                MessageType.Info);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            int failing = 0;
            foreach (var check in _checks)
            {
                var (ok, details) = _results.TryGetValue(check, out var r) ? r : (false, "Not evaluated.");
                if (!ok && !check.IsAdvisory) failing++;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                var icon = ok ? "TestPassed" : check.IsAdvisory ? "console.warnicon" : "TestFailed";
                GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.Width(20));
                EditorGUILayout.BeginVertical();
                GUILayout.Label(check.Label, EditorStyles.boldLabel);
                GUILayout.Label(details, EditorStyles.wordWrappedMiniLabel);
                if (_fixErrors.TryGetValue(check, out var fixError))
                    EditorGUILayout.HelpBox($"Fix failed: {fixError}", MessageType.Error);
                EditorGUILayout.EndVertical();
                using (new EditorGUI.DisabledScope(ok))
                {
                    if (GUILayout.Button("Fix", GUILayout.Width(60)))
                    {
                        RunFix(check);
                        RefreshAll();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            // Optional input-system migration toggles (ProjectValidationCheck exclusion opt-in).
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Optional input-system migrations (may break existing input code)",
                EditorStyles.boldLabel);
            bool poseOn = OptionalRulePrefs.PoseControlEnabled;
            bool poseOnNew = EditorGUILayout.ToggleLeft(
                "Migrate PoseControl → InputSystem.XR.PoseControl (OpenXR deprecation)",
                poseOn);
            if (poseOnNew != poseOn)
            {
                OptionalRulePrefs.PoseControlEnabled = poseOnNew;
                RefreshAll();
            }
            bool stickOn = OptionalRulePrefs.StickControlEnabled;
            bool stickOnNew = EditorGUILayout.ToggleLeft(
                "Migrate Vector2Control thumbsticks → StickControl (OpenXR deprecation)",
                stickOn);
            if (stickOnNew != stickOn)
            {
                OptionalRulePrefs.StickControlEnabled = stickOnNew;
                RefreshAll();
            }
            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(failing == 0))
            {
                if (GUILayout.Button("Fix All", GUILayout.Height(28)))
                {
                    FixAllRunner.Start();
                }
            }
            if (failing == 0)
                EditorGUILayout.HelpBox("All required checks pass. You are ready to build for Quest.", MessageType.Info);
        }

        void RunFix(SetupCheck check)
        {
            try
            {
                check.Fix();
                _fixErrors.Remove(check);
            }
            catch (System.Exception e)
            {
                _fixErrors[check] = e.Message;
                Debug.LogError($"[Quest Setup] Fix failed for '{check.Label}': {e}");
            }
        }
    }

    /// <summary>Per-project EditorPrefs keys for the setup window.</summary>
    static class SetupKeys
    {
        // Delegates to OptionalRulePrefs.ProjectKey — single implementation of the
        // Application.dataPath → safe key-suffix transform used across the package.

        /// <summary>Set once the window has auto-opened for this project.</summary>
        public static string ShownKey => "Immrsiv.QuestSetup.SetupShown." + OptionalRulePrefs.ProjectKey;
    }

    /// <summary>Opens the setup window once per project after the package lands.</summary>
    [InitializeOnLoad]
    static class SetupWindowAutoOpen
    {
        static SetupWindowAutoOpen()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool(SetupKeys.ShownKey, false)) return;
                EditorPrefs.SetBool(SetupKeys.ShownKey, true);
                QuestSetupWindow.Open();
            };
        }
    }
}
