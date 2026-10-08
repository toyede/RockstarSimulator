UnityEditor.EditorApplication.isPaused = false;
return new { UnityEngine.Application.isPlaying, paused = UnityEditor.EditorApplication.isPaused };
