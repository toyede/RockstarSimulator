var ui=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossBattleUI>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
return new{screen=new[]{UnityEngine.Screen.width,UnityEngine.Screen.height},images=new[]{"_barBack","_ourFill","_rivalFill"}.Select(n=>{
 var image=(UnityEngine.UI.Image)ui.GetType().GetField(n,flags).GetValue(ui);
 return new{name=n,amount=image.fillAmount,origin=image.fillOrigin,color=image.color.ToString(),rect=image.rectTransform.rect.ToString(),sprite=image.sprite.name,canvas=image.canvas.scaleFactor};}).ToArray()};
