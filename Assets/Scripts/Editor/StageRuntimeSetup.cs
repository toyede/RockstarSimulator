using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 스테이지 런타임 셋업. (기획서 §10·§17 P0-1/P0-2, 에셋 명세 §13)
    ///
    ///   Tools/Tour/Setup Stage Runtime 한 번이면:
    ///     1. Sprites/Stages 임포트 교정 (Single · Point · Mipmap Off · PPU 100)
    ///     2. Settings/Tour/AudiencePresets/*.asset  — 스테이지별 관객 프리셋 5개
    ///     3. Settings/Tour/RuleConfigs/*.asset      — Stage 2 특별 관객 / Stage 3·4 위기 설정
    ///     4. Resources/Stages/StageContentCatalog.asset, StageVisualCatalog.asset
    ///     5. Stage01~05Boss.asset 의 audiencePresetId / venueRuleIds 입력
    ///     6. Main 씬에 [StageRuntime] (디렉터 + 룰 5개) 과 Background 의 StageBackgroundView 배치
    ///   까지 끝난다. 이미 있는 에셋·오브젝트는 값만 맞추고 새로 만들지 않는다. 실행 후 Ctrl+S.
    /// </summary>
    public static class StageRuntimeSetup
    {
        const string TourFolder = "Assets/Settings/Tour";
        const string PresetFolder = TourFolder + "/AudiencePresets";
        const string RuleConfigFolder = TourFolder + "/RuleConfigs";
        const string ResourceFolder = "Assets/Resources/Stages";
        const string ContentCatalogPath = ResourceFolder + "/StageContentCatalog.asset";
        const string VisualCatalogPath = ResourceFolder + "/StageVisualCatalog.asset";
        const string StageSpriteFolder = "Assets/Sprites/Stages";
        const string MainScenePath = "Assets/Scenes/Main.unity";
        const string RuntimeRootName = "[StageRuntime]";

        const string RuleBusking = "busking_walk_in";
        const string RuleSpecial = "special_audience_requests";
        const string RuleFestival = "festival_nearby_concert";
        const string RuleArena = "arena_adaptive_event";
        const string RuleRival = "rival_crowd_challenge";
        const string RuleBoss = "boss_battle";
        const string RuleRain = "festival_rain_shower";
        const string RuleFlag = "festival_flag_wave";
        const string RuleScreen = "arena_big_screen";
        const string RuleVip = "arena_vip_entrance";
        const string RuleBlackout = "arena_blackout";
        const string RuleContested = "stadium_contested_fan";

        struct StageSpec
        {
            public string assetName;
            public string stageId;
            public string presetId;
            public string[] ruleIds;
            public string spriteFolder;
            public string spritePrefix;
            public string ruleTitle;
            public string ruleBody;
        }

        static readonly StageSpec[] Stages =
        {
            new StageSpec
            {
                assetName = "Stage01", stageId = "stage_01", presetId = "audience_alley_balanced",
                ruleIds = new[] { RuleBusking },
                spriteFolder = "ST01_Alley", spritePrefix = "ST01",
                ruleTitle = "골목 버스킹",
                ruleBody = "지나가던 관객은 관심이 식으면 바로 떠납니다.\n복장과 상태를 살펴 맞는 카드를 사용하세요.",
            },
            new StageSpec
            {
                assetName = "Stage02", stageId = "stage_02", presetId = "audience_basement_regulars",
                ruleIds = new[] { RuleSpecial },
                spriteFolder = "ST02_Basement", spritePrefix = "ST02",
                ruleTitle = "단골의 요청",
                ruleBody = "특별 관객은 원하는 공연을 직접 요구합니다.\n같은 성향의 Special 카드를 관객에게 드롭하세요.",
            },
            new StageSpec
            {
                assetName = "Stage03", stageId = "stage_03", presetId = "audience_festival_mixed",
                ruleIds = new[] { RuleSpecial, RuleFestival, RuleRain, RuleFlag },
                spriteFolder = "ST03_Festival", spritePrefix = "ST03",
                ruleTitle = "축제의 변수",
                ruleBody = "옆 무대의 유혹, 소나기, 깃발 웨이브가 찾아옵니다.\n경고를 읽고 CHILL 카드로 우산을, 콤보로 웨이브를 만드세요.",
            },
            new StageSpec
            {
                assetName = "Stage04", stageId = "stage_04", presetId = "audience_arena_mainstream",
                ruleIds = new[] { RuleSpecial, RuleArena, RuleScreen, RuleBlackout },
                spriteFolder = "ST04_Arena", spritePrefix = "ST04",
                ruleTitle = "생방송 사고",
                ruleBody = "카메라 큐시트 순서대로 카드를 내고, 정전 중에는 관객을 기억해 Miss 없이 버티세요.\n정전 보너스 없이는 목표 점수에 닿기 어렵습니다.",
            },
            new StageSpec
            {
                assetName = "Stage05Boss", stageId = "stage_05_boss", presetId = "audience_stadium_final",
                ruleIds = new[] { RuleSpecial, RuleBoss, RuleContested },
                spriteFolder = "ST05_BossStadium", spritePrefix = "ST05",
                ruleTitle = "라이벌 배틀",
                ruleBody = "LUX//FAUNA가 특정 팬층을 빼앗으려 합니다.\n예고된 관객의 호응도를 60 이상으로 만들거나 대응 Special 카드를 성공시키세요.",
            },
        };

        [MenuItem("Tools/Tour/Setup Stage Runtime", false, 10)]
        public static void Setup()
        {
            EnsureFolder(TourFolder);
            EnsureFolder(PresetFolder);
            EnsureFolder(RuleConfigFolder);
            EnsureFolder(ResourceFolder);

            FixStageSpriteImport();

            List<AudienceStagePreset> presets = CreatePresets();
            StageContentCatalog contentCatalog = GetOrCreateContentCatalog(presets);
            StageVisualCatalog visualCatalog = GetOrCreateVisualCatalog();
            Dictionary<string, StageDefinition> stages = ConfigureStageDefinitions();

            SpecialAudienceConfig specialStage2 = GetOrCreateSpecialConfig(
                "SpecialAudience_Stage02", firstSpawnDelay: 18f, spawnInterval: 24f, requestDuration: 8f);
            AudienceCrisisConfig crisisFestival = GetOrCreateFestivalCrisisConfig();
            AudienceCrisisConfig crisisArena = GetOrCreateArenaCrisisConfig();

            var baseEngagement = AssetDatabase.LoadAssetAtPath<AudienceEngagementConfig>(
                "Assets/Settings/Audience/AudienceEngagementConfig.asset");
            if (!contentCatalog.TryValidate(baseEngagement, out string contentError))
                Debug.LogError($"[StageRuntime] 관객 프리셋 검증 실패: {contentError}", contentCatalog);

            AssetDatabase.SaveAssets();

            ConfigureMainScene(stages, specialStage2, crisisFestival, crisisArena);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[StageRuntime] 셋업 완료. 프리셋 5 · 룰 설정 3 · 카탈로그 2 · Main 씬 [StageRuntime] 배치. " +
                "씬을 Ctrl+S 로 저장할 것!",
                visualCatalog);
        }

        // ---------------- 스프라이트 임포트 ----------------

        [MenuItem("Tools/Tour/Fix Stage Sprite Import", false, 11)]
        public static void FixStageSpriteImport()
        {
            if (!AssetDatabase.IsValidFolder(StageSpriteFolder)) return;

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { StageSpriteFolder });
            int fixedCount = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
                if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f)) { importer.spritePixelsPerUnit = 100f; changed = true; }
                if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; changed = true; }
                if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }

                if (!changed) continue;
                importer.SaveAndReimport();
                fixedCount++;
            }

            if (fixedCount > 0)
                Debug.Log($"[StageRuntime] 스테이지 스프라이트 임포트 교정: {fixedCount}장 (Single · Point · Mipmap Off · PPU 100)");
        }

        // ---------------- 관객 프리셋 ----------------

        static List<AudienceStagePreset> CreatePresets()
        {
            // 기획서 §5~§9 관객 프리셋 초기값
            return new List<AudienceStagePreset>
            {
                CreatePreset("audience_alley_balanced", 3, 8, 1f, 1f, 1f, 45f, 60f, 0.7f, 6f, 0.6f),
                CreatePreset("audience_basement_regulars", 6, 10, 1f, 1f, 1f, 42f, 58f, 0.9f, 6f, 0.4f),
                CreatePreset("audience_festival_mixed", 8, 12, 1f, 1f, 1f, 40f, 55f, 1.0f, 5f, 0.3f),
                CreatePreset("audience_arena_mainstream", 9, 12, 1f, 1.2f, 1f, 38f, 52f, 1.2f, 5f, 0.25f),
                CreatePreset("audience_stadium_final", 12, 12, 1f, 1f, 1f, 45f, 60f, 1.25f, 0f, 0f),
            };
        }

        static AudienceStagePreset CreatePreset(
            string id, int initial, int maximum,
            float chill, float singalong, float mosh,
            float engagementMin, float engagementMax, float decay,
            float interval, float chance)
        {
            string path = $"{PresetFolder}/{id}.asset";
            var preset = AssetDatabase.LoadAssetAtPath<AudienceStagePreset>(path);
            if (preset != null) return preset; // 손으로 조정한 값을 지키기 위해 기존 프리셋은 덮어쓰지 않는다

            preset = ScriptableObject.CreateInstance<AudienceStagePreset>();
            preset.EditorInitialize(id, initial, maximum, chill, singalong, mosh,
                engagementMin, engagementMax, decay, interval, chance);
            AssetDatabase.CreateAsset(preset, path);
            return preset;
        }

        static StageContentCatalog GetOrCreateContentCatalog(List<AudienceStagePreset> presets)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageContentCatalog>(ContentCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<StageContentCatalog>();
                AssetDatabase.CreateAsset(catalog, ContentCatalogPath);
            }

            var merged = new List<AudienceStagePreset>(catalog.AudiencePresets);
            for (int i = 0; i < presets.Count; i++)
                if (!merged.Contains(presets[i])) merged.Add(presets[i]);
            merged.RemoveAll(preset => preset == null);

            catalog.EditorSetPresets(merged);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        // ---------------- 비주얼 카탈로그 ----------------

        static StageVisualCatalog GetOrCreateVisualCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageVisualCatalog>(VisualCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<StageVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, VisualCatalogPath);
            }

            for (int i = 0; i < Stages.Length; i++)
            {
                StageSpec spec = Stages[i];
                StageVisualEntry entry = catalog.EditorGetOrAddEntry(spec.stageId);
                string folder = $"{StageSpriteFolder}/{spec.spriteFolder}";

                // 아트가 직접 넣은 스프라이트는 유지하고, 비어 있는 칸만 현재 납품물로 채운다
                if (entry.backgroundBase == null)
                    entry.backgroundBase = LoadSprite(folder, $"{spec.spritePrefix}_BG_Base_v2", $"{spec.spritePrefix}_BG_Base");
                if (entry.mapThumbnail == null)
                    entry.mapThumbnail = LoadSprite(folder, $"{spec.spritePrefix}_UI_MapThumb_v2", $"{spec.spritePrefix}_UI_MapThumb");
                if (entry.ruleIcon == null)
                    entry.ruleIcon = LoadSpriteByPrefix(folder, $"{spec.spritePrefix}_UI_Rule_");
                if (entry.foregroundLeft == null)
                    entry.foregroundLeft = LoadSpriteByPrefix(folder, $"{spec.spritePrefix}_FG_", "Left");
                if (entry.foregroundRight == null)
                    entry.foregroundRight = LoadSpriteByPrefix(folder, $"{spec.spritePrefix}_FG_", "Right");
                if (entry.lightOverlays == null) entry.lightOverlays = new List<Sprite>();
                if (entry.lightOverlays.Count == 0)
                {
                    foreach (Sprite overlay in LoadSpritesByPrefix(folder, $"{spec.spritePrefix}_BG_"))
                    {
                        if (overlay == null) continue;
                        if (overlay.name.Contains("_BG_Base")) continue;
                        entry.lightOverlays.Add(overlay);
                    }
                }
                if (string.IsNullOrEmpty(entry.ruleTitle)) entry.ruleTitle = spec.ruleTitle;
                if (string.IsNullOrEmpty(entry.ruleBody)) entry.ruleBody = spec.ruleBody;
            }

            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static Sprite LoadSprite(string folder, params string[] namesInPriority)
        {
            for (int i = 0; i < namesInPriority.Length; i++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{namesInPriority[i]}.png");
                if (sprite != null) return sprite;
            }
            return null;
        }

        static Sprite LoadSpriteByPrefix(string folder, string prefix, string contains = null)
        {
            foreach (Sprite sprite in LoadSpritesByPrefix(folder, prefix))
            {
                if (sprite == null) continue;
                if (contains != null && !sprite.name.Contains(contains)) continue;
                return sprite;
            }
            return null;
        }

        static IEnumerable<Sprite> LoadSpritesByPrefix(string folder, string prefix)
        {
            if (!AssetDatabase.IsValidFolder(folder)) yield break;

            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            var paths = new List<string>(guids.Length);
            for (int i = 0; i < guids.Length; i++) paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));
            paths.Sort(string.CompareOrdinal);

            for (int i = 0; i < paths.Count; i++)
            {
                string fileName = Path.GetFileNameWithoutExtension(paths[i]);
                if (!fileName.StartsWith(prefix, System.StringComparison.Ordinal)) continue;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
                if (sprite != null) yield return sprite;
            }
        }

        // ---------------- StageDefinition ----------------

        static Dictionary<string, StageDefinition> ConfigureStageDefinitions()
        {
            var result = new Dictionary<string, StageDefinition>();
            for (int i = 0; i < Stages.Length; i++)
            {
                StageSpec spec = Stages[i];
                string path = $"{TourFolder}/{spec.assetName}.asset";
                var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(path);
                if (stage == null)
                {
                    Debug.LogWarning($"[StageRuntime] {path} 가 없습니다. Tools/Tour/Setup Prototype Loop 를 먼저 실행하세요.");
                    continue;
                }

                var serialized = new SerializedObject(stage);
                serialized.FindProperty("audiencePresetId").stringValue = spec.presetId;
                SerializedProperty rules = serialized.FindProperty("venueRuleIds");
                rules.arraySize = spec.ruleIds.Length;
                for (int r = 0; r < spec.ruleIds.Length; r++)
                    rules.GetArrayElementAtIndex(r).stringValue = spec.ruleIds[r];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(stage);

                result[spec.stageId] = stage;
            }
            return result;
        }

        // ---------------- 룰 설정 에셋 ----------------

        static SpecialAudienceConfig GetOrCreateSpecialConfig(
            string assetName, float firstSpawnDelay, float spawnInterval, float requestDuration)
        {
            string path = $"{RuleConfigFolder}/{assetName}.asset";
            var config = AssetDatabase.LoadAssetAtPath<SpecialAudienceConfig>(path);
            if (config != null) return config;

            config = ScriptableObject.CreateInstance<SpecialAudienceConfig>();
            AssetDatabase.CreateAsset(config, path);

            var serialized = new SerializedObject(config);
            serialized.FindProperty("firstSpawnDelay").floatValue = firstSpawnDelay;
            serialized.FindProperty("spawnInterval").floatValue = spawnInterval;
            serialized.FindProperty("requestDuration").floatValue = requestDuration;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        static AudienceCrisisConfig GetOrCreateFestivalCrisisConfig()
        {
            // 기획서 §7: 진행률 38~52% 에 정확히 한 번, 6초 경고, 33%/최대 4/최소 생존 3, 호응 50
            return GetOrCreateCrisisConfig("AudienceCrisis_Festival", serialized =>
            {
                serialized.FindProperty("useAdaptiveTrigger").boolValue = false;
                serialized.FindProperty("encounterChance").floatValue = 1f;
                serialized.FindProperty("earliestPerformanceRatio").floatValue = 0.38f;
                serialized.FindProperty("latestPerformanceRatio").floatValue = 0.52f;
                serialized.FindProperty("warningDuration").floatValue = 6f;
                serialized.FindProperty("minimumAudienceCount").intValue = 4;
                serialized.FindProperty("threatenedRatio").floatValue = 0.33f;
                serialized.FindProperty("maximumThreatenedCount").intValue = 4;
                serialized.FindProperty("minimumSurvivorCount").intValue = 3;
                serialized.FindProperty("retentionEngagement").floatValue = 50f;
                serialized.FindProperty("successfulSpecialSaveCount").intValue = 1;
            });
        }

        static AudienceCrisisConfig GetOrCreateArenaCrisisConfig()
        {
            // 기획서 §8: 20~85% 평가, 2.5초 유지, 앞서면(1.15·호응 70) 위기, 밀리면(0.75) 지원, 55% 에 확정(0.95)
            return GetOrCreateCrisisConfig("AudienceCrisis_Arena", serialized =>
            {
                serialized.FindProperty("useAdaptiveTrigger").boolValue = true;
                serialized.FindProperty("adaptiveEvaluationStartRatio").floatValue = 0.20f;
                serialized.FindProperty("adaptiveEvaluationEndRatio").floatValue = 0.85f;
                serialized.FindProperty("adaptiveSustainDuration").floatValue = 2.5f;
                serialized.FindProperty("adaptiveCheckInterval").floatValue = 0.5f;
                serialized.FindProperty("highPerformancePace").floatValue = 1.15f;
                serialized.FindProperty("highPerformanceEngagement").floatValue = 70f;
                serialized.FindProperty("lowPerformancePace").floatValue = 0.75f;
                serialized.FindProperty("adaptiveFallbackRatio").floatValue = 0.55f;
                serialized.FindProperty("fallbackCrisisPace").floatValue = 0.95f;
                serialized.FindProperty("comebackWarningDuration").floatValue = 1.75f;
                serialized.FindProperty("comebackAudienceCount").intValue = 2;
                serialized.FindProperty("comebackAudienceEngagement").floatValue = 55f;
                serialized.FindProperty("comebackEngagementBoost").floatValue = 8f;
                serialized.FindProperty("warningDuration").floatValue = 6f;
                serialized.FindProperty("minimumAudienceCount").intValue = 6;
                serialized.FindProperty("threatenedRatio").floatValue = 0.33f;
                serialized.FindProperty("maximumThreatenedCount").intValue = 4;
                serialized.FindProperty("minimumSurvivorCount").intValue = 3;
                serialized.FindProperty("retentionEngagement").floatValue = 50f;
                serialized.FindProperty("successfulSpecialSaveCount").intValue = 1;
            });
        }

        static AudienceCrisisConfig GetOrCreateCrisisConfig(
            string assetName, System.Action<SerializedObject> configure)
        {
            string path = $"{RuleConfigFolder}/{assetName}.asset";
            var config = AssetDatabase.LoadAssetAtPath<AudienceCrisisConfig>(path);
            if (config != null) return config;

            config = ScriptableObject.CreateInstance<AudienceCrisisConfig>();
            AssetDatabase.CreateAsset(config, path);

            var serialized = new SerializedObject(config);
            configure(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        // ---------------- Main 씬 ----------------

        static void ConfigureMainScene(
            Dictionary<string, StageDefinition> stages,
            SpecialAudienceConfig specialStage2,
            AudienceCrisisConfig crisisFestival,
            AudienceCrisisConfig crisisArena)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MainScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            }

            var roster = Object.FindFirstObjectByType<AudienceRosterSystem>(FindObjectsInactive.Include);
            var special = Object.FindFirstObjectByType<SpecialAudienceManager>(FindObjectsInactive.Include);
            var crisis = Object.FindFirstObjectByType<NearbyConcertCrisisDirector>(FindObjectsInactive.Include);
            var decorativeCrowd = Object.FindFirstObjectByType<DecorativeCrowd>(FindObjectsInactive.Include);
            if (roster == null) Debug.LogWarning("[StageRuntime] Main 씬에 AudienceRosterSystem 이 없습니다.");
            if (special == null) Debug.LogWarning("[StageRuntime] Main 씬에 SpecialAudienceManager 가 없습니다.");
            if (crisis == null) Debug.LogWarning("[StageRuntime] Main 씬에 NearbyConcertCrisisDirector 가 없습니다. (Tools/Audience/Setup Nearby Concert Crisis)");

            // 1) [StageRuntime] 루트 + 디렉터
            GameObject root = GameObject.Find(RuntimeRootName);
            if (root == null)
            {
                root = new GameObject(RuntimeRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create StageRuntime");
            }
            var director = EnsureComponent<StageRuntimeDirector>(root);

            // 2) 룰 컴포넌트 (자식 오브젝트 하나에 하나씩 — 인스펙터에서 구분하기 쉽게)
            var rules = new List<StageRuleBehaviour>();

            var busking = EnsureRule<BuskingWalkInRule>(root, "Rule_BuskingWalkIn", RuleBusking);
            rules.Add(busking);

            var specialRule = EnsureRule<SpecialAudienceRequestsRule>(root, "Rule_SpecialAudienceRequests", RuleSpecial);
            var overrides = new List<SpecialAudienceRequestsRule.StageOverride>();
            if (stages.TryGetValue("stage_02", out StageDefinition stage02) && specialStage2 != null)
                overrides.Add(new SpecialAudienceRequestsRule.StageOverride { stage = stage02, config = specialStage2 });
            specialRule.EditorConfigure(
                AssetDatabase.LoadAssetAtPath<SpecialAudienceConfig>("Assets/Settings/SpecialAudienceConfig.asset"),
                overrides);
            EditorUtility.SetDirty(specialRule);
            rules.Add(specialRule);

            var festival = EnsureRule<CrisisEventRule>(root, "Rule_FestivalNearbyConcert", RuleFestival);
            festival.EditorConfigure(crisisFestival);
            EditorUtility.SetDirty(festival);
            rules.Add(festival);

            var arena = EnsureRule<CrisisEventRule>(root, "Rule_ArenaAdaptiveEvent", RuleArena);
            arena.EditorConfigure(crisisArena);
            EditorUtility.SetDirty(arena);
            rules.Add(arena);

            var rival = EnsureRule<RivalCrowdChallengeRule>(root, "Rule_RivalCrowdChallenge", RuleRival);
            var notice = EnsureComponent<RivalAttackNoticeUI>(rival.gameObject);
            notice.EditorSetFont(ProjectFontTool.TmpFont);
            EditorUtility.SetDirty(notice);
            rules.Add(rival);

            // 기믹 이벤트 (Stage 3·4 에 집중, Stage 5 는 가볍게 하나). 수치는 각 Rule_* 인스펙터
            var rain = EnsureRule<RainShowerRule>(root, "Rule_FestivalRainShower", RuleRain);
            rain.EditorConfigure("소나기", new[] { 45f }, 12f);
            rules.Add(rain);
            var flag = EnsureRule<FlagWaveRule>(root, "Rule_FestivalFlagWave", RuleFlag);
            flag.EditorConfigure("깃발 웨이브", new float[0], 4f);
            rules.Add(flag);
            var screen = EnsureRule<BigScreenRule>(root, "Rule_ArenaBigScreen", RuleScreen);
            screen.EditorConfigure("큐시트", new[] { 40f, 80f }, 12f);
            rules.Add(screen);
            var blackout = EnsureRule<ArenaBlackoutRule>(root, "Rule_ArenaBlackout", RuleBlackout);
            blackout.EditorConfigure("정전", new[] { 58f, 100f }, 8f);
            EnsureComponent<BlackoutPresentation>(blackout.gameObject);
            rules.Add(blackout);
            // 게스트(VIP) 입장은 보류 — 컴포넌트는 남겨 두되 Stage 4 룰 목록에는 넣지 않는다
            var vip = EnsureRule<VipEntranceRule>(root, "Rule_ArenaVipEntrance", RuleVip);
            vip.EditorConfigure("VIP석 입장", new float[0], 15f);
            rules.Add(vip);
            var contested = EnsureRule<ContestedFanRule>(root, "Rule_StadiumContestedFan", RuleContested);
            contested.EditorConfigure("스탠딩석 팬 쟁탈", new[] { 35f, 85f }, 6f);
            rules.Add(contested);
            var eventNotice = EnsureComponent<StageEventNoticeUI>(root);
            eventNotice.EditorSetFont(ProjectFontTool.TmpFont);
            EditorUtility.SetDirty(eventNotice);

            // 보스 룰은 Tools/Tour/Setup Boss Battle 이 만든다. 있으면 디렉터 목록에 유지
            Transform bossChild = root.transform.Find("Rule_BossBattle");
            if (bossChild != null)
            {
                var boss = bossChild.GetComponent<BossBattleRule>();
                if (boss != null) rules.Add(boss);
            }

            director.EditorConfigure(roster, special, crisis, rules);
            EditorUtility.SetDirty(director);

            // 3) 배경 뷰 — Background/night_city_ground + stage_lights
            ConfigureBackgroundView(decorativeCrowd);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
        }

        static T EnsureRule<T>(GameObject root, string childName, string ruleId) where T : StageRuleBehaviour
        {
            Transform child = root.transform.Find(childName);
            GameObject go;
            if (child != null) go = child.gameObject;
            else
            {
                go = new GameObject(childName);
                Undo.RegisterCreatedObjectUndo(go, $"Create {childName}");
                go.transform.SetParent(root.transform, false);
            }

            T rule = EnsureComponent<T>(go);
            rule.EditorSetRuleId(ruleId);
            EditorUtility.SetDirty(rule);
            return rule;
        }

        static void ConfigureBackgroundView(DecorativeCrowd decorativeCrowd)
        {
            // "Background" 라는 이름은 UI 안에도 여럿 있으므로 배경 스프라이트(night_city_ground)부터 찾아 올라간다
            GameObject background = null;
            SpriteRenderer baseRenderer = null;
            SpriteRenderer lightRenderer = null;

            GameObject groundObject = GameObject.Find("night_city_ground");
            if (groundObject != null)
            {
                baseRenderer = groundObject.GetComponent<SpriteRenderer>();
                background = groundObject.transform.parent != null
                    ? groundObject.transform.parent.gameObject
                    : groundObject;
            }

            if (background != null)
            {
                Transform lights = background.transform.Find("stage_lights");
                if (lights == null)
                {
                    GameObject lightsObject = GameObject.Find("stage_lights");
                    if (lightsObject != null) lights = lightsObject.transform;
                }
                if (lights != null) lightRenderer = lights.GetComponent<SpriteRenderer>();
            }

            if (background == null || baseRenderer == null)
            {
                Debug.LogWarning("[StageRuntime] Background/night_city_ground 를 찾지 못해 StageBackgroundView 를 배치하지 않았습니다.");
                return;
            }

            var view = EnsureComponent<StageBackgroundView>(background);
            view.EditorConfigure(baseRenderer, lightRenderer, decorativeCrowd);
            EditorUtility.SetDirty(view);
        }
    }
}
