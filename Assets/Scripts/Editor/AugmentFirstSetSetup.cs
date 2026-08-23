#if UNITY_EDITOR
using System.Collections.Generic;
using ContextStage;
using UnityEditor;
using UnityEngine;

namespace ContextStageEditor
{
    public static class AugmentFirstSetSetup
    {
        const string PrefabFolder = "Assets/Prefabs/Augments";
        const string ResourceFolder = "Assets/Resources/Augments";
        const string RewardTableFolder = ResourceFolder + "/RewardTables";
        const string CatalogPath = ResourceFolder + "/AugmentCatalog.asset";
        const string CardPrefabFolder = "Assets/Card_Prefab";
        const string CardResourceFolder = "Assets/Resources/Cards";
        const string CardCatalogPath = CardResourceFolder + "/CardCatalog.asset";
        const string CardDeckConfigPath = "Assets/Settings/Cards/CardDeckConfig.asset";
        const string CardBasePrefabPath = CardPrefabFolder + "/Card_Base.prefab";
        const string EncoreBasePrefabPath = CardPrefabFolder + "/Card_EncoreBase.prefab";
        const string StageControlBasePrefabPath =
            CardPrefabFolder + "/Card_StageControlBase.prefab";

        [MenuItem("Tools/Tour/Setup First Augments", false, 20)]
        public static void Setup()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(ResourceFolder);
            EnsureFolder(RewardTableFolder);
            EnsureFolder(CardResourceFolder);

            CardDefinition[] encoreCards = CreateEncoreCards();
            CardDefinition[] stageControlCards = CreateStageControlCards();

            AugmentDefinition fever = CreateDefinitionIfMissing(
                "Augment_FeverDuration",
                "fever_duration",
                "피버 연장",
                AugmentEffectType.FeverDurationSeconds,
                new[] { 1f, 2f, 3f },
                value => $"피버 시간이 {value:0}초 증가한다.");

            AugmentDefinition performanceTime = CreateDefinitionIfMissing(
                "Augment_PerformanceDuration",
                "performance_duration",
                "앙코르",
                AugmentEffectType.GrantCard,
                new[] { 0f, 0f, 0f },
                _ => "매 덱 묶음에 공연 시간을 연장하는 앙코르 카드를 추가한다.");

            ConfigureGrantedCardAugment(performanceTime, encoreCards, "앙코르");

            AugmentDefinition initialAudience = CreateDefinitionIfMissing(
                "Augment_InitialAudience",
                "initial_audience",
                "사전 홍보",
                AugmentEffectType.InitialAudienceCount,
                new[] { 1f, 2f, 3f },
                value => $"공연 시작 시 일반 관객이 {value:0}명 추가된다.");

            AugmentDefinition audiencePromotion = CreateDefinitionIfMissing(
                "Augment_AudiencePromotion",
                "audience_promotion",
                "공연 홍보",
                AugmentEffectType.AudienceArrivalIntervalReductionSeconds,
                new[] { 0.5f, 1f, 1.5f },
                value => $"일반 관객의 자연 유입 주기가 {value:0.#}초 짧아진다.");

            AugmentDefinition bandmateCover = CreateDefinitionIfMissing(
                "Augment_BandmateCover",
                "bandmate_cover",
                "동료 커버",
                AugmentEffectType.ComboBreakPreventionCount,
                new[] { 1f, 2f, 3f },
                value => $"공연마다 콤보 끊김을 {value:0}회 방지한다.");

            AugmentDefinition extraHand = CreateDefinitionIfMissing(
                "Augment_ExtraHand",
                "extra_hand",
                "한 장의 여유",
                AugmentEffectType.MinimumHandSizeIncrease,
                new[] { 0f, 0f, 1f },
                _ => "기본 손패가 3장에서 4장으로 증가한다.");
            ConfigureGoldOnlyAugment(
                extraHand,
                1f,
                "기본 손패가 3장에서 4장으로 증가한다.");

            AugmentDefinition preferenceInsight = CreateDefinitionIfMissing(
                "Augment_PreferenceInsight",
                "preference_insight",
                "취향 간파",
                AugmentEffectType.RevealAudiencePreferences,
                new[] { 0f, 0f, 1f },
                _ => "모든 일반 관객의 취향 테두리가 항상 표시된다.");
            ConfigureGoldOnlyAugment(
                preferenceInsight,
                1f,
                "모든 일반 관객의 취향 테두리가 항상 표시된다.");

            AugmentDefinition stageControl = CreateDefinitionIfMissing(
                "Augment_StageControl",
                "stage_control",
                "무대 장악",
                AugmentEffectType.GrantCard,
                new[] { 0f, 0f, 0f },
                _ => "매 덱 묶음에 무대 장악 카드를 추가한다.");
            ConfigureGrantedCardAugment(
                stageControl,
                stageControlCards,
                "무대 장악");
            ConfigureOwnershipPolicy(
                stageControl,
                AugmentTierOwnershipPolicy.IndependentTiers);

            AugmentDefinition breathingRoom = CreateDefinitionIfMissing(
                "Augment_BreathingRoom",
                "breathing_room",
                "숨 고르기",
                AugmentEffectType.PeriodicIdleDrawSeconds,
                new[] { 5f, 4f, 3f },
                value =>
                    $"카드를 {value:0}초 동안 사용하지 않으면 카드 1장을 뽑는다. " +
                    "계속 사용하지 않으면 같은 주기로 반복한다.");
            ConfigureOwnershipPolicy(
                breathingRoom,
                AugmentTierOwnershipPolicy.OneTierPerRun);

            CreateOrUpdateCatalog(new[]
            {
                fever,
                performanceTime,
                initialAudience,
                audiencePromotion,
                bandmateCover,
                extraHand,
                preferenceInsight,
                stageControl,
                breathingRoom
            });
            var augmentCards = new List<CardDefinition>(6);
            augmentCards.AddRange(encoreCards);
            augmentCards.AddRange(stageControlCards);
            CreateOrUpdateCardCatalog(augmentCards);

            // 티어 확률은 아직 미확정이므로 첫 검증 버전에서는 모두 같은 가중치로 둔다.
            CreateRewardTableIfMissing("reward_basic", 1f, 1f, 1f, 1);
            CreateRewardTableIfMissing("reward_advanced", 1f, 1f, 1f, 1);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[AugmentFirstSetSetup] 증강 9종과 지급 카드 6종, " +
                "런 덱 카탈로그 구성을 완료했습니다.");
        }

        static CardDefinition[] CreateEncoreCards()
        {
            GameObject basePrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(CardBasePrefabPath);
            if (basePrefab == null)
            {
                Debug.LogError(
                    $"[AugmentFirstSetSetup] 카드 베이스 프리팹이 없습니다: " +
                    CardBasePrefabPath);
                return new CardDefinition[3];
            }

            CardDefinition drawCard = LoadCardDefinition("Card_07_Draw");
            Sprite placeholderArtwork = drawCard == null ? null : drawCard.Artwork;
            CardDefinition encoreBase = CreateEncoreBaseIfMissing(
                basePrefab,
                placeholderArtwork);
            if (encoreBase == null) return new CardDefinition[3];

            return new[]
            {
                CreateEncoreTierIfMissing(
                    encoreBase.gameObject,
                    "Card_Encore_Bronze",
                    "encore_bronze",
                    "앙코르 · 브론즈",
                    5f,
                    new Color32(0xCD, 0x7F, 0x32, 0xFF)),
                CreateEncoreTierIfMissing(
                    encoreBase.gameObject,
                    "Card_Encore_Silver",
                    "encore_silver",
                    "앙코르 · 실버",
                    7f,
                    new Color32(0xC0, 0xC0, 0xC0, 0xFF)),
                CreateEncoreTierIfMissing(
                    encoreBase.gameObject,
                    "Card_Encore_Gold",
                    "encore_gold",
                    "앙코르 · 골드",
                    10f,
                    new Color32(0xFF, 0xD7, 0x00, 0xFF))
            };
        }

        static CardDefinition CreateEncoreBaseIfMissing(
            GameObject cardBasePrefab,
            Sprite placeholderArtwork)
        {
            GameObject existing =
                AssetDatabase.LoadAssetAtPath<GameObject>(EncoreBasePrefabPath);
            if (existing != null) return existing.GetComponent<CardDefinition>();

            GameObject instance =
                PrefabUtility.InstantiatePrefab(cardBasePrefab) as GameObject;
            if (instance == null) return null;

            try
            {
                instance.name = "Card_EncoreBase";
                CardDefinition card = instance.GetComponent<CardDefinition>();
                var serialized = new SerializedObject(card);
                serialized.FindProperty("id").stringValue = "encore_base";
                serialized.FindProperty("displayName").stringValue = "앙코르";
                serialized.FindProperty("description").stringValue =
                    "사용하면 현재 공연 시간을 연장한다.";
                serialized.FindProperty("role").enumValueIndex =
                    (int)CardRole.Utility;
                serialized.FindProperty("utilityEffect").enumValueIndex =
                    (int)UtilityCardEffect.ExtendPerformanceTime;
                serialized.FindProperty("drawCount").intValue = 0;
                serialized.FindProperty("performanceTimeBonusSeconds").floatValue = 0f;
                serialized.FindProperty("artwork").objectReferenceValue =
                    placeholderArtwork;
                serialized.FindProperty("audienceReaction")
                    .FindPropertyRelative("appliesToAudience")
                    .boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                    instance,
                    EncoreBasePrefabPath);
                return saved == null ? null : saved.GetComponent<CardDefinition>();
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        static CardDefinition CreateEncoreTierIfMissing(
            GameObject encoreBasePrefab,
            string fileName,
            string cardId,
            string displayName,
            float durationBonus,
            Color color)
        {
            string path = $"{CardPrefabFolder}/{fileName}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<CardDefinition>();

            GameObject instance =
                PrefabUtility.InstantiatePrefab(encoreBasePrefab) as GameObject;
            if (instance == null) return null;

            try
            {
                instance.name = fileName;
                CardDefinition card = instance.GetComponent<CardDefinition>();
                var serialized = new SerializedObject(card);
                serialized.FindProperty("id").stringValue = cardId;
                serialized.FindProperty("displayName").stringValue = displayName;
                serialized.FindProperty("description").stringValue =
                    $"사용하면 현재 공연 시간이 {durationBonus:0}초 연장된다.";
                serialized.FindProperty("performanceTimeBonusSeconds").floatValue =
                    durationBonus;
                serialized.FindProperty("cardColor").colorValue = color;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
                return saved == null ? null : saved.GetComponent<CardDefinition>();
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        static CardDefinition[] CreateStageControlCards()
        {
            GameObject basePrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(CardBasePrefabPath);
            if (basePrefab == null)
            {
                Debug.LogError(
                    $"[AugmentFirstSetSetup] 카드 베이스 프리팹이 없습니다: " +
                    CardBasePrefabPath);
                return new CardDefinition[3];
            }

            CardDefinition placeholderCard = LoadCardDefinition("Card_05_GuitarSolo");
            Sprite placeholderArtwork =
                placeholderCard == null ? null : placeholderCard.Artwork;
            CardDefinition stageControlBase = CreateStageControlBaseIfMissing(
                basePrefab,
                placeholderArtwork);
            if (stageControlBase == null) return new CardDefinition[3];

            return new[]
            {
                CreateStageControlTierIfMissing(
                    stageControlBase.gameObject,
                    "Card_StageControl_Bronze",
                    "stage_control_bronze",
                    "무대 장악 · 브론즈",
                    10,
                    new Color32(0xCD, 0x7F, 0x32, 0xFF)),
                CreateStageControlTierIfMissing(
                    stageControlBase.gameObject,
                    "Card_StageControl_Silver",
                    "stage_control_silver",
                    "무대 장악 · 실버",
                    20,
                    new Color32(0xC0, 0xC0, 0xC0, 0xFF)),
                CreateStageControlTierIfMissing(
                    stageControlBase.gameObject,
                    "Card_StageControl_Gold",
                    "stage_control_gold",
                    "무대 장악 · 골드",
                    30,
                    new Color32(0xFF, 0xD7, 0x00, 0xFF))
            };
        }

        static CardDefinition CreateStageControlBaseIfMissing(
            GameObject cardBasePrefab,
            Sprite placeholderArtwork)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(
                StageControlBasePrefabPath);
            if (existing != null) return existing.GetComponent<CardDefinition>();

            GameObject instance =
                PrefabUtility.InstantiatePrefab(cardBasePrefab) as GameObject;
            if (instance == null) return null;

            try
            {
                instance.name = "Card_StageControlBase";
                CardDefinition card = instance.GetComponent<CardDefinition>();
                var serialized = new SerializedObject(card);
                serialized.FindProperty("id").stringValue = "stage_control_base";
                serialized.FindProperty("displayName").stringValue = "무대 장악";
                serialized.FindProperty("description").stringValue =
                    "모든 관객에게 동일한 고정 호응을 적용한다.";
                serialized.FindProperty("role").enumValueIndex = (int)CardRole.Normal;
                serialized.FindProperty("targetStage").enumValueIndex =
                    (int)HeatStage.Mosh;
                serialized.FindProperty("targetPreference").enumValueIndex =
                    (int)CrowdPreference.Mosh;
                serialized.FindProperty("utilityEffect").enumValueIndex =
                    (int)UtilityCardEffect.None;
                serialized.FindProperty("drawCount").intValue = 0;
                serialized.FindProperty("performanceTimeBonusSeconds").floatValue = 0f;
                serialized.FindProperty("artwork").objectReferenceValue =
                    placeholderArtwork;

                SerializedProperty reaction =
                    serialized.FindProperty("audienceReaction");
                reaction.FindPropertyRelative("appliesToAudience").boolValue = true;
                reaction.FindPropertyRelative("reactionMode").enumValueIndex =
                    (int)AudienceReactionMode.FixedAllAudience;
                reaction.FindPropertyRelative("fixedReactionValue").intValue = 0;
                reaction.FindPropertyRelative("chillScore").intValue = 0;
                reaction.FindPropertyRelative("singalongScore").intValue = 0;
                reaction.FindPropertyRelative("moshScore").intValue = 0;
                reaction.FindPropertyRelative("calmScore").intValue = 0;
                reaction.FindPropertyRelative("middleScore").intValue = 0;
                reaction.FindPropertyRelative("excitedScore").intValue = 0;
                reaction.FindPropertyRelative("engagementMultiplier").floatValue = 1f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                    instance,
                    StageControlBasePrefabPath);
                return saved == null ? null : saved.GetComponent<CardDefinition>();
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        static CardDefinition CreateStageControlTierIfMissing(
            GameObject stageControlBasePrefab,
            string fileName,
            string cardId,
            string displayName,
            int fixedReactionValue,
            Color color)
        {
            string path = $"{CardPrefabFolder}/{fileName}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<CardDefinition>();

            GameObject instance =
                PrefabUtility.InstantiatePrefab(stageControlBasePrefab) as GameObject;
            if (instance == null) return null;

            try
            {
                instance.name = fileName;
                CardDefinition card = instance.GetComponent<CardDefinition>();
                var serialized = new SerializedObject(card);
                serialized.FindProperty("id").stringValue = cardId;
                serialized.FindProperty("displayName").stringValue = displayName;
                serialized.FindProperty("description").stringValue =
                    $"모든 관객에게 고정 +{fixedReactionValue} 호응을 적용한다.";
                serialized.FindProperty("audienceReaction")
                    .FindPropertyRelative("fixedReactionValue")
                    .intValue = fixedReactionValue;
                serialized.FindProperty("cardColor").colorValue = color;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
                return saved == null ? null : saved.GetComponent<CardDefinition>();
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        static void ConfigureGrantedCardAugment(
            AugmentDefinition definition,
            IReadOnlyList<CardDefinition> cards,
            string cardSetName)
        {
            if (definition == null || cards == null || cards.Count != 3)
            {
                Debug.LogError(
                    $"[AugmentFirstSetSetup] {cardSetName} 증강을 카드 지급형으로 " +
                    "구성할 수 없습니다.");
                return;
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("effectType").enumValueIndex =
                (int)AugmentEffectType.GrantCard;
            SerializedProperty tiers = serialized.FindProperty("tiers");
            tiers.arraySize = 3;
            for (int i = 0; i < tiers.arraySize; i++)
            {
                if (cards[i] == null)
                {
                    Debug.LogError(
                        $"[AugmentFirstSetSetup] {cardSetName} {((AugmentTier)i)} " +
                        "카드가 없습니다.");
                    continue;
                }

                SerializedProperty tier = tiers.GetArrayElementAtIndex(i);
                tier.FindPropertyRelative("tier").enumValueIndex = i;
                tier.FindPropertyRelative("enabled").boolValue = true;
                tier.FindPropertyRelative("value").floatValue = 0f;
                tier.FindPropertyRelative("description").stringValue =
                    cards[i].Description;
                tier.FindPropertyRelative("grantedCard").objectReferenceValue =
                    cards[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            PrefabUtility.SavePrefabAsset(definition.gameObject);
        }

        static void ConfigureOwnershipPolicy(
            AugmentDefinition definition,
            AugmentTierOwnershipPolicy policy)
        {
            if (definition == null) return;

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("tierOwnershipPolicy").enumValueIndex =
                (int)policy;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            PrefabUtility.SavePrefabAsset(definition.gameObject);
        }

        static void ConfigureGoldOnlyAugment(
            AugmentDefinition definition,
            float goldValue,
            string goldDescription)
        {
            if (definition == null) return;

            var serialized = new SerializedObject(definition);
            SerializedProperty tiers = serialized.FindProperty("tiers");
            tiers.arraySize = 3;
            for (int i = 0; i < tiers.arraySize; i++)
            {
                bool isGold = i == (int)AugmentTier.Gold;
                SerializedProperty tier = tiers.GetArrayElementAtIndex(i);
                tier.FindPropertyRelative("tier").enumValueIndex = i;
                tier.FindPropertyRelative("enabled").boolValue = isGold;
                tier.FindPropertyRelative("value").floatValue =
                    isGold ? goldValue : 0f;
                tier.FindPropertyRelative("description").stringValue =
                    isGold ? goldDescription : string.Empty;
                tier.FindPropertyRelative("grantedCard").objectReferenceValue = null;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            PrefabUtility.SavePrefabAsset(definition.gameObject);
        }

        static void CreateOrUpdateCardCatalog(
            IReadOnlyList<CardDefinition> addedCards)
        {
            CardCatalog catalog =
                AssetDatabase.LoadAssetAtPath<CardCatalog>(CardCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CardCatalog>();
                AssetDatabase.CreateAsset(catalog, CardCatalogPath);
            }

            var definitions = new List<CardDefinition>();
            var ids = new HashSet<string>(System.StringComparer.Ordinal);
            CardDeckConfig deck =
                AssetDatabase.LoadAssetAtPath<CardDeckConfig>(CardDeckConfigPath);
            if (deck != null)
            {
                for (int i = 0; i < deck.CardPool.Count; i++)
                {
                    CardPoolEntry entry = deck.CardPool[i];
                    AddCardIfUnique(
                        definitions,
                        ids,
                        entry == null ? null : entry.Prefab);
                }
            }

            IReadOnlyList<CardDefinition> existing = catalog.Definitions;
            for (int i = 0; i < existing.Count; i++)
                AddCardIfUnique(definitions, ids, existing[i]);
            for (int i = 0; i < addedCards.Count; i++)
                AddCardIfUnique(definitions, ids, addedCards[i]);

            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("definitions");
            entries.arraySize = definitions.Count;
            for (int i = 0; i < definitions.Count; i++)
                entries.GetArrayElementAtIndex(i).objectReferenceValue =
                    definitions[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        static void AddCardIfUnique(
            List<CardDefinition> definitions,
            HashSet<string> ids,
            CardDefinition card)
        {
            if (card == null ||
                string.IsNullOrWhiteSpace(card.Id) ||
                !ids.Add(card.Id))
                return;

            definitions.Add(card);
        }

        static CardDefinition LoadCardDefinition(string fileName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{CardPrefabFolder}/{fileName}.prefab");
            return prefab == null ? null : prefab.GetComponent<CardDefinition>();
        }

        static AugmentDefinition CreateDefinitionIfMissing(
            string assetName,
            string augmentId,
            string displayName,
            AugmentEffectType effectType,
            IReadOnlyList<float> values,
            System.Func<float, string> createDescription)
        {
            string path = $"{PrefabFolder}/{assetName}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                AugmentDefinition existingDefinition =
                    existing.GetComponent<AugmentDefinition>();
                ConfigureDefinition(
                    existingDefinition,
                    augmentId,
                    displayName,
                    effectType,
                    values,
                    createDescription);
                PrefabUtility.SavePrefabAsset(existing);
                return existingDefinition;
            }

            var root = new GameObject(assetName);
            try
            {
                AugmentDefinition definition = root.AddComponent<AugmentDefinition>();
                ConfigureDefinition(
                    definition,
                    augmentId,
                    displayName,
                    effectType,
                    values,
                    createDescription);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab == null ? null : prefab.GetComponent<AugmentDefinition>();
        }

        static void ConfigureDefinition(
            AugmentDefinition definition,
            string augmentId,
            string displayName,
            AugmentEffectType effectType,
            IReadOnlyList<float> values,
            System.Func<float, string> createDescription)
        {
            if (definition == null || values == null || values.Count != 3)
            {
                Debug.LogError(
                    $"[AugmentFirstSetSetup] 증강 기본 데이터를 구성할 수 없습니다: " +
                    augmentId);
                return;
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("augmentId").stringValue = augmentId;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("effectType").enumValueIndex = (int)effectType;
            serialized.FindProperty("tierOwnershipPolicy").enumValueIndex =
                (int)AugmentTierOwnershipPolicy.IndependentTiers;

            SerializedProperty tiers = serialized.FindProperty("tiers");
            tiers.arraySize = 3;
            for (int i = 0; i < tiers.arraySize; i++)
            {
                SerializedProperty tier = tiers.GetArrayElementAtIndex(i);
                tier.FindPropertyRelative("tier").enumValueIndex = i;
                tier.FindPropertyRelative("enabled").boolValue = true;
                tier.FindPropertyRelative("value").floatValue = values[i];
                tier.FindPropertyRelative("description").stringValue =
                    createDescription(values[i]);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        static void CreateOrUpdateCatalog(IReadOnlyList<AugmentDefinition> definitions)
        {
            AugmentCatalog catalog = AssetDatabase.LoadAssetAtPath<AugmentCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AugmentCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("definitions");
            entries.arraySize = definitions.Count;
            for (int i = 0; i < definitions.Count; i++)
                entries.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        static void CreateRewardTableIfMissing(
            string tableId,
            float bronzeWeight,
            float silverWeight,
            float goldWeight,
            int rerollsPerSlot)
        {
            string path = $"{RewardTableFolder}/{tableId}.asset";
            if (AssetDatabase.LoadAssetAtPath<AugmentRewardTable>(path) != null)
                return;

            AugmentRewardTable table = ScriptableObject.CreateInstance<AugmentRewardTable>();
            var serialized = new SerializedObject(table);
            serialized.FindProperty("tableId").stringValue = tableId;
            serialized.FindProperty("bronzeWeight").floatValue = bronzeWeight;
            serialized.FindProperty("silverWeight").floatValue = silverWeight;
            serialized.FindProperty("goldWeight").floatValue = goldWeight;
            serialized.FindProperty("rerollsPerSlot").intValue = rerollsPerSlot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(table, path);
        }

        static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
