using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>부분 디스토션 공용 에셋과 Minyoung2 데모를 안전하게 생성·업데이트한다.</summary>
    public static class PartialDistortionSetupTool
    {
        const string ScenePath = "Assets/Scenes/Minyoung2.unity";
        const string MaterialFolder = "Assets/Materials/Effects";
        const string MaterialPath = MaterialFolder + "/PartialDistortion.mat";
        const string ProfileFolder = "Assets/Settings/Effects";
        const string ProfilePath = ProfileFolder + "/DemoShockwaveDistortion.asset";
        const string ShaderName = "ContextStage/Effects/Local Screen Effect";
        const string DemoName = "[Effects] Partial Distortion Demo";

        [MenuItem("Tools/Effects/Setup Partial Distortion In Minyoung2")]
        public static void Setup()
        {
            EnsureEffectsSortingLayer();
            EnsureFolder(MaterialFolder);
            EnsureFolder(ProfileFolder);

            Material material = GetOrCreateMaterial();
            DistortionProfile profile = GetOrCreateProfile();
            if (material == null || profile == null) return;

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            GameObject demo = scene.GetRootGameObjects().FirstOrDefault(go => go.name == DemoName);
            if (demo == null)
            {
                demo = new GameObject(DemoName, typeof(MeshFilter), typeof(MeshRenderer));
                SceneManager.MoveGameObjectToScene(demo, scene);
            }

            demo.transform.position = new Vector3(0f, 1.2f, 0f);
            demo.transform.rotation = Quaternion.identity;

            var meshFilter = EnsureComponent<MeshFilter>(demo);
            var meshRenderer = EnsureComponent<MeshRenderer>(demo);
            var effect = EnsureComponent<PartialDistortionEffect>(demo);
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingLayerName = "Effects";
            meshRenderer.sortingOrder = 0;

            var serialized = new SerializedObject(effect);
            serialized.FindProperty("profile").objectReferenceValue = profile;
            serialized.FindProperty("sourceMaterial").objectReferenceValue = material;
            serialized.FindProperty("areaSize").vector2Value = new Vector2(6f, 4f);
            serialized.FindProperty("playOnEnable").boolValue = true;
            serialized.FindProperty("loop").boolValue = true;
            serialized.FindProperty("loopDelay").floatValue = 0.35f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(meshFilter);
            EditorUtility.SetDirty(meshRenderer);
            EditorUtility.SetDirty(effect);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            if (openedHere) EditorSceneManager.CloseScene(scene, removeScene: true);

            Selection.activeObject = profile;
            Debug.Log($"[Distortion] Minyoung2 데모 구성 완료: {ScenePath}");
        }

        static Material GetOrCreateMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[Distortion] 셰이더를 찾지 못했습니다: {ShaderName}");
                return null;
            }

            if (material == null)
            {
                material = new Material(shader) { name = "PartialDistortion" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        static DistortionProfile GetOrCreateProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<DistortionProfile>(ProfilePath);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<DistortionProfile>();
            profile.name = "DemoShockwaveDistortion";
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        static void EnsureEffectsSortingLayer()
        {
            Object tagManagerAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")
                .FirstOrDefault();
            if (tagManagerAsset == null)
            {
                Debug.LogError("[Distortion] TagManager.asset을 찾지 못해 Effects Sorting Layer를 만들 수 없습니다.");
                return;
            }

            var tagManager = new SerializedObject(tagManagerAsset);
            SerializedProperty layers = tagManager.FindProperty("m_SortingLayers");
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty entry = layers.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("name").stringValue == "Effects") return;
            }

            layers.InsertArrayElementAtIndex(layers.arraySize);
            SerializedProperty added = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            added.FindPropertyRelative("name").stringValue = "Effects";
            added.FindPropertyRelative("uniqueID").intValue = unchecked((int)0x6E4F2A19);
            added.FindPropertyRelative("locked").boolValue = false;
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

    }
}
