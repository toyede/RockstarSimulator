if(!UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Play Mode required");
UnityEditor.EditorApplication.isPaused=false;
ContextStage.TutorialFlow.SuppressForTests=true;
var gm=GameJamKit.GameManager.Instance;gm.ResetGame();
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage02.asset"));
gm.StartGame();ContextStage.PerformanceTimer.SetPaused(true);ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=true;
var overlay=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>();
overlay.SetSpecialLessonLayout(true);
overlay.ShowMessage("카드를 누른 채 특별 관객에게 가져가세요.","관객의 몸과 겹치면 놓으세요. 움직이는 손 모양을 따라 해보세요.",false);
System.Collections.IEnumerator Shot(){yield return new UnityEngine.WaitForSecondsRealtime(.2f);UnityEditor.EditorApplication.isPaused=true;}
overlay.StartCoroutine(Shot());return "Actual Stage 2 scene overlay, expanded paper and keyword colors";
