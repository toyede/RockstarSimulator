using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 화자 + 표정 → 초상화(프레임). Resources/Dialogue/DialoguePortraitCatalog.asset — Tools/Art/Apply 0918 Art 가 만든다.
    ///
    /// 해석 순서: (speakerId, emotionId) 일치 → (speakerId, 표정 없음 = 기본) → 없으면 DialogueLine.portrait 그대로.
    /// 프레임이 2장 이상이면 대화창이 frameInterval 마다 교대한다 (LUX//FAUNA 3프레임).
    /// 표정 ID 는 대사 쪽(DialogueSetupMenu 의 Line 세 번째 인자)과 여기의 emotionIds 로만 연결된다. 코드에는 표정 이름이 없다.
    /// </summary>
    [CreateAssetMenu(fileName = "DialoguePortraitCatalog", menuName = "ContextStage/Dialogue/Portrait Catalog")]
    public sealed class DialoguePortraitCatalog : ScriptableObject
    {
        public const string ResourcesPath = "Dialogue/DialoguePortraitCatalog";

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("DialogueLine.speakerId (raccoon / hedgehog / skunk / rival)")] public string speakerId = "";
            [Tooltip("이 초상화를 쓸 표정 ID 목록. 비우면 그 화자의 기본 초상화")] public List<string> emotionIds = new List<string>();
            [Tooltip("프레임. 1장이면 정지, 2장 이상이면 교대")] public List<Sprite> frames = new List<Sprite>();
            [Tooltip("프레임 교대 간격(초)")] public float frameInterval = 0.45f;

            public bool IsDefault => emotionIds == null || emotionIds.Count == 0;
            public bool HasFrames => frames != null && frames.Count > 0 && frames[0] != null;
            public bool Matches(string emotionId)
            {
                if (IsDefault || string.IsNullOrEmpty(emotionId)) return false;
                for (int i = 0; i < emotionIds.Count; i++)
                    if (string.Equals(emotionIds[i], emotionId, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
        }

        [SerializeField] List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public static DialoguePortraitCatalog LoadDefault() => Resources.Load<DialoguePortraitCatalog>(ResourcesPath);

        /// <summary>표정 일치 항목 → 기본 항목 순으로 찾는다. 없으면 null.</summary>
        public Entry Resolve(string speakerId, string emotionId)
        {
            if (string.IsNullOrEmpty(speakerId)) return null;
            Entry fallback = null;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (e == null || !e.HasFrames || !string.Equals(e.speakerId, speakerId, StringComparison.Ordinal)) continue;
                if (e.Matches(emotionId)) return e;
                if (e.IsDefault && fallback == null) fallback = e;
            }
            return fallback;
        }

        /// <summary>셋업용: 같은 화자·같은 표정 목록의 항목이 있으면 프레임만 갱신, 없으면 추가.</summary>
        public void Upsert(string speakerId, IList<string> emotionIds, IList<Sprite> frames, float interval)
        {
            Entry target = null;
            for (int i = 0; i < entries.Count && target == null; i++)
            {
                Entry e = entries[i];
                if (e == null || e.speakerId != speakerId) continue;
                bool sameEmotions = (e.emotionIds == null ? 0 : e.emotionIds.Count) == (emotionIds == null ? 0 : emotionIds.Count);
                for (int k = 0; sameEmotions && emotionIds != null && k < emotionIds.Count; k++)
                    sameEmotions = e.emotionIds.Contains(emotionIds[k]);
                if (sameEmotions) target = e;
            }
            if (target == null)
            {
                target = new Entry { speakerId = speakerId };
                if (emotionIds != null) target.emotionIds.AddRange(emotionIds);
                entries.Add(target);
            }
            target.frames.Clear();
            if (frames != null) foreach (Sprite s in frames) if (s != null) target.frames.Add(s);
            target.frameInterval = interval;
        }
    }
}
