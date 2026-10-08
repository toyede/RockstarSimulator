var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var panel = UnityEngine.Object.FindFirstObjectByType<ContextStage.DialoguePanel>(UnityEngine.FindObjectsInactive.Include);
if (panel == null || panel.gameObject.scene.path != "Assets/Scenes/TourHub.unity" || !panel.IsPlaying)
    throw new System.Exception("Actual TourHub dialogue is required");
var type = panel.GetType();
object Read(string field) => type.GetField(field, flags).GetValue(panel);
var sequence = (ContextStage.DialogueSequence)Read("_sequence");
var image = (UnityEngine.UI.Image)Read("arrowImage");
var box = (UnityEngine.UI.Image)Read("boxBackground");
var body = (TMPro.TMP_Text)Read("bodyText");
var style = (ContextStage.DialogueStyle)Read("style");
var show = type.GetMethod("ShowLine", flags);
var complete = type.GetMethod("OnLineComplete", flags);
var passed = new System.Collections.Generic.List<string>();
void Require(bool condition, string label) {
    if (!condition) throw new System.Exception(label);
    passed.Add(label);
}
void CheckCorner(string label) {
    Require(image.enabled, label + ": shown after completion");
    Require(image.transform.parent == box.transform, label + ": parent is dialogue art");
    Require(image.rectTransform.anchorMin == new UnityEngine.Vector2(1, 0) &&
            image.rectTransform.anchorMax == new UnityEngine.Vector2(1, 0) &&
            image.rectTransform.pivot == new UnityEngine.Vector2(1, 0), label + ": bottom-right anchor");
    Require(image.rectTransform.anchoredPosition == style.ArrowOffset, label + ": fixed corner position");
}
int restoreIndex = panel.CurrentLineIndex;
try {
    Require(type.GetField("arrowFollowsText", flags) == null, "Legacy scene setting cannot enable text following");
    for (int i = 0; i < sequence.LineCount; i++) {
        show.Invoke(panel, new object[] {i});
        Require(!image.enabled, "Native line " + i + ": hidden while typing");
        panel.Advance();
        CheckCorner("Native line " + i);
    }
    foreach (var text in new[] {"네.", "", "관객이 어떤 공연을 기다리는지 살펴보자.\n관객 구성이 바뀌면 좋은 행동 카드도 달라진다.\n서로 다른 길이의 대사에서도 화살표는 흰 모서리에 고정된다."}) {
        body.text = text;
        body.ForceMeshUpdate();
        complete.Invoke(panel, null);
        CheckCorner("Text length " + text.Length);
    }
    Require(UnityEngine.Vector2.Distance(style.ArrowOffset, new UnityEngine.Vector2(-80, 42)) < 0.01f,
            "Default project style uses the verified white-corner offset");
} finally {
    show.Invoke(panel, new object[] {restoreIndex});
    if (panel.IsTyping) panel.Advance();
}
return Newtonsoft.Json.JsonConvert.SerializeObject(new {scene = panel.gameObject.scene.path,
    sequence = sequence.SequenceId, nativeLines = sequence.LineCount, passed = passed.Count, checks = passed});
