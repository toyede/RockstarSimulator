var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
var view=UnityEngine.Resources.FindObjectsOfTypeAll(type).First();
var flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var prop=type.GetProperty("selectedSizeIndex",flags);
int selected=UnityEditor.SessionState.GetInt("PolishGameViewSize",4);
prop.SetValue(view,selected);((UnityEditor.EditorWindow)view).Repaint();return new{selected};
