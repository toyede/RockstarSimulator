UnityEditor.EditorApplication.isPaused=false;
var gm=GameJamKit.GameManager.Instance;gm.ResetGame();
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage01.asset"));
gm.StartGame();ContextStage.PerformanceTimer.SetPaused(true);ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=true;
var overlay=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>();
overlay.ShowMessage("관객 위에 마우스를 올리거나 길게 누르면 관객의 마음을 알 수 있습니다.","말풍선과 테두리색을 통해 좋아하는 행동과 현재 기분을 확인할 수 있습니다. 관객을 2초 동안 살펴보세요.",true);
System.Collections.IEnumerator Shot(){yield return new UnityEngine.WaitForSeconds(.15f);UnityEditor.EditorApplication.isPaused=true;}
overlay.StartCoroutine(Shot());return "Tutorial cue sheet freeze requested";
