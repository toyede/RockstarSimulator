using System;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 어디서든 한 줄로 대화를 재생하는 정적 파사드. (Hype / PerformanceTimer 와 같은 패턴)
    ///
    ///   Dialogue.TryPlay("intro_stage_01", context, () => StartPerformance());
    ///
    /// 패널은 씬에 있는 것 → Resources/Dialogue/DialoguePanel 프리팹 → 코드 생성 순으로 찾는다.
    /// 씬이 바뀌면 패널도 함께 사라지므로 다음 호출에서 다시 만든다.
    /// </summary>
    public static class Dialogue
    {
        static DialoguePanel s_panel;

        public static bool IsPlaying => s_panel != null && s_panel.IsPlaying;

        /// <summary>
        /// 시퀀스를 찾아 재생한다. 시퀀스가 없어도 context 에 룰 카드가 있으면 룰 카드만 보여준다.
        /// 둘 다 없으면 false 를 돌려주고 아무것도 하지 않는다 (호출자가 폴백 UI 를 쓴다).
        /// </summary>
        public static bool TryPlay(string sequenceId, DialoguePresentationContext context, Action onComplete)
        {
            DialogueCatalog catalog = DialogueCatalog.LoadDefault();
            DialogueSequence sequence = null;
            if (catalog != null && !catalog.TryGetSequence(sequenceId, out sequence))
                sequence = null;

            if (sequence == null && !context.HasRuleCard) return false;

            DialoguePanel panel = EnsurePanel(catalog != null ? catalog.Style : null);
            if (panel == null) return false;

            panel.Play(sequence, context, onComplete);
            return true;
        }

        /// <summary>진행 중인 대화를 전부 건너뛴다 (룰 카드는 남는다).</summary>
        public static void SkipCurrent()
        {
            if (s_panel != null && s_panel.IsPlaying) s_panel.Skip();
        }

        /// <summary>콜백 없이 즉시 닫는다. 씬을 떠나기 전 정리용.</summary>
        public static void HideImmediate()
        {
            if (s_panel != null) s_panel.HideImmediate();
        }

        static DialoguePanel EnsurePanel(DialogueStyle style)
        {
            // 파괴된 패널은 Unity 의 == 오버로드가 null 로 판정해 준다 (?? 금지)
            if (s_panel != null)
            {
                s_panel.Configure(style);
                return s_panel;
            }

            TourPrototypeUIFactory.EnsureEventSystem();

            s_panel = UnityEngine.Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);

            if (s_panel == null)
            {
                GameObject prefab = Resources.Load<GameObject>(DialoguePanelFactory.PrefabResourcesPath);
                if (prefab != null)
                {
                    GameObject instance = UnityEngine.Object.Instantiate(prefab);
                    instance.name = prefab.name;
                    s_panel = instance.GetComponentInChildren<DialoguePanel>(true);
                    if (s_panel == null)
                    {
                        Debug.LogWarning(
                            "[Dialogue] DialoguePanel 프리팹에 DialoguePanel 컴포넌트가 없어 코드 생성으로 대체합니다.",
                            prefab);
                        UnityEngine.Object.Destroy(instance);
                    }
                }
            }

            if (s_panel == null) s_panel = DialoguePanelFactory.Create(null, style);
            if (s_panel != null) s_panel.Configure(style);
            return s_panel;
        }
    }
}
