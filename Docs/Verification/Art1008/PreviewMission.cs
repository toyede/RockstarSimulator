if(!UnityEditor.EditorApplication.isPlaying)throw new System.InvalidOperationException("Play Mode only");
var canvasObject=new UnityEngine.GameObject("Art1008MissionPreview",typeof(UnityEngine.RectTransform),typeof(UnityEngine.Canvas),typeof(UnityEngine.UI.CanvasScaler));
var canvas=canvasObject.GetComponent<UnityEngine.Canvas>();canvas.renderMode=UnityEngine.RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1000;
var scaler=canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new UnityEngine.Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
var font=UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Font/DungGeunMo SDF.asset");
var root=new UnityEngine.GameObject("Mission",typeof(UnityEngine.RectTransform)).GetComponent<UnityEngine.RectTransform>();root.SetParent(canvasObject.transform,false);root.anchorMin=root.anchorMax=new UnityEngine.Vector2(0,.5f);root.pivot=new UnityEngine.Vector2(0,.5f);root.anchoredPosition=new UnityEngine.Vector2(24,0);root.sizeDelta=new UnityEngine.Vector2(512,384);
var view=ContextStage.MissionCueSheetView.Create(root,font);
view.Title.text="BEATMATCH";view.Timer.text="4.8s";view.Body.text="박자를 놓치지 마 — 콤보를 지키며 카드 2장!";view.Progress.text="0 / 2";view.SetSecondary("");
return new{sprite=view.GetComponent<UnityEngine.UI.Image>().sprite.name,point=view.GetComponent<UnityEngine.UI.Image>().sprite.texture.filterMode.ToString(),bodyColor=view.Body.color.ToString()};
