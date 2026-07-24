using System.Collections.Generic;
using System;
using GameJamKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 프리팹 기반 카드 8종, 카드 풀 설정, 동적 손패 UI를 구성한다.
    /// 이미 생성된 카드 프리팹과 유효한 카드 풀은 덮어쓰지 않는다.
    /// </summary>
    public static class CardSetupMenu
    {
        sealed class ValidationCard
        {
            public ValidationCard(int id) => Id = id;
            public int Id { get; }
        }

        const string PrefabFolder = "Assets/Card_Prefab";
        const string BasePrefabPath = PrefabFolder + "/Card_Base.prefab";
        const string CardSettingsFolder = "Assets/Settings/Cards";
        const string DeckPath = CardSettingsFolder + "/CardDeckConfig.asset";
        const string HypeConfigPath = "Assets/Settings/HypeConfig.asset";
        const string GuitarArtPath = "Assets/Art_Assets/card_guitar.png";

        readonly struct CardSeed
        {
            public CardSeed(
                string fileName,
                string id,
                string displayName,
                string description,
                CardRole role,
                HeatStage targetStage,
                UtilityCardEffect utilityEffect,
                Color color)
            {
                FileName = fileName;
                Id = id;
                DisplayName = displayName;
                Description = description;
                Role = role;
                TargetStage = targetStage;
                UtilityEffect = utilityEffect;
                Color = color;
            }

            public string FileName { get; }
            public string Id { get; }
            public string DisplayName { get; }
            public string Description { get; }
            public CardRole Role { get; }
            public HeatStage TargetStage { get; }
            public UtilityCardEffect UtilityEffect { get; }
            public Color Color { get; }
        }

        [MenuItem("Tools/Cards/Setup Prefab Card System", false, 0)]
        public static void SetupPrefabCardSystem()
        {
            var deck = SyncCardPrefabsAndDeck();
            UpgradeLegacyHypeTiers();
            var cardInput = BuildCardSystem(deck);
            BuildCardCanvas(cardInput);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Cards] 프리팹 카드 8종, 이중 10장 덱, 동적 4장 손패 셋업 완료.");
        }

        [MenuItem("Tools/Cards/Setup Card Prototype", false, 100)]
        static void SetupLegacyMenuAlias() => SetupPrefabCardSystem();

        [MenuItem("Tools/Cards/Validate Prefab Card System", false, 1)]
        public static void ValidatePrefabCardSystem()
        {
            var errors = new List<string>();
            var deck = AssetDatabase.LoadAssetAtPath<CardDeckConfig>(DeckPath);
            if (deck == null)
            {
                errors.Add("CardDeckConfig가 없습니다.");
            }
            else
            {
                if (deck.CardPool.Count < 8) errors.Add($"초기 카드 풀이 8종보다 적습니다: {deck.CardPool.Count}");
                if (deck.GeneratedDeckSize != 10) errors.Add($"덱 크기가 10이 아닙니다: {deck.GeneratedDeckSize}");
                if (deck.PreparedDeckCount != 2) errors.Add($"준비 덱 수가 2가 아닙니다: {deck.PreparedDeckCount}");
                if (deck.MinimumHandSize != 3) errors.Add($"기본 손패가 3이 아닙니다: {deck.MinimumHandSize}");
                if (deck.MaximumHandSize != 4) errors.Add($"최대 손패가 4가 아닙니다: {deck.MaximumHandSize}");

                for (int i = 0; i < deck.CardPool.Count; i++)
                {
                    var entry = deck.CardPool[i];
                    if (entry == null || entry.Prefab == null) errors.Add($"카드 풀 {i + 1}번 프리팹이 비었습니다.");
                    else if (!Mathf.Approximately(entry.Weight, 1f)) errors.Add($"카드 풀 {i + 1}번 가중치가 1이 아닙니다.");
                }
            }

            var seeds = BuildSeeds();
            for (int i = 0; i < seeds.Length; i++)
            {
                string path = $"{PrefabFolder}/{seeds[i].FileName}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    errors.Add($"{seeds[i].FileName} 프리팹이 없습니다.");
                    continue;
                }
                if (prefab.GetComponent<CardDefinition>() == null) errors.Add($"{seeds[i].FileName}에 CardDefinition이 없습니다.");
                if (prefab.GetComponent<CardSlotUI>() == null) errors.Add($"{seeds[i].FileName}에 CardSlotUI가 없습니다.");
            }

            ValidatePreparedDeck(errors);
            ValidateCardEffects(errors);

            var handUi = Object.FindFirstObjectByType<CardHandUI>();
            if (handUi == null) errors.Add("씬에 CardHandUI가 없습니다.");
            else if (handUi.GetComponentsInChildren<CardSlotUI>(true).Length != 0)
                errors.Add("씬 CardHand 아래에 레거시 고정 카드 슬롯이 남아 있습니다.");

            if (errors.Count > 0)
                throw new InvalidOperationException("[Cards] 검증 실패:\n- " + string.Join("\n- ", errors));

            Debug.Log("[Cards] 검증 완료: 카드 8종, 가중치 1, 이중 10장 덱, 3~4장 손패, 카드 효과 설정이 정상입니다.");
        }

        [MenuItem("Tools/Cards/Run Play Mode Smoke Test", false, 2)]
        public static void RunPlayModeSmokeTest()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("[Cards] Play Mode에서만 실행할 수 있습니다.");
            if (!CardSystem.HasInstance || !HypeSystem.HasInstance || !GameManager.HasInstance)
                throw new InvalidOperationException("[Cards] Play Mode 필수 시스템을 찾지 못했습니다.");

            var gameManager = GameManager.Instance;
            if (gameManager.State == GameState.Ready) gameManager.StartGame();
            if (!gameManager.IsPlaying)
                throw new InvalidOperationException("[Cards] 게임을 Playing 상태로 전환하지 못했습니다.");

            var system = CardSystem.Instance;
            if (system.HandCount != 3)
                throw new InvalidOperationException($"[Cards] 초기 손패가 3장이 아닙니다: {system.HandCount}");
            if (system.PreparedDeckCount != 2)
                throw new InvalidOperationException($"[Cards] 초기 준비 덱이 2개가 아닙니다: {system.PreparedDeckCount}");

            var firstCard = system.GetCard(0);
            float hypeBefore = Hype.Current;
            int scoreBefore = gameManager.Score;
            int handBefore = system.HandCount;
            var firstResult = CardEffectResolver.Resolve(
                firstCard,
                hypeBefore,
                HypeSystem.Instance.Config,
                SpecialCardRequest.None);
            int expectedScoreGain = Mathf.RoundToInt(firstResult.BaseScore * Hype.MultiplierFor(hypeBefore));
            float expectedHype = Mathf.Clamp(
                hypeBefore + firstResult.HeatDelta,
                0f,
                HypeSystem.Instance.Config.maxHype);

            if (!system.SelectCard(0))
                throw new InvalidOperationException("[Cards] 첫 카드 사용에 실패했습니다.");
            if (gameManager.Score - scoreBefore != expectedScoreGain)
                throw new InvalidOperationException("[Cards] 카드 사용 전 열기 배율로 점수가 계산되지 않았습니다.");
            if (!Mathf.Approximately(Hype.Current, expectedHype))
                throw new InvalidOperationException("[Cards] 점수 계산 후 열기 변화가 적용되지 않았습니다.");

            int expectedFirstHand = firstCard.Role == CardRole.Utility &&
                                    firstCard.UtilityEffect == UtilityCardEffect.Draw
                ? Mathf.Min(system.MaximumHandSize, handBefore - 1 + firstCard.DrawCount)
                : firstCard.Role == CardRole.Utility &&
                  firstCard.UtilityEffect == UtilityCardEffect.Reroll
                    ? firstCard.RerollDrawCount
                    : system.MinimumHandSize;
            if (system.HandCount != expectedFirstHand)
                throw new InvalidOperationException(
                    $"[Cards] 첫 카드 효과 후 손패가 예상과 다릅니다: {system.HandCount}/{expectedFirstHand}");

            // 충분히 사용해 10장 묶음 경계를 여러 번 넘기고도 항상 대기 덱이 유지되는지 확인한다.
            for (int i = 0; i < 30; i++)
            {
                if (system.HandCount < system.MinimumHandSize || system.HandCount > system.MaximumHandSize)
                    throw new InvalidOperationException($"[Cards] {i + 1}회차 손패 범위가 잘못되었습니다: {system.HandCount}");
                if (!system.SelectCard(0))
                    throw new InvalidOperationException($"[Cards] {i + 1}회차 연속 카드 사용에 실패했습니다.");
                if (system.PreparedDeckCount != 2 || system.CurrentDeckRemaining <= 0)
                    throw new InvalidOperationException(
                        $"[Cards] {i + 1}회차 덱 승격/대기 덱 보충에 실패했습니다.");
            }

            NormalizeHandToMinimum(system);
            ExerciseUtilityCard(system, UtilityCardEffect.Draw);
            ExerciseUtilityCard(system, UtilityCardEffect.Reroll);

            Debug.Log(
                $"[Cards] Play Mode 스모크 테스트 완료: 첫 카드={firstCard.DisplayName}, " +
                $"첫 사용 후 손패={expectedFirstHand}, 드로우 3→4장, 리롤→3장, " +
                $"30회 연속 사용 및 덱 경계 통과 정상.");
        }

        static void NormalizeHandToMinimum(CardSystem system)
        {
            for (int attempt = 0; attempt < 100 && system.HandCount > system.MinimumHandSize; attempt++)
            {
                int index = -1;
                for (int i = 0; i < system.HandCount; i++)
                {
                    var candidate = system.GetCard(i);
                    if (candidate.UtilityEffect != UtilityCardEffect.Draw)
                    {
                        index = i;
                        break;
                    }
                }
                if (index < 0) index = 0;
                if (!system.SelectCard(index))
                    throw new InvalidOperationException("[Cards] 손패를 3장으로 정리하지 못했습니다.");
            }

            if (system.HandCount != system.MinimumHandSize)
                throw new InvalidOperationException("[Cards] 드로우 검증 전 손패를 3장으로 맞추지 못했습니다.");
        }

        static void ExerciseUtilityCard(CardSystem system, UtilityCardEffect targetEffect)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                int targetIndex = -1;
                for (int i = 0; i < system.HandCount; i++)
                {
                    var candidate = system.GetCard(i);
                    if (candidate.Role == CardRole.Utility && candidate.UtilityEffect == targetEffect)
                    {
                        targetIndex = i;
                        break;
                    }
                }

                if (targetIndex >= 0)
                {
                    int before = system.HandCount;
                    var card = system.GetCard(targetIndex);
                    if (!system.SelectCard(targetIndex))
                        throw new InvalidOperationException($"[Cards] {targetEffect} 카드 사용에 실패했습니다.");

                    int expected = targetEffect == UtilityCardEffect.Draw
                        ? Mathf.Min(system.MaximumHandSize, before - 1 + card.DrawCount)
                        : card.RerollDrawCount;
                    if (system.HandCount != expected)
                        throw new InvalidOperationException(
                            $"[Cards] {targetEffect} 후 손패가 예상과 다릅니다: {system.HandCount}/{expected}");
                    return;
                }

                if (!system.SelectCard(0))
                    throw new InvalidOperationException($"[Cards] {targetEffect} 탐색 중 카드 사용에 실패했습니다.");
            }

            throw new InvalidOperationException($"[Cards] 200회 안에 {targetEffect} 카드를 찾지 못했습니다.");
        }

        public static CardDeckConfig SyncCardPrefabsAndDeck()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(CardSettingsFolder);

            var guitar = LoadSprite(GuitarArtPath);
            CreateBasePrefab();

            var seeds = BuildSeeds();
            var cards = new List<CardDefinition>(seeds.Length);
            for (int i = 0; i < seeds.Length; i++)
            {
                var card = GetOrCreateCardVariant(seeds[i], guitar);
                if (card != null) cards.Add(card);
            }

            return GetOrCreateDeck(cards);
        }

        static CardSeed[] BuildSeeds()
        {
            return new[]
            {
                new CardSeed(
                    "Card_01_TempoUp", "tempo_up", "템포 끌어올리기",
                    "차분한 무대의 템포를 끌어올린다.",
                    CardRole.Normal, HeatStage.Chill, UtilityCardEffect.None,
                    FromHex("#31DFEA")),
                new CardSeed(
                    "Card_02_Response", "response_call", "호응 유도",
                    "관객의 첫 반응을 이끌어낸다.",
                    CardRole.Special, HeatStage.Chill, UtilityCardEffect.None,
                    FromHex("#16A9B3")),
                new CardSeed(
                    "Card_03_HandsUp", "hands_up", "손 머리 위로!",
                    "함께 따라 할 동작으로 무대를 묶는다.",
                    CardRole.Normal, HeatStage.Singalong, UtilityCardEffect.None,
                    FromHex("#6431EA")),
                new CardSeed(
                    "Card_04_PassMic", "pass_mic", "마이크 넘기기",
                    "관객에게 노래를 맡겨 열기를 이어간다.",
                    CardRole.Special, HeatStage.Singalong, UtilityCardEffect.None,
                    FromHex("#8C5CFF")),
                new CardSeed(
                    "Card_05_GuitarSolo", "guitar_solo", "기타 솔로",
                    "달아오른 무대에 강렬한 솔로를 터뜨린다.",
                    CardRole.Normal, HeatStage.Mosh, UtilityCardEffect.None,
                    FromHex("#F01F1F")),
                new CardSeed(
                    "Card_06_MoshPit", "open_mosh_pit", "모쉬핏 열어!",
                    "폭발 직전의 관객을 모쉬핏으로 끌어낸다.",
                    CardRole.Special, HeatStage.Mosh, UtilityCardEffect.None,
                    FromHex("#B51230")),
                new CardSeed(
                    "Card_07_Draw", "draw_two", "드로우",
                    "덱에서 카드 2장을 획득한다.",
                    CardRole.Utility, HeatStage.Chill, UtilityCardEffect.Draw,
                    FromHex("#F2C94C")),
                new CardSeed(
                    "Card_08_Reroll", "reroll_hand", "리롤",
                    "현재 패를 모두 없애고 카드 3장을 뽑는다.",
                    CardRole.Utility, HeatStage.Chill, UtilityCardEffect.Reroll,
                    FromHex("#F2994A"))
            };
        }

        static void ValidatePreparedDeck(List<string> errors)
        {
            int nextId = 1;
            var prepared = new PreparedDeck<ValidationCard>(10, 2, () => new ValidationCard(nextId++));
            prepared.Reset();
            if (prepared.PreparedBatchCount != 2 || prepared.CurrentRemaining != 10)
                errors.Add("초기 현재/대기 덱 준비 상태가 올바르지 않습니다.");

            for (int i = 1; i <= 9; i++)
            {
                var card = prepared.Draw();
                if (card == null || card.Id != i)
                {
                    errors.Add("현재 덱 드로우 순서 검증에 실패했습니다.");
                    return;
                }
            }

            var lastCurrent = prepared.Draw();
            var firstStandby = prepared.Draw();
            var secondStandby = prepared.Draw();
            if (lastCurrent == null || lastCurrent.Id != 10 ||
                firstStandby == null || firstStandby.Id != 11 ||
                secondStandby == null || secondStandby.Id != 12 ||
                prepared.PreparedBatchCount != 2 ||
                prepared.CurrentRemaining != 8)
            {
                errors.Add("현재 덱 1장 + 대기 덱 2장 경계 드로우 검증에 실패했습니다.");
            }
        }

        static void ValidateCardEffects(List<string> errors)
        {
            var config = AssetDatabase.LoadAssetAtPath<HypeConfig>(HypeConfigPath);
            var chill = LoadCardDefinition("Card_01_TempoUp");
            var special = LoadCardDefinition("Card_04_PassMic");
            var mosh = LoadCardDefinition("Card_05_GuitarSolo");
            var draw = LoadCardDefinition("Card_07_Draw");
            var reroll = LoadCardDefinition("Card_08_Reroll");
            if (config == null || chill == null || special == null || mosh == null || draw == null || reroll == null)
            {
                errors.Add("카드 효과 검증에 필요한 에셋이 없습니다.");
                return;
            }

            var exact = CardEffectResolver.Resolve(chill, 49f, config, SpecialCardRequest.None);
            if (exact.BaseScore != 100 || !Mathf.Approximately(exact.HeatDelta, 12f))
                errors.Add("일반 카드 단계 일치 효과가 100점/+12가 아닙니다.");

            var far = CardEffectResolver.Resolve(mosh, 30f, config, SpecialCardRequest.None);
            if (far.BaseScore != 0 || !Mathf.Approximately(far.HeatDelta, -5f))
                errors.Add("일반 카드 두 단계 차이 효과가 0점/-5가 아닙니다.");

            var specialHit = CardEffectResolver.Resolve(
                special,
                10f,
                config,
                new SpecialCardRequest(HeatStage.Singalong));
            if (!specialHit.IsSpecialHit ||
                specialHit.BaseScore != 400 ||
                !Mathf.Approximately(specialHit.HeatDelta, 25f))
            {
                errors.Add("특수 히트 효과가 400점/+25가 아닙니다.");
            }

            if (draw.UtilityEffect != UtilityCardEffect.Draw || draw.DrawCount != 2)
                errors.Add("드로우 카드 효과가 2장 획득이 아닙니다.");
            if (reroll.UtilityEffect != UtilityCardEffect.Reroll || reroll.RerollDrawCount != 3)
                errors.Add("리롤 카드 효과가 전체 제거 후 3장 획득이 아닙니다.");
            if (!Mathf.Approximately(config.GetMultiplier(0.49f), 1f) ||
                !Mathf.Approximately(config.GetMultiplier(0.50f), 3f) ||
                !Mathf.Approximately(config.GetMultiplier(0.80f), 5f))
            {
                errors.Add("열기 단계 배율이 Chill ×1 / Singalong ×3 / Mosh ×5가 아닙니다.");
            }
        }

        static CardDefinition LoadCardDefinition(string fileName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{fileName}.prefab");
            return prefab != null ? prefab.GetComponent<CardDefinition>() : null;
        }

        static void CreateBasePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath) != null) return;

            var root = CreateUIObject("Card_Base", null);
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160f, 240f);

            var layout = root.AddComponent<LayoutElement>();
            layout.preferredWidth = 160f;
            layout.preferredHeight = 240f;

            var background = root.AddComponent<Image>();
            background.color = new Color(0.3f, 0.3f, 0.3f);
            background.raycastTarget = true;

            root.AddComponent<Canvas>();
            root.AddComponent<CanvasGroup>();
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<CardDragHandler>();

            var art = CreateImage("Artwork", root.transform);
            SetRect(
                art.rectTransform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            art.preserveAspect = true;

            var role = CreateText("Role", root.transform, "CHILL", 15, TextAnchor.UpperRight);
            SetRect(
                role.rectTransform,
                new Vector2(0.22f, 0.86f),
                Vector2.one,
                Vector2.zero,
                new Vector2(-8f, -6f));

            var title = CreateText("Title", root.transform, "Card", 25, TextAnchor.MiddleCenter);
            SetRect(
                title.rectTransform,
                new Vector2(0.04f, 0.23f),
                new Vector2(0.96f, 0.40f),
                Vector2.zero,
                Vector2.zero);

            var description = CreateText("Description", root.transform, "", 17, TextAnchor.MiddleCenter);
            SetRect(
                description.rectTransform,
                new Vector2(0.06f, 0.03f),
                new Vector2(0.94f, 0.24f),
                Vector2.zero,
                Vector2.zero);

            root.AddComponent<CardDefinition>();
            var view = root.AddComponent<CardSlotUI>();
            view.Configure(background, art, title, description, role);

            PrefabUtility.SaveAsPrefabAsset(root, BasePrefabPath);
            Object.DestroyImmediate(root);
        }

        static CardDefinition GetOrCreateCardVariant(CardSeed seed, Sprite guitar)
        {
            string path = $"{PrefabFolder}/{seed.FileName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<CardDefinition>();

            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
            if (basePrefab == null)
            {
                Debug.LogError($"[Cards] 베이스 카드 프리팹을 찾지 못했습니다: {BasePrefabPath}");
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(basePrefab) as GameObject;
            if (instance == null) return null;

            instance.name = seed.FileName;
            ConfigureCardDefinition(instance.GetComponent<CardDefinition>(), seed, guitar);
            var variant = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);

            return variant != null ? variant.GetComponent<CardDefinition>() : null;
        }

        static void ConfigureCardDefinition(CardDefinition card, CardSeed seed, Sprite guitar)
        {
            var serialized = new SerializedObject(card);
            serialized.FindProperty("id").stringValue = seed.Id;
            serialized.FindProperty("displayName").stringValue = seed.DisplayName;
            serialized.FindProperty("description").stringValue = seed.Description;
            serialized.FindProperty("role").enumValueIndex = (int)seed.Role;
            serialized.FindProperty("targetStage").enumValueIndex = (int)seed.TargetStage;
            serialized.FindProperty("targetPreference").enumValueIndex = (int)seed.TargetStage;
            serialized.FindProperty("utilityEffect").enumValueIndex = (int)seed.UtilityEffect;
            serialized.FindProperty("artwork").objectReferenceValue = guitar;
            serialized.FindProperty("cardColor").colorValue = seed.Color;

            bool normal = seed.Role == CardRole.Normal;
            bool special = seed.Role == CardRole.Special;
            serialized.FindProperty("exactBaseScore").intValue = normal ? 100 : special ? 150 : 0;
            serialized.FindProperty("adjacentBaseScore").intValue = normal ? 50 : special ? 60 : 0;
            serialized.FindProperty("farBaseScore").intValue = 0;
            serialized.FindProperty("exactHeatDelta").floatValue = normal ? 12f : special ? 8f : 0f;
            serialized.FindProperty("adjacentHeatDelta").floatValue = normal ? 4f : special ? 2f : 0f;
            serialized.FindProperty("farHeatDelta").floatValue = normal || special ? -5f : 0f;
            serialized.FindProperty("specialHitBaseScore").intValue = special ? 400 : 0;
            serialized.FindProperty("specialHitHeatDelta").floatValue = special ? 25f : 0f;
            serialized.FindProperty("drawCount").intValue = seed.UtilityEffect == UtilityCardEffect.Draw ? 2 : 0;
            serialized.FindProperty("rerollDrawCount").intValue = 3;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(card);
        }

        static CardDeckConfig GetOrCreateDeck(IReadOnlyList<CardDefinition> initialCards)
        {
            var deck = AssetDatabase.LoadAssetAtPath<CardDeckConfig>(DeckPath);
            if (deck == null)
            {
                deck = ScriptableObject.CreateInstance<CardDeckConfig>();
                AssetDatabase.CreateAsset(deck, DeckPath);
            }

            var serialized = new SerializedObject(deck);
            var pool = serialized.FindProperty("cardPool");
            bool needsInitialPool = pool != null && pool.arraySize == 0;
            if (needsInitialPool)
            {
                pool.arraySize = initialCards.Count;
                for (int i = 0; i < initialCards.Count; i++)
                {
                    var entry = pool.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("prefab").objectReferenceValue = initialCards[i];
                    entry.FindPropertyRelative("weight").floatValue = 1f;
                }
            }

            if (needsInitialPool)
            {
                serialized.FindProperty("generatedDeckSize").intValue = 10;
                serialized.FindProperty("preparedDeckCount").intValue = 2;
                serialized.FindProperty("minimumHandSize").intValue = 3;
                serialized.FindProperty("maximumHandSize").intValue = 4;
            }
            else
            {
                var maximum = serialized.FindProperty("maximumHandSize");
                if (maximum != null && maximum.intValue > 4) maximum.intValue = 4;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(deck);
            return deck;
        }

        static void UpgradeLegacyHypeTiers()
        {
            var config = AssetDatabase.LoadAssetAtPath<HypeConfig>(HypeConfigPath);
            if (config == null) return;

            var serialized = new SerializedObject(config);
            var tiers = serialized.FindProperty("multiplierTiers");
            if (!IsLegacyFourTierSetup(tiers)) return;

            serialized.FindProperty("singalongMinHype").floatValue = 50f;
            serialized.FindProperty("moshMinHype").floatValue = 80f;
            tiers.arraySize = 3;
            SetTier(tiers.GetArrayElementAtIndex(0), "Chill", 0f, 1f);
            SetTier(tiers.GetArrayElementAtIndex(1), "Singalong", 0.5f, 3f);
            SetTier(tiers.GetArrayElementAtIndex(2), "Mosh", 0.8f, 5f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        static bool IsLegacyFourTierSetup(SerializedProperty tiers)
        {
            if (tiers == null || tiers.arraySize != 4) return false;

            return ApproximatelyTier(tiers.GetArrayElementAtIndex(0), 0f, 1f) &&
                   ApproximatelyTier(tiers.GetArrayElementAtIndex(1), 0.34f, 2f) &&
                   ApproximatelyTier(tiers.GetArrayElementAtIndex(2), 0.67f, 3f) &&
                   ApproximatelyTier(tiers.GetArrayElementAtIndex(3), 0.9f, 5f);
        }

        static bool ApproximatelyTier(SerializedProperty tier, float min, float multiplier)
        {
            return Mathf.Approximately(tier.FindPropertyRelative("minNormalized").floatValue, min) &&
                   Mathf.Approximately(tier.FindPropertyRelative("multiplier").floatValue, multiplier);
        }

        static void SetTier(SerializedProperty tier, string label, float min, float multiplier)
        {
            tier.FindPropertyRelative("label").stringValue = label;
            tier.FindPropertyRelative("minNormalized").floatValue = min;
            tier.FindPropertyRelative("multiplier").floatValue = multiplier;
        }

        static CardInput BuildCardSystem(CardDeckConfig deck)
        {
            var root = GameObject.Find("[CardSystem]");
            if (root == null)
            {
                root = new GameObject("[CardSystem]");
                Undo.RegisterCreatedObjectUndo(root, "Create CardSystem");
            }

            var system = EnsureComponent<CardSystem>(root);
            var cardInput = EnsureComponent<CardInput>(root);
            SetObjectField(system, "config", deck);

            var scoreRoot = GameObject.Find("[ScoreSystem]");
            if (scoreRoot == null)
            {
                scoreRoot = new GameObject("[ScoreSystem]");
                Undo.RegisterCreatedObjectUndo(scoreRoot, "Create ScoreSystem");
            }
            EnsureComponent<PerformanceScoreSystem>(scoreRoot);
            return cardInput;
        }

        static void BuildCardCanvas(CardInput cardInput)
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            var eventSystemGo = eventSystem != null ? eventSystem.gameObject : null;
            if (eventSystemGo == null)
            {
                eventSystemGo = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create EventSystem");
            }

            EnsureComponent<EventSystem>(eventSystemGo);
            var inputModule = EnsureComponent<InputSystemUIInputModule>(eventSystemGo);
            if (inputModule.actionsAsset == null) inputModule.AssignDefaultActions();

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

            var dragLayer = canvasGo.transform.Find("CardDragLayer") as RectTransform;
            if (dragLayer == null)
            {
                var dragLayerGo = CreateUIObject("CardDragLayer", canvasGo.transform);
                Undo.RegisterCreatedObjectUndo(dragLayerGo, "Create Card Drag Layer");
                dragLayer = dragLayerGo.GetComponent<RectTransform>();
            }
            SetRect(dragLayer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var handUi = Object.FindFirstObjectByType<CardHandUI>();
            var handGo = handUi != null ? handUi.gameObject : GameObject.Find("CardHand");
            if (handGo == null)
            {
                handGo = CreateUIObject("CardHand", canvasGo.transform);
                Undo.RegisterCreatedObjectUndo(handGo, "Create CardHand");
            }

            var oldSlots = handGo.GetComponentsInChildren<CardSlotUI>(true);
            for (int i = 0; i < oldSlots.Length; i++)
            {
                if (oldSlots[i] != null && oldSlots[i].gameObject != handGo)
                    Undo.DestroyObjectImmediate(oldSlots[i].gameObject);
            }

            var handRect = handGo.GetComponent<RectTransform>();
            handRect.anchorMin = handRect.anchorMax = new Vector2(0.5f, 0f);
            handRect.pivot = new Vector2(0.5f, 0f);
            handRect.anchoredPosition = new Vector2(0f, 55f);
            handRect.sizeDelta = new Vector2(800f, 260f);

            var layout = EnsureComponent<HorizontalLayoutGroup>(handGo);
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var hintGo = GameObject.Find("CardHint");
            Text hint = hintGo != null ? hintGo.GetComponent<Text>() : null;
            if (hint == null)
            {
                hint = CreateText("CardHint", canvasGo.transform, "숫자키로 카드를 선택", 24, TextAnchor.MiddleCenter);
                var hintRect = hint.rectTransform;
                hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0f);
                hintRect.pivot = new Vector2(0.5f, 0f);
                hintRect.anchoredPosition = new Vector2(0f, 16f);
                hintRect.sizeDelta = new Vector2(600f, 32f);
            }
            hint.text = "카드를 위로 드래그해 사용";

            handUi = handUi != null ? handUi : EnsureComponent<CardHandUI>(handGo);
            handUi.Configure(handGo.transform, hint, dragLayer, cardInput);
            dragLayer.SetAsLastSibling();
            EditorUtility.SetDirty(handUi);
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

        static Image CreateImage(string name, Transform parent)
        {
            var go = CreateUIObject(name, parent);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
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
            if (parent != null) go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            return go;
        }

        static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
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
            EditorUtility.SetDirty(target);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static Color FromHex(string hex)
            => ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.white;
    }
}
