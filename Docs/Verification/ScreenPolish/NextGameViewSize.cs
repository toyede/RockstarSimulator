int index=UnityEditor.SessionState.GetInt("PolishGameViewSize",4);
UnityEditor.SessionState.SetInt("PolishGameViewSize",index==4?7:3);return index==4?7:3;
