var panel = UnityEngine.Object.FindFirstObjectByType<ContextStage.DialoguePanel>(UnityEngine.FindObjectsInactive.Include);
if (panel == null || panel.gameObject.scene.path != "Assets/Scenes/TourHub.unity" || !panel.IsPlaying)
    throw new System.Exception("Actual TourHub dialogue is not playing");
if (panel.IsTyping) panel.Advance();
UnityEditor.EditorApplication.isPaused = true;
return "Frozen actual TourHub dialogue: line " + panel.CurrentLineIndex;
