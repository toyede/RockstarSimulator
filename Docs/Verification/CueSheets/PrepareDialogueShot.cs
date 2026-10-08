UnityEditor.EditorApplication.isPaused=false;
UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>().HideAll();
var dialogue=UnityEngine.Object.Instantiate(UnityEngine.Resources.Load<UnityEngine.GameObject>("Dialogue/DialoguePanel")).GetComponent<ContextStage.DialoguePanel>();
var sequence=UnityEngine.ScriptableObject.CreateInstance<ContextStage.DialogueSequence>();
sequence.EditorInitialize("shot",new System.Collections.Generic.List<ContextStage.DialogueLine>{new ContextStage.DialogueLine{speakerId="raccoon",speakerName="너구리",text="좋아, 이제 우리 차례야. 관객이 어떤 공연을 기다리는지 잘 살펴보자!"}});
dialogue.Play(sequence,new ContextStage.DialoguePresentationContext(),null);
dialogue.GetType().GetMethod("CompleteLineInstantly",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(dialogue,null);
System.Collections.IEnumerator Shot(){yield return new UnityEngine.WaitForSecondsRealtime(.3f);UnityEditor.EditorApplication.isPaused=true;}
dialogue.StartCoroutine(Shot());return "Dialogue arrow freeze requested";
