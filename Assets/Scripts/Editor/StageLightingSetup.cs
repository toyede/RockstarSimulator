using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextStage.EditorTools
{
    /// <summary>새 조명 자식과 참조만 추가한다. 기존 StageSet 배치/UI/게임 규칙을 재생성하지 않는다.</summary>
    public static class StageLightingSetup
    {
        const string Root = "Assets/Settings/Lighting";
        const string Art = "Assets/Sprites/Lighting";
        [MenuItem("Tools/Lighting/Apply Venue Cues V3")]
        public static void ApplyVenueCuesV3()
        {
            if(EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Main")
                throw new InvalidOperationException("Main 씬 Edit Mode에서 실행하세요.");
            var square=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/LED_Square.asset");
            if(square==null)throw new InvalidOperationException("기존 조명 Square 없음");
            var rimMaterial=Material("BandRim","ContextStage/Lighting/Band Rim Light");
            var rigs=UnityEngine.Object.FindObjectsByType<StageLightingRig>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            foreach(var rig in rigs)
            {
                var p=rig.Profile;if(p==null)continue;
                Undo.RecordObject(p,"Venue lighting cues");
                p.feverBoost=1.3f;p.washAlpha=0.12f;p.washPeakAlpha=0.24f;
                p.washCueDuration=1;p.washCueCooldown=3;
                p.audienceSweepInterval=12;p.audienceSweepDuration=2.5f;
                p.studioStepSeconds=0.22f;p.rimDuration=0.8f;p.rimStrength=0.5f;
                p.dropQuietFraction=0.45f;p.normalLasers=4;p.dropLasers=6;p.peakLasers=8;
                // 정렬을 월드 위로 옮긴 뒤 캐릭터가 하얗게 날아가지 않는 강도로 조정.
                p.beamIntensity=p.rival?0.26f:p.venue==2?0.22f:p.venue==4?0.27f:0.30f;
                EditorUtility.SetDirty(p);
                SpriteRenderer wash=null;PixelLaserRenderer sweep=null,center=null;
                if(p.venue==2)
                {
                    var old=rig.transform.Find("AudienceWash");
                    wash=old!=null?old.GetComponent<SpriteRenderer>():Prop(rig.transform,"AudienceWash",square,Vector2.zero,new Vector2(19.2f,10.8f),p.fixtureMaterial);
                    Undo.RecordObject(wash,"World wash sorting");wash.sortingLayerName="Effects";wash.sortingOrder=-150;wash.color=Color.clear;
                }
                if(p.venue==3)
                {
                    var old=rig.transform.Find("AudienceSweep");
                    sweep=old!=null?old.GetComponent<PixelLaserRenderer>():Beam(rig.transform,"AudienceSweep",-7,4.8f,220,13,5,p.beamMaterial);
                }
                if(p.venue==4)
                {
                    var old=rig.transform.Find("StudioCenter");
                    center=old!=null?old.GetComponent<PixelLaserRenderer>():Beam(rig.transform,"StudioCenter",0,4.2f,180,11,7,p.beamMaterial);
                }
                if(p.rival)
                {
                    var thin=new List<PixelLaserRenderer>();
                    for(int i=0;i<8;i++)
                    {
                        var old=rig.transform.Find("Laser_"+i);
                        var item=old!=null?old.GetComponent<PixelLaserRenderer>():Beam(rig.transform,"Laser_"+i,i%2==0?-7.6f:7.6f,0.8f,i%2==0?-40:40,8,0.12f,p.laserMaterial);
                        Undo.RecordObject(item,"Expand LUX lasers");item.gameObject.SetActive(true);
                        item.EditorConfigure(p.laserMaterial,8,0.12f);thin.Add(item);
                    }
                    var so=new SerializedObject(rig);
                    Undo.RecordObject(rig,"Connect LUX lasers");
                    rig.EditorConfigure(p,ReadArray<PixelLaserRenderer>(so,"beams"),thin.ToArray(),ReadArray<SpriteRenderer>(so,"leds"),ReadArray<SpriteRenderer>(so,"fixtures"),ReadArray<ParticleSystem>(so,"smoke"),so.FindProperty("onAir").objectReferenceValue as TMP_Text);
                }
                Undo.RecordObject(rig,"Connect venue cues");rig.EditorConfigureVenue(wash,sweep,center);EditorUtility.SetDirty(rig);
            }
            var rims=new List<BandRimLightView>();
            foreach(var source in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(source.gameObject.scene.name!="Main" || (source.name!="Raccoon" && !source.name.StartsWith("Friends")))continue;
                var view=source.GetComponent<BandRimLightView>()??Undo.AddComponent<BandRimLightView>(source.gameObject);
                Undo.RecordObject(view,"Connect band rim");view.EditorConfigure(source,rimMaterial);EditorUtility.SetDirty(view);rims.Add(view);
            }
            var director=UnityEngine.Object.FindFirstObjectByType<StageShowDirector>(FindObjectsInactive.Include);
            var hint=UnityEngine.Object.FindFirstObjectByType<StageLightController>(FindObjectsInactive.Include);
            if(director==null || hint==null)throw new InvalidOperationException("조명 Director/Hint 없음");
            Undo.RecordObject(director,"Connect band rims");director.EditorConfigureRims(rims.ToArray());EditorUtility.SetDirty(director);
            foreach(var fever in UnityEngine.Object.FindObjectsByType<FeverSpotlight>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {Undo.RecordObject(fever,"Connect blackout suppression");fever.EditorConfigureBlackout(hint);EditorUtility.SetDirty(fever);}
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        [MenuItem("Tools/Lighting/Apply Lighting Feedback V2")]
        public static void ApplyFeedbackV2()
        {
            if(EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Main")
                throw new InvalidOperationException("Main 씬 Edit Mode에서 실행하세요.");
            var classic=Shader.Find("ContextStage/Lighting/Pixel Spotlight 2D");
            if(classic==null)throw new InvalidOperationException("기존 PixelSpotlight shader 없음");
            var beam=Material("ShowBeam","ContextStage/Lighting/Pixel Spotlight 2D");
            Undo.RecordObject(beam,"Restore classic pixel beam");beam.shader=classic;EditorUtility.SetDirty(beam);
            var laser=Material("ShowLaser","ContextStage/Lighting/Pixel Show Beam");
            Sprite litSoftbox=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"/ST04_StudioSoftbox_Lit_v2.png");
            if(litSoftbox==null && File.Exists("C:/Users/FOR/AppData/Local/Temp/codex-clipboard-2f35d93f-34f7-4d79-b63d-00db672de949.png"))
            {
                string path=Art+"/ST04_StudioSoftbox_Lit_v2.png";
                File.Copy("C:/Users/FOR/AppData/Local/Temp/codex-clipboard-2f35d93f-34f7-4d79-b63d-00db672de949.png",path);
                AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;
                importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=256;importer.alphaIsTransparency=true;importer.SaveAndReimport();
                litSoftbox=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            foreach(var rig in UnityEngine.Object.FindObjectsByType<StageLightingRig>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var p=rig.Profile;if(p==null)continue;Undo.RecordObject(p,"Lighting feedback V2");
                p.beamMaterial=beam;p.laserMaterial=laser;
                p.ambientFloor=p.venue==1?1.05f:p.venue==3?1.05f:p.venue==4?1.1f:0;
                p.beamIntensity=p.rival?0.3f:p.venue==2?0.30f:p.venue==4?0.4f:0.38f;
                p.sweepDegrees=p.rival?8:p.venue==2?1.5f:p.venue==3?6:p.venue==4?3:2;
                p.sweepPeriod=p.rival?5:7;
                if(!p.rival){ColorUtility.TryParseHtmlString(p.venue==1?"#8fd3ff":p.venue==2?"#e83b3b":p.venue==3?"#a884f3":"#8fd3ff",out p.primary);p.secondary=p.primary;}
                EditorUtility.SetDirty(p);
                var wide=new List<PixelLaserRenderer>();var thin=new List<PixelLaserRenderer>();
                foreach(var renderer in rig.GetComponentsInChildren<PixelLaserRenderer>(true))
                {
                    Undo.RecordObject(renderer,"Enlarge pixel beams");Undo.RecordObject(renderer.transform,"Align pixel beams");
                    if(renderer.name.StartsWith("Beam_"))
                    {
                        int index=int.Parse(renderer.name.Substring(5));bool visible=p.venue==2?index<3:index<2;
                        renderer.gameObject.SetActive(visible);
                        float x=p.venue==2?(index-1)*5.5f:(index==0?-8:8);
                        float angle=p.venue==2?180:index==0?-135:135;
                        if(p.venue==4){x=index==0?-5.5f:5.5f;angle=index==0?-150:150;}
                        renderer.transform.localPosition=new Vector3(x,p.rival?4.2f:p.venue==4?4.2f:4.8f,0);
                        renderer.transform.localRotation=Quaternion.Euler(0,0,angle);
                        renderer.EditorConfigure(beam,p.rival?10:p.venue==2?8.5f:12,p.rival?8:p.venue==2?6:13);
                        wide.Add(renderer);
                    }
                    else if(renderer.name.StartsWith("Laser_"))
                    {
                        int index=int.Parse(renderer.name.Substring(6));renderer.gameObject.SetActive(index<4);
                        renderer.transform.localPosition=new Vector3(index%2==0?-7.6f:7.6f,index<2?-2.5f:0.8f,0);
                        renderer.transform.localRotation=Quaternion.Euler(0,0,index%2==0?-40:40);
                        renderer.EditorConfigure(laser,7,0.12f);thin.Add(renderer);
                    }
                    EditorUtility.SetDirty(renderer);
                }
                foreach(var fixture in rig.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if(fixture.name.StartsWith("CeilingPAR_"))
                    {
                        int index=int.Parse(fixture.name.Substring(11));Undo.RecordObject(fixture.transform,"Enlarge PAR fixture");
                        fixture.transform.localPosition=new Vector3((index-1)*5.5f,5.1f,0);
                        fixture.transform.localScale=Vector3.one*(1.25f/fixture.sprite.bounds.size.y);
                    }
                    if(fixture.name.StartsWith("StudioSoftbox_"))
                    {
                        int index=int.Parse(fixture.name.Substring(14));Undo.RecordObject(fixture.transform,"Enlarge studio softbox");
                        Undo.RecordObject(fixture,"Use supplied lit softbox");if(litSoftbox!=null)fixture.sprite=litSoftbox;
                        fixture.gameObject.SetActive(index<2);fixture.transform.localPosition=new Vector3(index==0?-5.5f:5.5f,4.2f,0);
                        fixture.transform.localScale=Vector3.one*(2.6f/fixture.sprite.bounds.size.x);
                    }
                    if(fixture.name.StartsWith("LED_"))
                    {
                        int index=int.Parse(fixture.name.Substring(4));Undo.RecordObject(fixture.transform,"Consolidate LUX LED strip");
                        fixture.transform.localPosition=new Vector3(-5.2f+index*0.8f,3.6f,0);fixture.transform.localScale=new Vector3(0.72f,0.045f,1);
                    }
                }
                PixelLaserRenderer accent=null;
                if(p.venue==1)
                {
                    var old=rig.transform.Find("WarmAmbient");accent=old!=null?old.GetComponent<PixelLaserRenderer>():Beam(rig.transform,"WarmAmbient",-7,4.7f,-145,9,11,beam);
                }
                Undo.RecordObject(rig,"Refresh classic lighting references");
                var data=new SerializedObject(rig);
                rig.EditorConfigure(p,wide.ToArray(),thin.ToArray(),ReadArray<SpriteRenderer>(data,"leds"),ReadArray<SpriteRenderer>(data,"fixtures"),ReadArray<ParticleSystem>(data,"smoke"),data.FindProperty("onAir").objectReferenceValue as TMP_Text);
                rig.EditorConfigureAccent(accent);EditorUtility.SetDirty(rig);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[Lighting V2] Classic pixel shader restored; our beams follow one audience hint color. Fever settings preserved.");
        }
        static T[] ReadArray<T>(SerializedObject source,string property) where T:UnityEngine.Object
        {var array=source.FindProperty(property);var values=new T[array.arraySize];for(int i=0;i<values.Length;i++)values[i]=array.GetArrayElementAtIndex(i).objectReferenceValue as T;return values;}
        [MenuItem("Tools/Lighting/Install Stage Shows In Main")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Main")
                throw new InvalidOperationException("Main 씬 Edit Mode에서 실행하세요.");
            EditorSetupUtility.EnsureFolder(Root); EditorSetupUtility.EnsureFolder(Art);
            Sprite fog=Import("VFX_PixelSmog_Neutral_v1",128);
            Sprite par=Import("ST02_CeilingPAR_Unlit_v1",256);
            Sprite softbox=Import("ST04_StudioSoftbox_Unlit_v1",256);
            Sprite frame=Import("ST04_OnAirFrame_Blank_v1",256);
            Sprite square=Square();
            Material beam=Material("ShowBeam","ContextStage/Lighting/Pixel Show Beam");
            Material fixture=Material("ShowFixture","ContextStage/Lighting/Stage Show Sprite");
            Material smoke=Material("ShowSmoke","ContextStage/Lighting/Stage Show Sprite");
            fixture.SetFloat("_Cutoff",0.12f); smoke.SetFloat("_Cutoff",0.025f);
            smoke.mainTexture=fog.texture; EditorUtility.SetDirty(fixture); EditorUtility.SetDirty(smoke);
            var rigs=new List<StageLightingRig>();
            var sets=UnityEngine.Object.FindObjectsByType<StageSet>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            for(int stage=1;stage<=5;stage++)
            {
                string id=stage==5?"stage_05_boss":"stage_0"+stage;
                StageSet set=sets.FirstOrDefault(x=>x.StageId==id);
                if(set==null)throw new InvalidOperationException("StageSet 없음: "+id);
                rigs.Add(Build(set.transform,Profile(stage,false,beam,fixture,smoke),par,softbox,frame,fog));
                if(stage==5)rigs.Add(Build(set.transform,Profile(stage,true,beam,fixture,smoke),par,softbox,frame,fog));
            }
            foreach(var rig in rigs)
            {
                if(rig.Profile.venue==4)
                    foreach(var item in rig.GetComponentsInChildren<Transform>(true))
                        if((item.name=="OnAir" || item.name=="OnAirFrame") && Mathf.Abs(item.localPosition.x+7.3f)<0.01f)
                        {Undo.RecordObject(item,"Keep ON AIR clear of timer");item.localPosition=new Vector3(-5.7f,3.4f,0);}
                foreach(var sr in rig.GetComponentsInChildren<SpriteRenderer>(true))
                    if(sr.name.StartsWith("LED_")){Undo.RecordObject(sr,"Persist LED sprite");sr.sprite=square;EditorUtility.SetDirty(sr);}
                if(!rig.Profile.rival)continue;
                var path=new List<SpriteRenderer>();
                for(int i=0;i<6;i++)
                {
                    string name="PathLight_"+i;var existing=rig.transform.Find(name);
                    path.Add(existing!=null?existing.GetComponent<SpriteRenderer>():Prop(rig.transform,name,square,new Vector2(i%2==0?-2:2,-5.5f-i*0.35f),new Vector2(0.6f,0.08f),fixture));
                }
                Undo.RecordObject(rig,"Connect fan path lights");rig.EditorConfigurePath(path.ToArray());EditorUtility.SetDirty(rig);
            }
            var hint=UnityEngine.Object.FindFirstObjectByType<StageLightController>(FindObjectsInactive.Include);
            if(hint==null)throw new InvalidOperationException("StageLightController 없음");
            var director=hint.GetComponent<StageShowDirector>();
            if(director==null)director=Undo.AddComponent<StageShowDirector>(hint.gameObject);
            Undo.RecordObject(director,"Connect lighting shows");
            var arena=UnityEngine.Object.FindFirstObjectByType<BossArenaLayout>(FindObjectsInactive.Include);
            director.EditorConfigure(rigs.ToArray(),hint,UnityEngine.Object.FindFirstObjectByType<StageBackgroundView>(FindObjectsInactive.Include),arena);
            EditorUtility.SetDirty(director);
            foreach(var rival in UnityEngine.Object.FindObjectsByType<RivalStagePlaceholder>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                Undo.RecordObject(rival,"Delegate rival lighting"); var so=new SerializedObject(rival);
                so.FindProperty("externalLightingShow").boolValue=true;so.FindProperty("lightingShow").objectReferenceValue=director;so.ApplyModifiedProperties();
            }
            if(arena!=null)
            {
                var so=new SerializedObject(arena);var bg=so.FindProperty("rivalBackground");
                if(bg!=null && bg.objectReferenceValue==null){Undo.RecordObject(arena,"Connect rival background");bg.objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/0930_art/boss_background.png");so.ApplyModifiedProperties();}
            }
            foreach(var fever in UnityEngine.Object.FindObjectsByType<FeverSpotlight>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                Undo.RecordObject(fever,"Reduce fever flashing");var so=new SerializedObject(fever);
                so.FindProperty("hardBlink").boolValue=false;so.FindProperty("minIntensity").floatValue=0.9f;
                so.FindProperty("peakIntensity").floatValue=1.2f;so.FindProperty("blinksPerSecond").floatValue=1;
                so.ApplyModifiedProperties();
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[StageLightingSetup] 6 rigs connected. Existing StageSet/UI positions preserved. Repeat installs reuse existing rigs and profiles.");
        }
        static Sprite Import(string name,int size)
        {
            string path=Art+"/"+name+".png";
            if(!File.Exists(path)){File.Copy("Docs/ArtDrafts/Lighting_20261008_v1/"+name+".png",path);AssetDatabase.ImportAsset(path);}
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=size;importer.alphaIsTransparency=true;
            importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static Material Material(string name,string shader)
        {
            string path=Root+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){var s=Shader.Find(shader);if(s==null)throw new Exception("Shader missing: "+shader);m=new Material(s);AssetDatabase.CreateAsset(m,path);}return m;
        }
        static Sprite Square()
        {
            string path=Root+"/LED_Square.asset";
            var existing=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();if(existing!=null)return existing;
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false){name="LED White Pixel",filterMode=FilterMode.Point};
            texture.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white});texture.Apply();AssetDatabase.CreateAsset(texture,path);
            var sprite=Sprite.Create(texture,new Rect(0,0,2,2),new Vector2(0.5f,0.5f),2);sprite.name="LED_Square";AssetDatabase.AddObjectToAsset(sprite,texture);AssetDatabase.ImportAsset(path);return sprite;
        }
        static StageLightingProfile Profile(int stage,bool rival,Material beam,Material fixture,Material smoke)
        {
            string path=Root+"/Stage"+stage+(rival?"_LUX":"_Show")+".asset";
            var p=AssetDatabase.LoadAssetAtPath<StageLightingProfile>(path);if(p!=null)return p;
            p=ScriptableObject.CreateInstance<StageLightingProfile>();p.stageId=stage==5?"stage_05_boss":"stage_0"+stage;
            p.venue=stage;p.rival=rival;p.beamMaterial=beam;p.fixtureMaterial=fixture;p.smokeMaterial=smoke;
            string[] colors={"#30e1b9","#e83b3b","#a884f3","#c7dcd0","#fbb954"};
            ColorUtility.TryParseHtmlString(rival?"#f04f78":colors[stage-1],out p.primary);
            ColorUtility.TryParseHtmlString(rival?"#30e1b9":stage==4?"#9babb2":"#fdcbb0",out p.secondary);
            p.beamIntensity=rival?0.22f:stage==2?0.2f:0.13f;
            p.sweepDegrees=rival?18:stage==3?12:stage==5?5:stage==2?2:0;
            p.sweepPeriod=rival?4:6;p.ledStep=rival?0.2f:0.5f;
            AssetDatabase.CreateAsset(p,path);return p;
        }
        static GameObject Child(Transform parent,string name,Vector3 position)
        {
            var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Create stage lighting");go.transform.SetParent(parent,false);go.transform.localPosition=position;return go;
        }
        static PixelLaserRenderer Beam(Transform parent,string name,float x,float y,float angle,float length,float width,Material mat)
        {
            var go=Child(parent,name,new Vector3(x,y,0));go.transform.localRotation=Quaternion.Euler(0,0,angle);
            var b=go.AddComponent<PixelLaserRenderer>();b.EditorConfigure(mat,length,width);return b;
        }
        static SpriteRenderer Prop(Transform parent,string name,Sprite sprite,Vector2 position,Vector2 size,Material mat)
        {
            var go=Child(parent,name,new Vector3(position.x,position.y,0));var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=1;sr.sharedMaterial=mat;
            go.transform.localScale=new Vector3(size.x/Mathf.Max(0.001f,sprite.bounds.size.x),size.y/Mathf.Max(0.001f,sprite.bounds.size.y),1);return sr;
        }
        static StageLightingRig Build(Transform parent,StageLightingProfile p,Sprite par,Sprite softbox,Sprite frame,Sprite fog)
        {
            string name=p.rival?"[LUX LightingRig]":"[LightingRig]";
            var old=parent.Find(name);if(old!=null){var rig=old.GetComponent<StageLightingRig>();if(rig==null)throw new Exception("Existing lighting root without rig: "+name);return rig;}
            var root=Child(parent,name,Vector3.zero);var view=root.AddComponent<StageLightingRig>();
            var beams=new List<PixelLaserRenderer>();var lasers=new List<PixelLaserRenderer>();var leds=new List<SpriteRenderer>();var fixtures=new List<SpriteRenderer>();var particles=new List<ParticleSystem>();TMP_Text label=null;
            int count=p.rival?4:p.venue==2?3:p.venue==3?5:p.venue==4?3:2;
            for(int i=0;i<count;i++)
            {
                float x=count==2?(i==0?-7:7):Mathf.Lerp(-6,6,i/(float)(count-1));
                float angle=p.venue==3?180+(i-2)*10:180;
                if(p.rival)angle=x<0?-145:145;
                beams.Add(Beam(root.transform,"Beam_"+i,x,4,angle,p.rival?3.5f:p.venue==1?2.6f:3.8f,p.venue==4?3:1.6f,p.beamMaterial));
                if(p.venue==2)fixtures.Add(Prop(root.transform,"CeilingPAR_"+i,par,new Vector2(x,4.35f),new Vector2(0.9f,0.9f),p.fixtureMaterial));
                if(p.venue==4)fixtures.Add(Prop(root.transform,"StudioSoftbox_"+i,softbox,new Vector2(x,4.4f),new Vector2(1.4f,0.9f),p.fixtureMaterial));
            }
            if(p.rival)
            {
                root.transform.localPosition=Vector3.up*8;
                Sprite overlay=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/0930_art/boss_stage_overlay.png");
                if(overlay!=null)fixtures.Add(Prop(root.transform,"LUX_Truss",overlay,Vector2.zero,new Vector2(19.2f,10.8f),p.fixtureMaterial));
                for(int i=0;i<6;i++)
                {
                    float x=(i%2==0?-1:1)*(i<4?7:3);float y=i<4?0:1;
                    lasers.Add(Beam(root.transform,"Laser_"+i,x,y,x<0?-45:45,4,0.045f,p.beamMaterial));
                }
                for(int i=0;i<14;i++)leds.Add(Prop(root.transform,"LED_"+i,SquareSprite.Get(),new Vector2(-6.5f+i,3.5f),new Vector2(0.65f,0.09f),p.fixtureMaterial));
                for(int i=0;i<2;i++)
                {
                    var go=Child(root.transform,"Smog_"+i,new Vector3(i==0?-5.5f:5.5f,-3.4f,0));var ps=go.AddComponent<ParticleSystem>();
                    var main=ps.main;main.playOnAwake=false;main.loop=false;main.duration=4;main.startLifetime=3;main.startSpeed=0.2f;main.startSize=new ParticleSystem.MinMaxCurve(0.8f,1.4f);main.maxParticles=p.smokeParticlesPerEmitter;main.simulationSpace=ParticleSystemSimulationSpace.World;
                    main.startColor=new Color(0.65f,0.68f,0.7f,0.18f);var emission=ps.emission;emission.enabled=false;
                    var shape=ps.shape;shape.enabled=false;var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=new ParticleSystem.MinMaxCurve(i==0?0.12f:-0.12f);velocity.y=new ParticleSystem.MinMaxCurve(0.3f);velocity.z=new ParticleSystem.MinMaxCurve(0);
                    var fade=ps.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,0.25f),new GradientAlphaKey(0,1)});fade.color=gradient;
                    var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=p.smokeMaterial;renderer.sortingOrder=1;ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);particles.Add(ps);
                }
            }
            if(p.venue==4)
            {
                fixtures.Add(Prop(root.transform,"OnAirFrame",frame,new Vector2(-7.3f,3.4f),new Vector2(2.2f,0.73f),p.fixtureMaterial));
                var go=Child(root.transform,"OnAir",new Vector3(-7.3f,3.4f,0));label=go.AddComponent<TextMeshPro>();label.text="ON AIR";label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/DungGeunMo SDF.asset");label.fontSize=4;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(2,0.6f);label.GetComponent<MeshRenderer>().sortingOrder=2;
            }
            view.EditorConfigure(p,beams.ToArray(),lasers.ToArray(),leds.ToArray(),fixtures.ToArray(),particles.ToArray(),label);EditorUtility.SetDirty(view);return view;
        }
    }
}
