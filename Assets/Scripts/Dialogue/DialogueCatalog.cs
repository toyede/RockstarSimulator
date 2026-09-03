using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// sequenceId → DialogueSequence 를 찾는 단일 진입점. (기획서 §14 DialogueCatalog)
    /// Resources/Dialogue/DialogueCatalog.asset 하나만 두고, 문자열 비교를 여러 시스템에 흩뿌리지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueCatalog", menuName = "ContextStage/Dialogue/Catalog")]
    public sealed class DialogueCatalog : ScriptableObject
    {
        public const string ResourcesPath = "Dialogue/DialogueCatalog";

        [SerializeField, Tooltip("대화창 표현 설정")]
        DialogueStyle style;

        [SerializeField]
        List<DialogueSequence> sequences = new List<DialogueSequence>();

        public DialogueStyle Style => style;
        public IReadOnlyList<DialogueSequence> Sequences => sequences;

        public static DialogueCatalog LoadDefault() =>
            Resources.Load<DialogueCatalog>(ResourcesPath);

        public bool TryGetSequence(string sequenceId, out DialogueSequence sequence)
        {
            sequence = null;
            if (string.IsNullOrWhiteSpace(sequenceId) || sequences == null) return false;

            string trimmed = sequenceId.Trim();
            for (int i = 0; i < sequences.Count; i++)
            {
                DialogueSequence candidate = sequences[i];
                if (candidate != null &&
                    string.Equals(candidate.SequenceId, trimmed, StringComparison.Ordinal))
                {
                    sequence = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryValidate(out string error)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (sequences == null)
            {
                error = "시퀀스 목록이 없습니다.";
                return false;
            }

            for (int i = 0; i < sequences.Count; i++)
            {
                DialogueSequence sequence = sequences[i];
                if (sequence == null)
                {
                    error = $"카탈로그의 {i}번 항목이 비어 있습니다.";
                    return false;
                }

                if (!sequence.TryValidate(out error)) return false;

                if (!ids.Add(sequence.SequenceId))
                {
                    error = $"중복 sequenceId: {sequence.SequenceId}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorConfigure(DialogueStyle newStyle, List<DialogueSequence> newSequences)
        {
            if (newStyle != null) style = newStyle;
            if (newSequences != null) sequences = newSequences;
        }
#endif
    }
}
