using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 대사 시퀀스 하나 (공연 전 인트로 하나 = 시퀀스 하나). (기획서 §14 DialogueSequenceDefinition)
    /// StageDefinition.preDialogueId 가 이 sequenceId 를 가리킨다.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueSequence", menuName = "ContextStage/Dialogue/Sequence")]
    public sealed class DialogueSequence : ScriptableObject
    {
        [SerializeField, Tooltip("StageDefinition.preDialogueId 와 같은 값 (예: intro_stage_01)")]
        string sequenceId = "";

        [SerializeField, Tooltip("한 시퀀스는 20~40초, 3~6줄 안에 끝낸다")]
        List<DialogueLine> lines = new List<DialogueLine>();

        public string SequenceId => sequenceId;
        public IReadOnlyList<DialogueLine> Lines => lines;
        public int LineCount => lines != null ? lines.Count : 0;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(sequenceId))
            {
                error = $"'{name}' 시퀀스에 sequenceId 가 없습니다.";
                return false;
            }

            if (lines == null || lines.Count == 0)
            {
                error = $"'{sequenceId}' 시퀀스에 대사가 없습니다.";
                return false;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] == null || !lines[i].HasText)
                {
                    error = $"'{sequenceId}' 시퀀스의 {i + 1}번째 줄이 비어 있습니다.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용] 셋업 메뉴가 기본 대사를 채울 때 쓴다.</summary>
        public void EditorInitialize(string id, List<DialogueLine> newLines)
        {
            sequenceId = id ?? string.Empty;
            lines = newLines ?? new List<DialogueLine>();
        }

        void OnValidate()
        {
            sequenceId = sequenceId == null ? string.Empty : sequenceId.Trim();
        }
#endif
    }
}
