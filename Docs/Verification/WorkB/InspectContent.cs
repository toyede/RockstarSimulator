var result = new System.Collections.Generic.List<string>();
result.Add("scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
foreach (var view in UnityEngine.Object.FindObjectsByType<ContextStage.ResultNewspaperView>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
    result.Add("result=" + view.name + "|position=" + view.transform.localPosition);
var sound = UnityEngine.Resources.Load<GameJamKit.SoundLibrary>("SoundLibrary");
foreach (string id in new[] { "breakdown", "ui_click_wooden", "card_open_mosh_pit", "guitar_stroke" })
{
    var entry = sound != null ? sound.Find(id) : null;
    result.Add("sound=" + id + "|exists=" + (entry != null) + "|clip=" + (entry != null && entry.clips.Length > 0 && entry.clips[0] != null ? entry.clips[0].name + ":" + entry.clips[0].length : "missing"));
}
foreach (string id in new[] { "intro_stage_01", "intro_stage_02", "intro_stage_03", "intro_stage_04", "intro_stage_05_boss" })
{
    var seq = UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.DialogueSequence>("Assets/Settings/Dialogue/Sequences/" + id + ".asset");
    if (seq != null) foreach (var line in seq.Lines) result.Add(id + "|" + line.speakerId + ":" + line.text);
}
return result.ToArray();
