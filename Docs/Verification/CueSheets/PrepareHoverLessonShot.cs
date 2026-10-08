if(!UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Play Mode required");
UnityEditor.EditorApplication.isPaused=false;ContextStage.TutorialFlow.SuppressForTests=true;
var gm=GameJamKit.GameManager.Instance;gm.ResetGame();
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage01.asset"));gm.StartGame();
var flow=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>();flow.StartTutorial();
flow.GetType().GetMethod("EnterHoverHint",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(flow,null);
System.Collections.IEnumerator Shot(){yield return new UnityEngine.WaitForSecondsRealtime(.65f);UnityEditor.EditorApplication.isPaused=true;}
flow.StartCoroutine(Shot());return "Actual Stage 1 new-audience inspection lesson and cursor guide";
