var output=new System.Collections.Generic.List<object>();
foreach(string name in new[]{"MissionSlate","TutorialPaper"}) {
 string path="Assets/Resources/UI/CueSheets/"+name+".png";
 var tex=new UnityEngine.Texture2D(2,2);
 UnityEngine.ImageConversion.LoadImage(tex,System.IO.File.ReadAllBytes(path));
 var pixels=tex.GetPixels32();int transparent=0,opaque=0,partial=0;
 foreach(var p in pixels){if(p.a==0)transparent++;else if(p.a==255)opaque++;else partial++;}
 var importer=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
 output.Add(new{name,width=tex.width,height=tex.height,transparent,opaque,partial,point=importer.filterMode==UnityEngine.FilterMode.Point,noMips=!importer.mipmapEnabled});
 UnityEngine.Object.DestroyImmediate(tex);
}
return Newtonsoft.Json.JsonConvert.SerializeObject(output);
