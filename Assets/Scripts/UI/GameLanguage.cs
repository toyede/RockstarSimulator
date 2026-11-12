using System.Collections.Generic;
using GameJamKit;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ContextStage
{
    internal static class GameLanguage
    {
        const string LanguageKey = "ui_language_en";
        public static bool English => Save.GetBool(LanguageKey, false);
        public static string Pick(string korean, string english) => English ? english : korean;

        // Exact matches are used only on menu labels; player-entered names and sprites are not translated.
        static readonly (string ko, string en)[] MenuLabels =
        {
            ("게임 시작", "Start Game"), ("투어 시작", "Start Tour"), ("계속하기", "Continue"),
            ("옵션", "Options"), ("옵 션", "Options"), ("설정", "Settings"), ("랭킹", "Rankings"), ("크레딧", "Credits"),
            ("종료", "Quit"), ("게임 종료", "Quit Game"), ("닫기", "Close"), ("뒤로", "Back"),
            ("일시정지", "Paused"), ("일시 정지", "Paused"), ("게임 재개", "Resume Game"),
            ("다시 시작", "Restart"), ("타이틀로 돌아가기", "Return to Title"),
            ("타이틀로", "Back to Title"), ("마스터 볼륨", "Master Volume"),
            ("화면 모드", "Display Mode"), ("현재 모드", "Current Mode"), ("전체 화면", "Fullscreen"),
            ("전체화면", "Fullscreen"), ("창 모드", "Windowed"), ("창모드", "Windowed"),
            ("튜토리얼 건너뛰기", "Skip Tutorial"),
        };
        static readonly Dictionary<Component, (string ko, string en)> Bound = new();

        public static void SetEnglish(bool english)
        {
            Save.SetBool(LanguageKey, english);
            Save.Flush();
            RefreshMenus();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            Bound.Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshMenus();

        public static void RefreshMenus()
        {
            var dead = new List<Component>();
            foreach (var entry in Bound)
                if (entry.Key == null) dead.Add(entry.Key);
            foreach (var component in dead) Bound.Remove(component);
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // Only menu canvases / settings and pause descendants. No dialogue or runtime game text.
                bool title = canvas.gameObject.scene.name == "Title";
                foreach (var label in canvas.GetComponentsInChildren<Text>(true))
                {
                    if (label.GetComponentInParent<InputField>(true) != null) continue;
                    if (title || InMenu(label)) label.text = MenuText(label, label.text);
                }
                foreach (var label in canvas.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label.GetComponentInParent<TMP_InputField>(true) != null) continue;
                    if (title || InMenu(label)) label.text = MenuText(label, label.text);
                }
            }
        }

        static bool InMenu(Component c) =>
            c.GetComponentInParent<OptionsPopup>(true) != null || c.GetComponentInParent<PausePopup>(true) != null;

        static string MenuText(Component label, string original)
        {
            if (label.gameObject.scene.name == "Title" &&
                (original?.Trim() == "옵션" || original?.Trim() == "옵 션") &&
                IsTitleOptionButton(label))
                return "Option";
            if (Bound.TryGetValue(label, out var existing)) return Pick(existing.ko, existing.en);
            foreach (var pair in MenuLabels)
            {
                if (original?.Trim() != pair.ko) continue;
                Bound[label] = (original, pair.en);
                return Pick(original, pair.en);
            }
            return original;
        }

        static bool IsTitleOptionButton(Component label)
        {
            var button = label.GetComponentInParent<Button>(true);
            if (button == null) return false;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentMethodName(i) == "OpenOptions") return true;
            return false;
        }
    }
}
