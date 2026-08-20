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

        [MenuItem("Tools/Tour/Setup First Augments", false, 20)]
        public static void Setup()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(ResourceFolder);
            EnsureFolder(RewardTableFolder);
            EnsureFolder(CardResourceFolder);

            CardDefinition[] encoreCards = CreateEncoreCards();

            AugmentDefinition fever = CreateDefinitionIfMissing(
                "Augment_FeverDuration",
                "fever_duration",
                "FEVER EXTENSION",
                AugmentEffectType.FeverDurationSeconds,
                new[] { 1f, 2f, 3f },
                value => $"Fever lasts {value:0} second{(Mathf.Approximately(value, 1f) ? string.Empty : "s")} longer.");

            AugmentDefinition performanceTime = CreateDefinitionIfMissing(
                "Augment_PerformanceDuration",
                "performance_duration",
                "ENCORE TIME",
                AugmentEffectType.GrantCard,
                new[] { 0f, 0f, 0f },
                _ => "Adds an Encore card to every generated deck batch.");

            ConfigureGrantedCardAugment(performanceTime, encoreCards);

            AugmentDefinition initialAudience = CreateDefinitionIfMissing(
                "Augment_InitialAudience",
                "initial_audience",
                "EARLY CROWD",
                AugmentEffectType.InitialAudienceCount,
                new[] { 1f, 2f, 3f },
                value => $"Start each performance with {value:0} additional audience member{(Mathf.Approximately(value, 1f) ? string.Empty : "s")}.");

            AugmentDefinition audiencePromotion = CreateDefinitionIfMissing(
                "Augment_AudiencePromotion",
                "audience_promotion",
                "SHOW PROMOTION",
                AugmentEffectType.AudienceArrivalIntervalReductionSeconds,
                new[] { 0.5f, 1f, 1.5f },
                value => $"Natural audience arrival checks happen {value:0.#} seconds sooner.");

            AugmentDefinition bandmateCover = CreateDefinitionIfMissing(
                "Augment_BandmateCover",
                "bandmate_cover",
                "BANDMATE COVER",
                AugmentEffectType.ComboBreakPreventionCount,
                new[] { 1f, 2f, 3f },
                value => $"Prevent combo loss {value:0} time{(Mathf.Approximately(value, 1f) ? string.Empty : "s")} per performance.");

            CreateOrUpdateCatalog(new[]
            {
                fever,
                performanceTime,
                initialAudience,
                audiencePromotion,
                bandmateCover
            });
            CreateOrUpdateCardCatalog(encoreCards);

            // 티어 확률은 아직 미확정이므로 첫 검증 버전에서는 모두 같은 가중치로 둔다.
            CreateRewardTableIfMissing("reward_basic", 1f, 1f, 1f, 1);
            CreateRewardTableIfMissing("reward_advanced", 1f, 1f, 1f, 1);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AugmentFirstSetSetup] 증강 5종과 앙코르 카드 3종, 런 덱 카탈로그 구성을 완료했습니다.");
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

        static void ConfigureGrantedCardAugment(
            AugmentDefinition definition,
            IReadOnlyList<CardDefinition> cards)
        {
            if (definition == null || cards == null || cards.Count != 3)
            {
                Debug.LogError(
                    "[AugmentFirstSetSetup] 공연 시간 증강을 카드 지급형으로 바꿀 수 없습니다.");
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
                        $"[AugmentFirstSetSetup] 앙코르 {((AugmentTier)i)} 카드가 없습니다.");
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
                return existing.GetComponent<AugmentDefinition>();

            var root = new GameObject(assetName);
            try
            {
                AugmentDefinition definition = root.AddComponent<AugmentDefinition>();
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("augmentId").stringValue = augmentId;
                serialized.FindProperty("displayName").stringValue = displayName;
                serialized.FindProperty("effectType").enumValueIndex = (int)effectType;

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
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab == null ? null : prefab.GetComponent<AugmentDefinition>();
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
