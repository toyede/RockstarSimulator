var view=UnityEngine.Object.FindFirstObjectByType<ContextStage.ResultNewspaperView>();
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var values=new System.Collections.Generic.List<object>();
foreach(var name in new[]{"articleTitle","articleBody","headline","subtitle"})
{
    var text=(TMPro.TMP_Text)view.GetType().GetField(name,flags).GetValue(view);
    text.ForceMeshUpdate();
    values.Add(new{name,text=text.text,font=text.fontSize,min=text.fontSizeMin,max=text.fontSizeMax,auto=text.enableAutoSizing,rect=text.rectTransform.rect.ToString(),position=text.rectTransform.anchoredPosition.ToString(),overflow=text.overflowMode.ToString(),height=text.preferredHeight,lines=text.textInfo.lineCount});
}
return values;
