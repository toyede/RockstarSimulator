using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ContextStage.EditorTools
{
    public static class AudienceRuntimeSetup
    {
        internal const string RuntimePrefabPath =
            "Assets/Prefabs/Audience/AudienceRuntime.prefab";

        [MenuItem("Tools/Audience/Create Or Update Audience Runtime Prefab")]
        public static void CreateOrUpdateRuntimePrefab()
        {
            EditorSetupUtility.EnsureFolder("Assets/Prefabs/Audience");

            AudienceEngagementConfig engagement =
                AssetDatabase.LoadAssetAtPath<AudienceEngagementConfig>(
                    AudienceFoundationSetup.EngagementConfigPath);
            AudienceFlowConfig flow =
                AssetDatabase.LoadAssetAtPath<AudienceFlowConfig>(
                    AudienceFoundationSetup.FlowConfigPath);
            GameObject memberAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    AudienceFoundationSetup.MemberPrefabPath);
            AudienceMemberActor memberPrefab =
                memberAsset != null
                    ? memberAsset.GetComponent<AudienceMemberActor>()
                    : null;
            if (engagement == null || flow == null || memberPrefab == null)
                throw new UnityException(
                    "[AudienceRuntimeSetup] Create the audience settings and " +
                    "member prefab before creating the runtime prefab.");

            bool loadedPrefabContents =
                AssetDatabase.LoadAssetAtPath<GameObject>(RuntimePrefabPath) != null;
            GameObject root = loadedPrefabContents
                ? PrefabUtility.LoadPrefabContents(RuntimePrefabPath)
                : new GameObject("[AudienceRuntime]");

            try
            {
                root.name = "[AudienceRuntime]";
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                AudienceRosterSystem roster =
                    GetOrAddComponent<AudienceRosterSystem>(root);
                AudienceRosterPresenter presenter =
                    GetOrAddComponent<AudienceRosterPresenter>(root);
                AudienceDebugInput debugInput =
                    GetOrAddComponent<AudienceDebugInput>(root);

                SetObjectField(roster, "engagementConfig", engagement);
                SetObjectField(roster, "flowConfig", flow);
                SetObjectField(presenter, "memberPrefab", memberPrefab);
                SetObjectField(presenter, "memberRoot", root.transform);
                roster.enabled = true;
                presenter.enabled = true;
                debugInput.enabled = true;

                PrefabUtility.SaveAsPrefabAsset(root, RuntimePrefabPath);
            }
            finally
            {
                if (loadedPrefabContents)
                    PrefabUtility.UnloadPrefabContents(root);
                else
                    UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[AudienceRuntimeSetup] Runtime prefab ready: {RuntimePrefabPath}");
        }

        [MenuItem("Tools/Audience/Analyze Active Scene")]
        public static void AnalyzeActiveScene()
        {
            Scene scene = RequireEditableActiveScene();
            List<AudienceRosterSystem> rosters =
                FindSceneComponents<AudienceRosterSystem>(scene);
            List<CardSystem> cards = FindSceneComponents<CardSystem>(scene);
            int enabledLegacyCount = CountEnabledLegacySystems(scene);
            Debug.Log(
                $"[AudienceRuntimeSetup] Scene '{scene.path}': " +
                $"rosters={rosters.Count}, cardSystems={cards.Count}, " +
                $"enabledLegacySystems={enabledLegacyCount}, " +
                $"runtimePrefabInstance={FindRuntimeInstanceRoot(scene) != null}.");
        }

        [MenuItem("Tools/Audience/Install Individual Audience Runtime")]
        public static void InstallInActiveScene()
        {
            Scene scene = RequireEditableActiveScene();
            bool changed = false;
            GameObject runtimePrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(RuntimePrefabPath);
            if (runtimePrefab == null)
                throw new UnityException(
                    "[AudienceRuntimeSetup] Create AudienceRuntime.prefab first.");

            List<CardSystem> cards = FindSceneComponents<CardSystem>(scene);
            if (cards.Count != 1)
                throw new UnityException(
                    $"[AudienceRuntimeSetup] Scene '{scene.path}' must contain " +
                    $"exactly one CardSystem, found {cards.Count}.");

            GameObject runtimeRoot = FindRuntimeInstanceRoot(scene);
            Vector3 installPosition = new Vector3(0f, 2f, 0f);
            Quaternion installRotation = Quaternion.identity;
            Vector3 installScale = Vector3.one;

            List<AudienceRosterSystem> existingRosters =
                FindSceneComponents<AudienceRosterSystem>(scene);
            if (runtimeRoot == null && existingRosters.Count > 0)
            {
                if (existingRosters.Count != 1)
                    throw new UnityException(
                        $"[AudienceRuntimeSetup] Scene '{scene.path}' contains " +
                        $"{existingRosters.Count} audience rosters.");

                GameObject stagedRoot = existingRosters[0].gameObject;
                if (!CanReplaceStagedRuntime(stagedRoot, out string reason))
                    throw new UnityException(
                        $"[AudienceRuntimeSetup] Cannot replace " +
                        $"'{stagedRoot.name}': {reason}");

                installPosition = stagedRoot.transform.position;
                installRotation = stagedRoot.transform.rotation;
                installScale = stagedRoot.transform.localScale;
                Undo.DestroyObjectImmediate(stagedRoot);
                changed = true;
            }
            else if (runtimeRoot == null && existingRosters.Count > 1)
            {
                throw new UnityException(
                    $"[AudienceRuntimeSetup] Scene '{scene.path}' contains " +
                    $"{existingRosters.Count} audience rosters.");
            }

            if (runtimeRoot == null)
            {
                runtimeRoot =
                    PrefabUtility.InstantiatePrefab(runtimePrefab, scene) as GameObject;
                if (runtimeRoot == null)
                    throw new UnityException(
                        "[AudienceRuntimeSetup] Failed to instantiate runtime prefab.");

                Undo.RegisterCreatedObjectUndo(
                    runtimeRoot,
                    "Install Individual Audience Runtime");
                runtimeRoot.transform.SetPositionAndRotation(
                    installPosition,
                    installRotation);
                runtimeRoot.transform.localScale = installScale;
                changed = true;
            }

            AudienceRosterSystem roster =
                runtimeRoot.GetComponent<AudienceRosterSystem>();
            AudienceRosterPresenter presenter =
                runtimeRoot.GetComponent<AudienceRosterPresenter>();
            if (roster == null || presenter == null)
                throw new UnityException(
                    "[AudienceRuntimeSetup] Runtime prefab components are missing.");

            changed |= SetObjectField(
                cards[0],
                "audienceRoster",
                roster,
                true);
            changed |= SetBehaviourEnabled(roster, true);
            changed |= SetBehaviourEnabled(presenter, true);
            AudienceDebugInput debugInput =
                runtimeRoot.GetComponent<AudienceDebugInput>();
            if (debugInput != null)
                changed |= SetBehaviourEnabled(debugInput, true);

            changed |= DisableLegacySceneSystems(scene);
            changed |= EnableSpecialAudienceSystems(scene, presenter);
            if (changed) EditorSceneManager.MarkSceneDirty(scene);
            ValidateScene(scene);
            Debug.Log(
                $"[AudienceRuntimeSetup] Installed the individual audience runtime " +
                $"in '{scene.path}'. " +
                $"{(changed ? "Review and save the scene." : "No scene changes were required.")}",
                runtimeRoot);
        }

        [MenuItem("Tools/Audience/Validate Active Scene")]
        public static void ValidateActiveScene()
        {
            Scene scene = RequireEditableActiveScene();
            ValidateScene(scene);
            Debug.Log(
                $"[AudienceRuntimeSetup] Scene validation passed: {scene.path}");
        }

        internal static void ValidateScene(Scene scene)
        {
            var failures = new List<string>();
            CollectSceneFailures(scene, failures);
            AudienceFoundationSetup.CollectCardProfileFailures(failures);
            if (failures.Count > 0)
                throw new InvalidOperationException(
                    $"Audience runtime validation failed for '{scene.path}':\n- " +
                    string.Join("\n- ", failures));
        }

        static void CollectSceneFailures(Scene scene, List<string> failures)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                failures.Add("The target scene is not loaded.");
                return;
            }

            GameObject runtimeRoot = FindRuntimeInstanceRoot(scene);
            if (runtimeRoot == null)
            {
                failures.Add(
                    $"AudienceRuntime.prefab is not instantiated in '{scene.path}'.");
                return;
            }

            List<AudienceRosterSystem> rosters =
                FindSceneComponents<AudienceRosterSystem>(scene);
            List<AudienceRosterPresenter> presenters =
                FindSceneComponents<AudienceRosterPresenter>(scene);
            List<CardSystem> cards = FindSceneComponents<CardSystem>(scene);
            if (rosters.Count != 1)
                failures.Add($"Expected one AudienceRosterSystem, found {rosters.Count}.");
            if (presenters.Count != 1)
                failures.Add(
                    $"Expected one AudienceRosterPresenter, found {presenters.Count}.");
            if (cards.Count != 1)
                failures.Add($"Expected one CardSystem, found {cards.Count}.");

            if (rosters.Count == 1)
            {
                AudienceRosterSystem roster = rosters[0];
                if (!roster.enabled)
                    failures.Add("AudienceRosterSystem is disabled.");
                if (roster.EngagementConfig == null)
                    failures.Add("AudienceEngagementConfig is not assigned.");
                if (roster.FlowConfig == null)
                    failures.Add("AudienceFlowConfig is not assigned.");

                if (cards.Count == 1 &&
                    GetObjectField<AudienceRosterSystem>(
                        cards[0],
                        "audienceRoster") != roster)
                {
                    failures.Add(
                        "CardSystem does not reference the scene AudienceRosterSystem.");
                }
            }

            if (presenters.Count == 1)
            {
                AudienceRosterPresenter presenter = presenters[0];
                if (!presenter.enabled)
                    failures.Add("AudienceRosterPresenter is disabled.");
                if (presenter.MemberPrefab == null)
                    failures.Add("AudienceMember prefab is not assigned.");
                if (GetObjectField<Transform>(presenter, "memberRoot") !=
                    runtimeRoot.transform)
                {
                    failures.Add(
                        "AudienceRosterPresenter member root is not the runtime root.");
                }

                ValidateSpecialAudienceSystems(scene, presenter, failures);
            }

            int enabledLegacyCount = CountEnabledLegacySystems(scene);
            if (enabledLegacyCount > 0)
                failures.Add(
                    $"{enabledLegacyCount} legacy audience/hype systems remain enabled.");
        }

        static Scene RequireEditableActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException(
                    "Audience scene setup cannot run in Play Mode.");

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
                throw new InvalidOperationException(
                    "Open and save the target scene before running audience setup.");
            return scene;
        }

        static GameObject FindRuntimeInstanceRoot(Scene scene)
        {
            List<AudienceRosterSystem> rosters =
                FindSceneComponents<AudienceRosterSystem>(scene);
            for (int i = 0; i < rosters.Count; i++)
            {
                GameObject root =
                    PrefabUtility.GetOutermostPrefabInstanceRoot(
                        rosters[i].gameObject);
                if (root == null) continue;
                string path =
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
                if (string.Equals(
                        path,
                        RuntimePrefabPath,
                        StringComparison.OrdinalIgnoreCase))
                    return root;
            }
            return null;
        }

        static bool CanReplaceStagedRuntime(
            GameObject root,
            out string reason)
        {
            if (root.transform.parent != null)
            {
                reason = "the staged runtime is not a scene root";
                return false;
            }
            if (root.transform.childCount > 0)
            {
                reason = "the staged runtime contains child objects";
                return false;
            }

            Component[] components = root.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component is Transform ||
                    component is AudienceRosterSystem ||
                    component is AudienceRosterPresenter ||
                    component is AudienceDebugInput)
                    continue;

                reason =
                    $"it contains the unrelated component " +
                    $"'{component.GetType().Name}'";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        static bool DisableLegacySceneSystems(Scene scene)
        {
            bool changed = false;
            changed |= SetSceneComponentsEnabled<CrowdSpawner>(scene, false);
            changed |= SetSceneComponentsEnabled<CrowdMoodDirector>(scene, false);
            changed |= SetSceneComponentsEnabled<CrowdCompositionManager>(
                scene,
                false);
            changed |= SetSceneComponentsEnabled<CrowdCompositionDebugView>(
                scene,
                false);
            changed |= SetSceneComponentsEnabled<CrowdShiftDirector>(scene, false);
            changed |= SetSceneComponentsEnabled<HypeSystem>(scene, false);
            changed |= SetSceneComponentsEnabled<HypeDebugInput>(scene, false);
            changed |= SetSceneComponentsEnabled<StageLightEventBridge>(
                scene,
                false);
            changed |= SetSceneComponentsEnabled<CrowdAmbienceSystem>(
                scene,
                false);
            changed |= SetSceneComponentsEnabled<CrowdAmbienceDebugInput>(
                scene,
                false);

            changed |= SetSceneObjectActive(
                scene,
                "HypeCanvas/HypeGauge",
                false);
            return changed;
        }

        static int CountEnabledLegacySystems(Scene scene) =>
            CountEnabled<CrowdSpawner>(scene) +
            CountEnabled<CrowdMoodDirector>(scene) +
            CountEnabled<CrowdCompositionManager>(scene) +
            CountEnabled<CrowdCompositionDebugView>(scene) +
            CountEnabled<CrowdShiftDirector>(scene) +
            CountEnabled<HypeSystem>(scene) +
            CountEnabled<HypeDebugInput>(scene) +
            CountEnabled<StageLightEventBridge>(scene) +
            CountEnabled<CrowdAmbienceSystem>(scene) +
            CountEnabled<CrowdAmbienceDebugInput>(scene);

        static bool EnableSpecialAudienceSystems(
            Scene scene,
            AudienceRosterPresenter presenter)
        {
            List<SpecialAudienceManager> managers =
                FindSceneComponents<SpecialAudienceManager>(scene);
            List<SpecialAudienceDropTarget> targets =
                FindSceneComponents<SpecialAudienceDropTarget>(scene);
            List<SpecialAudienceCrowdActor> actors =
                FindSceneComponents<SpecialAudienceCrowdActor>(scene);
            if (managers.Count != 1 || targets.Count != 1 || actors.Count != 1)
            {
                throw new InvalidOperationException(
                    "The scene requires exactly one SpecialAudienceManager, " +
                    "SpecialAudienceDropTarget, and SpecialAudienceCrowdActor.");
            }

            bool changed = false;
            changed |= SetSceneObjectActive(
                scene,
                "[Crowd]/SpecialAudience",
                true);
            changed |= SetSceneObjectActive(
                scene,
                "SpecialAudienceCanvas",
                true);
            changed |= SetBehaviourEnabled(managers[0], true);
            changed |= SetBehaviourEnabled(targets[0], true);
            changed |= SetObjectField(
                actors[0],
                "audiencePresenter",
                presenter,
                true);
            return changed;
        }

        static void ValidateSpecialAudienceSystems(
            Scene scene,
            AudienceRosterPresenter presenter,
            List<string> failures)
        {
            List<SpecialAudienceManager> managers =
                FindSceneComponents<SpecialAudienceManager>(scene);
            List<SpecialAudienceDropTarget> targets =
                FindSceneComponents<SpecialAudienceDropTarget>(scene);
            List<SpecialAudienceCrowdActor> actors =
                FindSceneComponents<SpecialAudienceCrowdActor>(scene);

            if (managers.Count != 1)
                failures.Add(
                    $"Expected one SpecialAudienceManager, found {managers.Count}.");
            else if (!managers[0].isActiveAndEnabled)
                failures.Add("SpecialAudienceManager is disabled.");

            if (targets.Count != 1)
                failures.Add(
                    $"Expected one SpecialAudienceDropTarget, found {targets.Count}.");
            else if (!targets[0].isActiveAndEnabled)
                failures.Add("SpecialAudienceDropTarget is disabled.");

            if (actors.Count != 1)
            {
                failures.Add(
                    $"Expected one SpecialAudienceCrowdActor, found {actors.Count}.");
            }
            else if (actors[0].AudiencePresenter != presenter)
            {
                failures.Add(
                    "SpecialAudienceCrowdActor is not bound to " +
                    "AudienceRosterPresenter.");
            }
        }

        static int CountEnabled<T>(Scene scene) where T : Behaviour
        {
            int count = 0;
            List<T> components = FindSceneComponents<T>(scene);
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i].enabled) count++;
            }
            return count;
        }

        static bool SetSceneComponentsEnabled<T>(Scene scene, bool value)
            where T : Behaviour
        {
            bool changed = false;
            List<T> components = FindSceneComponents<T>(scene);
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i].enabled == value) continue;
                Undo.RecordObject(components[i], "Disable Legacy Audience System");
                components[i].enabled = value;
                EditorUtility.SetDirty(components[i]);
                changed = true;
            }
            return changed;
        }

        static List<T> FindSceneComponents<T>(Scene scene)
            where T : Component
        {
            var results = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T[] components = roots[i].GetComponentsInChildren<T>(true);
                results.AddRange(components);
            }
            return results;
        }

        static bool SetSceneObjectActive(
            Scene scene,
            string hierarchyPath,
            bool value)
        {
            Transform target = FindSceneTransform(scene, hierarchyPath);
            if (target == null || target.gameObject.activeSelf == value)
                return false;
            Undo.RecordObject(target.gameObject, "Update Legacy Audience Object");
            target.gameObject.SetActive(value);
            EditorUtility.SetDirty(target.gameObject);
            return true;
        }

        static Transform FindSceneTransform(Scene scene, string hierarchyPath)
        {
            string[] segments = hierarchyPath.Split('/');
            GameObject[] roots = scene.GetRootGameObjects();
            Transform current = null;
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name != segments[0]) continue;
                current = roots[i].transform;
                break;
            }
            if (current == null) return null;

            for (int i = 1; i < segments.Length; i++)
            {
                current = current.Find(segments[i]);
                if (current == null) return null;
            }
            return current;
        }

        static T GetOrAddComponent<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        static bool SetObjectField(
            UnityEngine.Object target,
            string fieldName,
            UnityEngine.Object value,
            bool recordUndo = false)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
                throw new InvalidOperationException(
                    $"Missing serialized field '{fieldName}' on " +
                    $"{target.GetType().Name}.");
            if (property.objectReferenceValue == value)
                return false;

            if (recordUndo) Undo.RecordObject(target, $"Set {fieldName}");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            return true;
        }

        static bool SetBehaviourEnabled(Behaviour behaviour, bool value)
        {
            if (behaviour.enabled == value) return false;
            Undo.RecordObject(behaviour, "Set Audience Runtime Enabled");
            behaviour.enabled = value;
            EditorUtility.SetDirty(behaviour);
            return true;
        }

        static T GetObjectField<T>(
            UnityEngine.Object target,
            string fieldName)
            where T : UnityEngine.Object
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            return property != null ? property.objectReferenceValue as T : null;
        }
    }
}
