var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(UnityEditor.EditorApplication.isPlaying || scene.path!="Assets/Scenes/Main.unity")
    throw new System.Exception("Apply only to the existing Main scene in Edit Mode");
var overlay=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>(UnityEngine.FindObjectsInactive.Include);
var tso=new UnityEditor.SerializedObject(overlay);
var panel=(UnityEngine.GameObject)tso.FindProperty("panelRoot").objectReferenceValue;
var pr=panel.GetComponent<UnityEngine.RectTransform>();
UnityEditor.Undo.RecordObject(pr,"Increase tutorial paper height");
pr.SetSizeWithCurrentAnchors(UnityEngine.RectTransform.Axis.Vertical,440);
tso.FindProperty("titleFontSize").intValue=36;
tso.FindProperty("titleMinFontSize").intValue=30;
tso.FindProperty("bodyFontSize").intValue=28;
tso.FindProperty("bodyMinFontSize").intValue=24;
tso.FindProperty("continueFontSize").intValue=22;
tso.ApplyModifiedProperties();
var boss=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossBattleUI>(UnityEngine.FindObjectsInactive.Include);
var bso=new UnityEditor.SerializedObject(boss);
var size=bso.FindProperty("patternPanelSize").vector2Value; size.y=384;
bso.FindProperty("patternPanelSize").vector2Value=size; bso.ApplyModifiedProperties();
var view=UnityEngine.Object.FindFirstObjectByType<ContextStage.ResultNewspaperView>(UnityEngine.FindObjectsInactive.Include);
var so=new UnityEditor.SerializedObject(view);
so.FindProperty("paperRestTilt").floatValue=0;
so.ApplyModifiedProperties();
UnityEngine.RectTransform FieldRect(string name) {
    var obj=so.FindProperty(name).objectReferenceValue as UnityEngine.Component;
    return obj?.GetComponent<UnityEngine.RectTransform>();
}
void Place(UnityEngine.RectTransform r, UnityEngine.Vector2 position, UnityEngine.Vector2 dimensions) {
    if(r==null)return; UnityEditor.Undo.RecordObject(r,"Result typography spacing");
    r.anchoredPosition=position; r.sizeDelta=dimensions;
}
void Label(string field,float max,float min,UnityEngine.Vector2? position=null,UnityEngine.Vector2? dimensions=null) {
    var t=so.FindProperty(field).objectReferenceValue as TMPro.TMP_Text;
    if(t==null)return; UnityEditor.Undo.RecordObject(t,"Readable result type");
    t.fontStyle &= ~TMPro.FontStyles.Bold; t.outlineWidth=0; t.extraPadding=true;
    t.fontSize=t.fontSizeMax=max;t.fontSizeMin=min;t.enableAutoSizing=false;
    if(position.HasValue||dimensions.HasValue)Place(t.rectTransform,position??t.rectTransform.anchoredPosition,dimensions??t.rectTransform.sizeDelta);
    UnityEditor.EditorUtility.SetDirty(t);
}
Label("headline",40,32,new UnityEngine.Vector2(-135,193),new UnityEngine.Vector2(1050,58));
Label("subtitle",24,22,new UnityEngine.Vector2(-135,148),new UnityEngine.Vector2(1050,34));
Label("mastheadInfo",24,22);
Label("caption",22,20);
Label("stampText",32,32,UnityEngine.Vector2.zero,new UnityEngine.Vector2(320,60));
var stamp=FieldRect("stamp"); Place(stamp,new UnityEngine.Vector2(419,-12),new UnityEngine.Vector2(320,60));
for(int i=0;i<stamp.childCount;i++) {
    var r=stamp.GetChild(i) as UnityEngine.RectTransform;
    if(r.name=="Top") Place(r,new UnityEngine.Vector2(0,27.5f),new UnityEngine.Vector2(320,5));
    if(r.name=="Bottom") Place(r,new UnityEngine.Vector2(0,-27.5f),new UnityEngine.Vector2(320,5));
    if(r.name=="Left") Place(r,new UnityEngine.Vector2(-157.5f,0),new UnityEngine.Vector2(5,60));
    if(r.name=="Right") Place(r,new UnityEngine.Vector2(157.5f,0),new UnityEngine.Vector2(5,60));
}
Place(FieldRect("rankImage"),new UnityEngine.Vector2(250,-99),new UnityEngine.Vector2(106,106));
Label("scoreText",40,30,new UnityEngine.Vector2(500,-74),new UnityEngine.Vector2(340,52));
Label("targetText",22,22,new UnityEngine.Vector2(500,-113),new UnityEngine.Vector2(340,30));
Label("ratioText",22,22,new UnityEngine.Vector2(500,-140),new UnityEngine.Vector2(340,30));
Label("statCombo",24,24);Label("statFever",24,24);Label("statAudience",24,24);
Label("articleTitle",24,22,new UnityEngine.Vector2(-230,-342),new UnityEngine.Vector2(840,28));
Label("articleBody",22,20,new UnityEngine.Vector2(-230,-377),new UnityEngine.Vector2(840,46));
Label("primaryLabel",30,28);
UnityEditor.EditorUtility.SetDirty(view);
var canvas=view.GetComponent<UnityEngine.Canvas>();
if(canvas!=null){UnityEditor.Undo.RecordObject(canvas,"Pixel aligned newspaper");canvas.pixelPerfect=true;}
var tutorialCanvas=overlay.PanelImage.canvas;
if(tutorialCanvas!=null){UnityEditor.Undo.RecordObject(tutorialCanvas,"Pixel aligned tutorial");tutorialCanvas.pixelPerfect=true;}
var catalog=ContextStage.ResultNewspaperCatalog.LoadDefault();
var cso=new UnityEditor.SerializedObject(catalog);var skins=cso.FindProperty("skins");
for(int i=0;i<skins.arraySize;i++) {
    var skin=skins.GetArrayElementAtIndex(i);var id=skin.FindPropertyRelative("stageId").stringValue;
    if(id=="stage_04")skin.FindPropertyRelative("headlineOffset").vector2Value=new UnityEngine.Vector2(0,-38);
    if(id=="stage_05_boss")skin.FindPropertyRelative("headlineOffset").vector2Value=new UnityEngine.Vector2(0,-20);
}
cso.ApplyModifiedProperties();UnityEditor.EditorUtility.SetDirty(catalog);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEditor.AssetDatabase.SaveAssets();
return "Saved tutorial 440px, boss mission 384/480px, flat readable newspaper and skin spacing; existing UI references preserved";
