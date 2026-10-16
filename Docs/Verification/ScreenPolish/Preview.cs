if(!UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Play required");
UnityEditor.EditorApplication.isPaused=false;
var tutorial=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>();if(tutorial!=null)tutorial.enabled=false;
var overlay=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>();if(overlay!=null)overlay.HideAll();
var gm=GameJamKit.GameManager.Instance;GameJamKit.UIManager.Instance.CloseAll();gm.ResetGame();
int stage=UnityEditor.SessionState.GetInt("PolishPreviewStage",2);
string path=stage==5?"Stage05Boss":"Stage0"+stage;
var asset=UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/"+path+".asset");
if(asset==null)throw new System.Exception("Stage asset missing "+path);
ContextStage.StageRuntimeDirector.Active.ApplyStage(asset);gm.StartGame();
ContextStage.PerformanceTimer.SetPaused(true);ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=true;
if(stage==2 && overlay!=null){overlay.SetSpecialLessonLayout(true);overlay.ShowMessage("특별 관객이 연주를 요청하고 있습니다.","머리 위 요청 아이콘과 같은 SPECIAL 카드를 확인하세요. (클릭해서 계속)",true);}
if(stage==3)GameJamKit.EventBus.Raise(new ContextStage.StageEventStarted("festival_flag_wave","깃발을 흔들어!","8초 안에 Good 이상 카드 2장을 사용하세요!",8));
if(stage==4)GameJamKit.EventBus.Raise(new ContextStage.StageEventStarted("arena_big_screen","카메라 큐!","큐시트 순서대로 카드를 쓰세요: CHILL → MOSH → SINGALONG",8));
return new{stage=asset.StageId,score=gm.Score,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path};
