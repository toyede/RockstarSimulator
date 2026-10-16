int stage=UnityEditor.SessionState.GetInt("PolishPreviewStage",2)+1;
if(stage>5)stage=2;
UnityEditor.SessionState.SetInt("PolishPreviewStage",stage);return stage;
