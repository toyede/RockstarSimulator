if (UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before setup");
if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/Main.unity") throw new System.InvalidOperationException("Main must be the inspected active scene");
const string materialPath = "Assets/Resources/Effects/AudienceWarningUnlit.mat";
if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources/Effects")) UnityEditor.AssetDatabase.CreateFolder("Assets/Resources", "Effects");
var material = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(materialPath);
if (material == null)
{
    var shader = UnityEngine.Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
    if (shader == null) throw new System.InvalidOperationException("URP Sprite Unlit shader unavailable");
    material = new UnityEngine.Material(shader) { name = "AudienceWarningUnlit" };
    UnityEditor.AssetDatabase.CreateAsset(material, materialPath);
}
var rain = UnityEngine.Object.FindFirstObjectByType<ContextStage.RainShowerRule>(UnityEngine.FindObjectsInactive.Include);
if (rain == null) throw new System.InvalidOperationException("Existing RainShowerRule missing");
var rainView = rain.GetComponent<ContextStage.RainShowerVFX>();
if (rainView == null) rainView = UnityEditor.Undo.AddComponent<ContextStage.RainShowerVFX>(rain.gameObject);
UnityEditor.EditorUtility.SetDirty(rain.gameObject);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rain.gameObject.scene);
var prefab = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/Prefabs/Audience/AudienceMember.prefab");
try
{
    var actor = prefab.GetComponent<ContextStage.AudienceMemberActor>();
    var data = new UnityEditor.SerializedObject(actor);
    var spread = data.FindProperty("moshSpreadDistance");
    bool migrated = UnityEngine.Mathf.Approximately(spread.floatValue, 0.45f);
    if (migrated) spread.floatValue = 1f; // migrate only the previous default, preserve custom tuning
    foreach (var propertyName in new[] { "warningFillRenderer", "warningBackgroundRenderer" })
    {
        var renderer = data.FindProperty(propertyName).objectReferenceValue as UnityEngine.SpriteRenderer;
        if (renderer != null) renderer.sharedMaterial = material;
    }
    data.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/Prefabs/Audience/AudienceMember.prefab");
}
finally { UnityEditor.PrefabUtility.UnloadPrefabContents(prefab); }
UnityEditor.AssetDatabase.SaveAssets();
return new { material = materialPath, shader = material.shader.name, rain = rainView.gameObject.name, sceneDirty = rainView.gameObject.scene.isDirty };
