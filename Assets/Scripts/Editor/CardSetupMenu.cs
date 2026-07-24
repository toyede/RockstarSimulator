using System.Collections.Generic;
using GameJamKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 카드 프로토타입 데이터와 씬 UI를 한 번에 구성한다.
    /// 이미 존재하는 에셋과 오브젝트는 재사용하므로 반복 실행해도 중복 생성되지 않는다.
    /// </summary>
    public static class CardSetupMenu
    {
        const string CardFolder = "Assets/Settings/Cards";
        const string DeckPath = CardFolder + "/CardDeckConfig.asset";
        const string ArtFolder = "Assets/Art_Assets";

        [MenuItem("Tools/Cards/Setup Card Prototype", false, 0)]
        public static void SetupCardPrototype()
        {
            var deck = SyncCardAssets();
            BuildCardSystem(deck);
            BuildCardCanvas();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Cards] 카드 프로토타입 셋업 완료. 숫자키로 공연을 시작하고 카드를 선택하세요.");
        }

        /// <summary>씬을 수정하지 않고 기존 카드/덱 에셋의 프로토타입 값만 동기화한다.</summary>
        public static CardDeckConfig SyncCardAssets()
        {
            EnsureFolder(CardFolder);

            var clapRhythm = LoadSprite(ArtFolder + "/card_clap_rhythm.png");
            var guitar = LoadSprite(ArtFolder + "/card_guitar.png");
            var rockHorns = LoadSprite(ArtFolder + "/card_rock_horns.png");

            var quietVerse = GetOrCreateCard(
                "PerfectCard", "quiet_verse", "Quiet Verse",
                "차분한 관객에게 분위기를 쌓아 올린다.",
                0f, 35f, new Color(0.15f, 0.55f, 0.35f), guitar);
            var crowdCall = GetOrCreateCard(
                "GoodCard", "crowd_call", "Crowd Call",
                "달아오르기 시작한 관객의 참여를 끌어낸다.",
                20f, 55f, new Color(0.2f, 0.4f, 0.65f), clapRhythm);
            var guitarSolo = GetOrCreateCard(
                "MissCard", "guitar_solo", "Guitar Solo",
                "충분히 달아오른 무대에서 솔로를 터뜨린다.",
                45f, 80f, new Color(0.55f, 0.35f, 0.2f), guitar);
            var stageDive = GetOrCreateCard(
                "RiskMissCard", "stage_dive", "Stage Dive",
                "절정에 가까운 관객에게 몸을 던진다.",
                70f, 100f, new Color(0.6f, 0.2f, 0.25f), rockHorns);

            var deck = GetOrCreateDeck(new[] { quietVerse, crowdCall, guitarSolo, stageDive });
            AssetDatabase.SaveAssets();
            return deck;
        }

        static CardData GetOrCreateCard(
            string assetName,
            string id,
            string displayName,
            string description,
            float favorableHypeMin,
            float favorableHypeMax,
            Color color,
            Sprite artwork)
        {
            string path = $"{CardFolder}/{assetName}.asset";
            var card = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (card == null)
            {
                card = ScriptableObject.CreateInstance<CardData>();
                AssetDatabase.CreateAsset(card, path);
            }

            var serialized = new SerializedObject(card);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("description").stringValue = description;
            serialized.FindProperty("favorableHypeMin").floatValue = favorableHypeMin;
            serialized.FindProperty("favorableHypeMax").floatValue = favorableHypeMax;
            serialized.FindProperty("artwork").objectReferenceValue = artwork;
            serialized.FindProperty("prototypeColor").colorValue = color;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(card);
            return card;
        }

        static CardDeckConfig GetOrCreateDeck(IReadOnlyList<CardData> prototypes)
        {
            var deck = AssetDatabase.LoadAssetAtPath<CardDeckConfig>(DeckPath);
            if (deck == null)
            {
                deck = ScriptableObject.CreateInstance<CardDeckConfig>();
                AssetDatabase.CreateAsset(deck, DeckPath);
            }

            // 각 프로토타입을 3장씩 넣어 기본 손패+앙코르 드로우 후에도 덱이 충분하도록 한다.
            var serialized = new SerializedObject(deck);
            var cards = serialized.FindProperty("cards");
            cards.arraySize = prototypes.Count * 3;
            int index = 0;
            for (int copy = 0; copy < 3; copy++)
            {
                for (int i = 0; i < prototypes.Count; i++)
                    cards.GetArrayElementAtIndex(index++).objectReferenceValue = prototypes[i];
            }

            serialized.FindProperty("minimumHandSize").intValue = 3;
            serialized.FindProperty("maximumHandSize").intValue = 9;
            serialized.FindProperty("shuffleOnStart").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(deck);
            return deck;
        }

        static void BuildCardSystem(CardDeckConfig deck)
        {
            var root = GameObject.Find("[CardSystem]");
            if (root == null)
            {
                root = new GameObject("[CardSystem]");
                Undo.RegisterCreatedObjectUndo(root, "Create CardSystem");
            }

            var system = EnsureComponent<CardSystem>(root);
            EnsureComponent<CardInput>(root);
            SetObjectField(system, "config", deck);
        }

        static void BuildCardCanvas()
        {
            var existing = Object.FindFirstObjectByType<CardHandUI>();
            if (existing != null)
            {
                UpgradeExistingCardCanvas(existing);
                return;
            }

            var canvasGo = GameObject.Find("CardCanvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("CardCanvas");
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create CardCanvas");
            }

            var canvas = EnsureComponent<Canvas>(canvasGo);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            var scaler = EnsureComponent<CanvasScaler>(canvasGo);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            EnsureComponent<GraphicRaycaster>(canvasGo);

            var handGo = CreateUIObject("CardHand", canvasGo.transform);
            var handRect = handGo.GetComponent<RectTransform>();
            handRect.anchorMin = handRect.anchorMax = new Vector2(0.5f, 0f);
            handRect.pivot = new Vector2(0.5f, 0f);
            handRect.anchoredPosition = new Vector2(0f, 55f);
            handRect.sizeDelta = new Vector2(1520f, 235f);

            var layout = handGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var slots = new CardSlotUI[9];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = CreateCardSlot(handGo.transform, i);

            var hint = CreateText("CardHint", canvasGo.transform, "숫자키로 카드를 선택", 24, TextAnchor.MiddleCenter);
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 16f);
            hintRect.sizeDelta = new Vector2(600f, 32f);

            var handUi = handGo.AddComponent<CardHandUI>();
            handUi.Configure(slots, hint);
        }

        static void UpgradeExistingCardCanvas(CardHandUI handUi)
        {
            const int targetSlotCount = 9;

            var hand = handUi.gameObject;
            var handRect = hand.GetComponent<RectTransform>();
            if (handRect != null)
            {
                Undo.RecordObject(handRect, "Resize Card Hand");
                handRect.sizeDelta = new Vector2(1520f, 235f);
            }

            var existingSlots = hand.GetComponentsInChildren<CardSlotUI>(true);
            if (existingSlots.Length == 0)
            {
                Debug.LogWarning("[Cards] 기존 CardHand에 복제할 카드 슬롯이 없습니다.");
                return;
            }

            var slots = new CardSlotUI[targetSlotCount];
            int reusedCount = Mathf.Min(existingSlots.Length, targetSlotCount);
            for (int i = 0; i < reusedCount; i++) slots[i] = existingSlots[i];

            for (int i = reusedCount; i < targetSlotCount; i++)
            {
                var clone = Object.Instantiate(existingSlots[0].gameObject, hand.transform);
                clone.name = $"CardSlot_{i + 1}";
                clone.transform.SetSiblingIndex(i);
                Undo.RegisterCreatedObjectUndo(clone, "Add Card Slot");
                slots[i] = clone.GetComponent<CardSlotUI>();
            }

            for (int i = 0; i < targetSlotCount; i++)
            {
                var slotRect = slots[i].GetComponent<RectTransform>();
                if (slotRect != null)
                {
                    Undo.RecordObject(slotRect, "Resize Card Slot");
                    slotRect.sizeDelta = new Vector2(150f, 225f);
                }

                var slotLayout = slots[i].GetComponent<LayoutElement>();
                if (slotLayout != null)
                {
                    Undo.RecordObject(slotLayout, "Resize Card Slot");
                    slotLayout.preferredWidth = 150f;
                    slotLayout.preferredHeight = 225f;
                }
            }

            var serialized = new SerializedObject(handUi);
            var slotsProperty = serialized.FindProperty("slots");
            slotsProperty.arraySize = targetSlotCount;
            for (int i = 0; i < targetSlotCount; i++)
                slotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(handUi);
        }

        static CardSlotUI CreateCardSlot(Transform parent, int index)
        {
            var slotGo = CreateUIObject($"CardSlot_{index + 1}", parent);
            var rect = slotGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(150f, 225f);

            var layout = slotGo.AddComponent<LayoutElement>();
            layout.preferredWidth = 150f;
            layout.preferredHeight = 225f;

            var background = slotGo.AddComponent<Image>();
            background.color = new Color(0.25f, 0.3f, 0.4f);
            background.raycastTarget = false;

            var number = CreateText("Number", slotGo.transform, (index + 1).ToString(), 24, TextAnchor.UpperLeft);
            SetRect(number.rectTransform, new Vector2(0f, 0.72f), Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, -10f));

            var title = CreateText("Title", slotGo.transform, "Card", 30, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0f, 0.33f), new Vector2(1f, 0.72f), new Vector2(10f, 0f), new Vector2(-10f, 0f));

            var delta = CreateText("Delta", slotGo.transform, "+0", 34, TextAnchor.MiddleCenter);
            SetRect(delta.rectTransform, Vector2.zero, new Vector2(1f, 0.33f), new Vector2(10f, 8f), new Vector2(-10f, -4f));

            var slot = slotGo.AddComponent<CardSlotUI>();
            slot.Configure(background, number, title, delta);
            return slot;
        }

        static Sprite LoadSprite(string path)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite) return sprite;
            }

            Debug.LogWarning($"[Cards] 카드 아트 Sprite를 찾지 못했습니다: {path}");
            return null;
        }

        static Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor alignment)
        {
            var go = CreateUIObject(name, parent);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return go;
        }

        static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }

        static void SetObjectField(Object target, string fieldName, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"[Cards] {target.GetType().Name}에서 '{fieldName}' 필드를 찾지 못했습니다.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
