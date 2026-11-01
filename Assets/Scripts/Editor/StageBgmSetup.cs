using System;
using System.Linq;
using GameJamKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextStage.EditorTools
{
    public static class StageBgmSetup
    {
        static readonly string[] Stages = { "Stage01", "Stage02", "Stage03", "Stage04", "Stage05Boss" };
        static readonly string[] Ids = { "osaka_rain", "sharp", "dogtown", "four_more_weeks", "360_no_scope", "pomade" };
        static readonly string[] Clips = {
            "Osaka Rain - ALBIS", "Sharp - Jeremy Korpas", "Dogtown - National Sweetheart",
            "Four More Weeks - Vans in Japan", "360 No Scope - The Soundlings", "Pomade - Silent Partner"
        };

        [MenuItem("Tools/Audio/Apply Stage BGM Playlist")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit Mode only");
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("Main");
            var player = UnityEngine.Object.FindObjectsByType<StageBgmPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .SingleOrDefault(p => p.gameObject.scene == scene);
            if (!scene.IsValid() || !scene.isLoaded || player == null)
                throw new InvalidOperationException("Open the existing Main scene with one StageBgmPlayer.");
            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>("Assets/GameJamKit/Resources/SoundLibrary.asset");
            var stages = Stages.Select(name => AssetDatabase.LoadAssetAtPath<StageDefinition>("Assets/Settings/Tour/" + name + ".asset")).ToArray();
            var clips = Clips.Select(name => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BGM/" + name + ".mp3")).ToArray();
            if (library == null || stages.Any(s => s == null) || clips.Any(c => c == null))
                throw new InvalidOperationException("Playlist asset missing; nothing changed.");
            float volume = library.Sounds.First(s => s.id == "big_rock").volume;
            var serialized = new SerializedObject(library);
            var sounds = serialized.FindProperty("sounds");
            for (int i = 0; i < Ids.Length; i++)
            {
                int index = -1;
                for (int j = 0; j < sounds.arraySize; j++)
                    if (sounds.GetArrayElementAtIndex(j).FindPropertyRelative("id").stringValue == Ids[i]) { index = j; break; }
                bool added = index < 0;
                if (added) { index = sounds.arraySize; sounds.arraySize++; }
                var entry = sounds.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("id").stringValue = Ids[i];
                var entries = entry.FindPropertyRelative("clips"); entries.arraySize = 1;
                entries.GetArrayElementAtIndex(0).objectReferenceValue = clips[i];
                if (added) entry.FindPropertyRelative("volume").floatValue = volume;
                entry.FindPropertyRelative("pitchMin").floatValue = 1f;
                entry.FindPropertyRelative("pitchMax").floatValue = 1f;
                entry.FindPropertyRelative("loop").boolValue = true;
                entry.FindPropertyRelative("minInterval").floatValue = .02f;
                entry.FindPropertyRelative("bus").enumValueIndex = (int)AudioBus.ImpactSFX;

            }
            for (int i = 0; i < stages.Length; i++)
            {
                var stage = new SerializedObject(stages[i]);
                stage.FindProperty("bgmId").stringValue = Ids[i];
                stage.FindProperty("tutorialBgmId").stringValue = i < 2 ? "big_rock" : "";
                stage.FindProperty("playerLeadBgmId").stringValue = i == 4 ? "pomade" : "";
                stage.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(stages[i]);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo(); library.InvalidateCache();
            AssetDatabase.SaveAssetIfDirty(library);
            var component = new SerializedObject(player);
            component.FindProperty("useStageMusic").boolValue = true;
            if (component.ApplyModifiedPropertiesWithoutUndo())
            { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            Debug.Log("[StageBgm] Stage playlist registered. Boss lead switches 360 No Scope/Pomade; Stage 1/2 tutorial uses Big Rock. Title unchanged.");
        }
    }
}
