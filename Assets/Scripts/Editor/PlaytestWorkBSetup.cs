using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextStage.EditorTools
{
    /// <summary>Content-only migration. Never rebuilds UI or overwrites edited dialogue lines.</summary>
    public static class PlaytestWorkBSetup
    {
        [MenuItem("Tools/Playtest/Apply Work B Content (Preserve Layout)")]
        public static void Apply()
        {
            var catalog = ResultNewspaperCatalog.LoadDefault();
            if (catalog != null)
            {
                Undo.RecordObject(catalog, "Work B Headlines");
                Add(catalog, "stage_01", "퇴근길 대정체… 범인은 너구리 밴드", "집에 가던 발걸음, 앙코르 앞에서 멈췄다", "퇴근길에 발견한 작은 라이브", "왕관 쓴 너구리, 골목 데뷔 성공", "작은 관객석 지키며 첫 공연 마쳐", "기타는 켰는데… 발걸음은 못 잡았다");
                Add(catalog, "stage_02", "지하실에서 지진? 아니, 모쉬핏이었다", "천장 낮은 공연장, 환호는 높았다", "낡은 앰프 너머로 터진 함성", "지하 라이브홀 첫 무대, 일단 합격", "앰프는 버텼다… 밴드도 버텼다", "앰프보다 먼저 식어버린 객석");
                Add(catalog, "stage_03", "페스티벌 지도에 새 명소가 생겼다", "작은 무대에서 시작된 거대한 떼창", "헤드라이너 옆에서도 존재감 증명", "낯선 팬 앞에서 이름 알린 너구리", "페스티벌 무대, 아슬아슬하게 완주", "넓은 무대, 붙잡지 못한 발길");
                Add(catalog, "stage_04", "생방송 찢었다… 재방송 요청 쇄도", "카메라 앞에서도 흔들리지 않은 합주", "ON AIR! 안방까지 전한 라이브", "생방송 데뷔, 긴장 속에 무사 완료", "방송 사고 없이 첫 출연 마쳐", "빨간 불은 켜졌지만 호응은 꺼졌다");
                var so = new SerializedObject(catalog);
                Replace(so, "articleCrisisTitle", "옆 공연의 유혹에도 자리를 지킨 팬들", "돌발상황 속에서도 지켜낸 객석");
                Replace(so, "headlineBossWin", "라이벌과의 정면 승부, 너구리 밴드 웃었다", "LUX//FAUNA와의 승부, 앙코르는 너구리에게");
                Replace(so, "headlineBossLose", "라이벌 무대에 조명이… 너구리 밴드는 재정비", "오늘의 앙코르는 LUX//FAUNA… 다음 승부를 기약");
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(catalog);
            }

            var replacements = new Dictionary<string, string>
            {
                ["왜 안 나지. 이쪽이 안 꽂혔나?"] = "기타 앰프에서 소리가 안 나. 입력 케이블이 헐거운가?",
                ["뭐 뽑았어?"] = "볼륨 켠 채로 입력 케이블을 뽑은 거야?",
                ["일단 손 떼."] = "앰프 볼륨부터 내려. 케이블은 내가 꽂을게.",
                ["들린다."] = "입력 잭 다시 꽂았어. 기타 줄 한 번 튕겨봐.",
                ["...일단 첫 곡 해봐."] = "첫 곡 인트로 여덟 마디만 맞춰보자. 베이스는 내가 넣을게.",
                ["곡 순서 줘."] = "곡 순서랑 후렴 들어가는 마디 적어줘. 오늘 베이스는 내가 맡을게.",
                ["나중에 하자더라."] = "Verse 2 뒤 베이스 솔로를 빼자더라. 후렴을 한 번 더 넣으래.",
                ["...들어는 보고 바꾸라든가."] = "솔로 여덟 마디는 들어보고 자르든가. 리허설도 하기 전에 빼자잖아.",
                ["앞에 베이스 나오는 부분은 줄이는 게 낫겠더라고요."] = "후렴 앞 베이스 솔로 여덟 마디, 네 마디로 줄여보세요.",
                ["그래요? 저희는 반응 안 나오는 부분은 바로 빼요."] = "그래요? 저희는 첫 드랍 전에 객석이 식으면 빌드업부터 잘라요.",
                ["저희는 그 부분도 좋아해서요."] = "그 베이스 솔로 듣고 만든 후렴이라서요. 같이 연주하려고요.",
                ["앞에 좀 줄이래."] = "후렴 앞 베이스 솔로를 여덟 마디에서 네 마디로 줄이래."
            };
            foreach (string id in new[] { "intro_stage_02", "intro_stage_03", "intro_stage_05_boss" })
            {
                var seq = AssetDatabase.LoadAssetAtPath<DialogueSequence>($"Assets/Settings/Dialogue/Sequences/{id}.asset");
                if (seq == null) continue;
                Undo.RecordObject(seq, "Work B Concrete Dialogue");
                foreach (DialogueLine line in seq.Lines)
                {
                    if (replacements.TryGetValue(line.text, out string updated)) line.text = updated;
                    if (id == "intro_stage_02" && (line.text == "너구리가 케이블을 뽑는다.\n펑! 스피커에서 큰 소리와 함께 먼지가 튄다." || line.presentationCue == "cable_pop"))
                    {
                        line.text = "너구리가 볼륨을 켠 채 입력 케이블을 뽑는다.\n펑! 앰프에서 큰 소리가 나며 먼지가 튄다.";
                        line.presentationCue = "cable_pop";
                        line.sfxId = "guitar_stroke";
                    }
                    if (line.text.StartsWith("기타 앰프에서")) line.emotionId = "nervous";
                }
                EditorUtility.SetDirty(seq);
            }
            // Existing wooden UI impact; no audio file/volume/Mixer changes.
            foreach (var hand in Object.FindObjectsByType<CardHandUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (hand.GetComponent<PointerPixelFeedback>() == null)
                {
                    Undo.AddComponent<PointerPixelFeedback>(hand.gameObject);
                    EditorSceneManager.MarkSceneDirty(hand.gameObject.scene);
                }
            foreach (var view in Object.FindObjectsByType<ResultNewspaperView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(view);
                var sound = so.FindProperty("stampSoundId");
                if (sound != null && string.IsNullOrEmpty(sound.stringValue))
                {
                    Undo.RecordObject(view, "Result Stamp Sound");
                    sound.stringValue = "ui_click_wooden";
                    so.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
                }
            }
            AssetDatabase.SaveAssets();
        }

        static void Add(ResultNewspaperCatalog catalog, string id, string s, string a, string b, string c, string d, string fail)
            => catalog.EditorAddStageHeadline(new ResultNewspaperCatalog.StageHeadline { stageId = id, s = s, a = a, b = b, c = c, d = d, fail = fail });

        static void Replace(SerializedObject so, string name, string oldText, string newText)
        {
            var property = so.FindProperty(name);
            if (property != null && property.stringValue == oldText) property.stringValue = newText;
        }
    }
}
