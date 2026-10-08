var flow=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>(); if(flow!=null) flow.enabled=false;
GameJamKit.GameManager.Instance.StartGame();
GameJamKit.UIManager.Instance.Open<ContextStage.PausePopup>();
return "Pause opened";
