var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
object Read(object owner, string field) => owner.GetType().GetField(field, flags)?.GetValue(owner);
object Rect(UnityEngine.RectTransform rect) => rect == null ? null : new {
    name = rect.name, size = new[] {rect.rect.width, rect.rect.height},
    anchor = new[] {rect.anchorMin.x, rect.anchorMin.y},
    pivot = new[] {rect.pivot.x, rect.pivot.y},
    position = new[] {rect.anchoredPosition.x, rect.anchoredPosition.y}
};
var panels = new System.Collections.Generic.List<object>();
foreach (var panel in UnityEngine.Object.FindObjectsByType<ContextStage.DialoguePanel>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)) {
    var arrow = (UnityEngine.UI.Image)Read(panel, "arrowImage");
    var box = (UnityEngine.UI.Image)Read(panel, "boxBackground");
    var body = (TMPro.TMP_Text)Read(panel, "bodyText");
    panels.Add(new {
        scene = panel.gameObject.scene.path, panel.name, active = panel.gameObject.activeInHierarchy,
        state = panel.DebugState, line = panel.CurrentLineIndex,
        followsFieldExists = panel.GetType().GetField("arrowFollowsText", flags) != null,
        legacyFollowsValue = Read(panel, "arrowFollowsText"),
        arrow = Rect(arrow?.rectTransform), arrowVisible = arrow != null && arrow.enabled,
        parent = arrow == null ? null : arrow.transform.parent.name,
        box = Rect(box?.rectTransform), text = body == null ? null : body.text
    });
}
return Newtonsoft.Json.JsonConvert.SerializeObject(new {
    playing = UnityEditor.EditorApplication.isPlaying,
    paused = UnityEditor.EditorApplication.isPaused,
    activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
    panels
});
