// Read-only asset diagnostic: never save prefab contents or modify open scenes.
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var root = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/Prefabs/Audience/AudienceMember.prefab");
try
{
    var actor = root.GetComponent<ContextStage.AudienceMemberActor>();
    var serialized = new UnityEditor.SerializedObject(actor);
    var fill = (UnityEngine.SpriteRenderer)serialized.FindProperty("warningFillRenderer").objectReferenceValue;
    var background = (UnityEngine.SpriteRenderer)serialized.FindProperty("warningBackgroundRenderer").objectReferenceValue;
    var before = new { shader = fill.sharedMaterial.shader.name, material = UnityEditor.AssetDatabase.GetAssetPath(fill.sharedMaterial), fillSprite = fill.sprite.name, fillBounds = fill.sprite.bounds.size.ToString(), fillParentScale = fill.transform.parent.localScale.ToString() };
    actor.GetType().GetMethod("Awake", flags).Invoke(actor, null);
    actor.GetType().GetField("_calmUpperBound", flags).SetValue(actor, 33f);
    var checks = new System.Collections.Generic.List<object>();
    foreach (float value in new[] {33f, 32f, 25f, 15f, 5f, 1f})
    {
        actor.GetType().GetField("_snapshot", flags).SetValue(actor,
            new ContextStage.AudienceSnapshot(new ContextStage.AudienceId(1), ContextStage.CrowdPreference.Chill, value, ContextStage.AudienceEngagementStage.Calm, 0f));
        actor.GetType().GetMethod("UpdateWarning", flags).Invoke(actor, null);
        checks.Add(new { engagement = value, fraction = fill.transform.localScale.x, localX = fill.transform.localPosition.x, active = fill.gameObject.activeInHierarchy, bounds = fill.bounds.size.ToString(), alpha = fill.color.a, spriteSize = fill.sprite.bounds.size.ToString(), backgroundWidth = background.bounds.size.x });
    }
    var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.CardCatalog>("Assets/Resources/Cards/CardCatalog.asset");
    var lights = UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)
        .Where(l=>l.gameObject.scene.name=="Main").Select(l=> {
            var data = new UnityEditor.SerializedObject(l);
            var layers = data.FindProperty("m_ApplyToSortingLayers");
            return new { l.name, active = l.isActiveAndEnabled, type = l.lightType.ToString(), l.intensity,
                layers = layers == null ? null : Enumerable.Range(0,layers.arraySize).Select(i=>UnityEngine.SortingLayer.IDToName(layers.GetArrayElementAtIndex(i).intValue)).ToArray() };
        }).ToArray();
    var cardAssets = UnityEditor.AssetDatabase.GetDependencies("Assets/Resources/Cards/CardCatalog.asset").Where(p=>p.EndsWith(".prefab")).ToArray();
    var cards = cardAssets.Select(p=>UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(p).GetComponent<ContextStage.CardDefinition>())
        .Where(c=>c!=null && (c.Id=="tempo_up" || c.Id=="hands_up" || c.Id=="open_mosh_pit"))
        .Select(c=>new { c.Id, c.DisplayName, preference = c.TargetPreference.ToString() }).ToArray();
    return new { before, checks, lights, sortingLayers = UnityEngine.SortingLayer.layers.Select(l=>new{l.id,l.name}).ToArray(), serializedMainExtraLayer = UnityEngine.SortingLayer.IDToName(System.Convert.ToInt32("6e4f2a19",16)), cards };
}
finally
{
    UnityEditor.PrefabUtility.UnloadPrefabContents(root);
}
