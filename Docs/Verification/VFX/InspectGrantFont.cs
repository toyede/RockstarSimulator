var prefab=UnityEngine.Resources.Load<ContextStage.AugmentSelectionPopup>("UI/AugmentSelectionPopup");
var label=prefab.GetComponentInChildren<UnityEngine.UI.Text>(true);
return new {font=label.font.name,fontPath=UnityEditor.AssetDatabase.GetAssetPath(label.font)};
