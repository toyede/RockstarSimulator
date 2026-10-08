var rain = UnityEngine.Object.FindFirstObjectByType<ContextStage.RainShowerRule>(UnityEngine.FindObjectsInactive.Include);
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/Audience/AudienceMember.prefab");
var data = new UnityEditor.SerializedObject(prefab.GetComponent<ContextStage.AudienceMemberActor>());
var fill = data.FindProperty("warningFillRenderer").objectReferenceValue as UnityEngine.SpriteRenderer;
var back = data.FindProperty("warningBackgroundRenderer").objectReferenceValue as UnityEngine.SpriteRenderer;
return new { rainAttached = rain != null && rain.GetComponent<ContextStage.RainShowerVFX>() != null,
    fillShader = fill.sharedMaterial.shader.name, backShader = back.sharedMaterial.shader.name,
    spread = data.FindProperty("moshSpreadDistance").floatValue,
    dirty = UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty };
