using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 투어 맵 선택 화면 셋업.
    ///
    ///   Tools/Tour/Setup Tour Map
    ///     1. Sprites/0823_art 의 맵·대화 아트 임포트 교정 (Single · Point · Mipmap Off)
    ///     2. Resources/Tour/TourMapConfig.asset 생성 + 스프라이트·랭크 글자 연결 (비어 있는 칸만)
    ///   씬 배치는 없다 — TourPrototypeUI 가 런타임에 이 설정으로 화면을 만든다.
    /// </summary>
    public static class TourMapSetup
    {
        const string ArtFolder = "Assets/Sprites/0823_art";
        const string RankFolder = "Assets/Sprites/UI/Rank";
        const string ResourceFolder = "Assets/Resources/Tour";
        const string ConfigPath = ResourceFolder + "/TourMapConfig.asset";

        /// <summary>이 화면(맵·대화)이 쓰는 아트만 교정한다. 2_* 는 증강 담당 소유라 건드리지 않는다.</summary>
        static readonly string[] OwnedArt =
        {
            "1_dialogue", "1_f_key", "1_print_done",
            "3_boss", "3_bus", "3_map_dot", "3_map_pin_hover", "3_map_pin_normal", "3_mic", "3_raccoon",
            "bg_map_select",
        };

        [MenuItem("Tools/Tour/Setup Tour Map", false, 20)]
        public static void Setup()
        {
            EnsureFolder(ResourceFolder);
            FixArtImport();

            var config = AssetDatabase.LoadAssetAtPath<TourMapConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<TourMapConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            var ranks = new List<TourMapConfig.RankSprite>();
            foreach (string label in new[] { "S", "A", "B", "C", "D", "F" })
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{RankFolder}/{label}.png");
                if (sprite != null) ranks.Add(new TourMapConfig.RankSprite { label = label, sprite = sprite });
            }

            config.EditorFillArt(
                LoadArt("bg_map_select"),
                LoadArt("3_map_pin_normal"),
                LoadArt("3_map_pin_hover"),
                LoadArt("3_mic"),
                LoadArt("3_boss"),
                LoadArt("3_map_dot"),
                LoadArt("3_bus"),
                LoadArt("3_raccoon"),
                ranks);
            EditorUtility.SetDirty(config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[TourMap] 셋업 완료: {ConfigPath} (아트 연결 {(config.HasArt ? "OK" : "일부 누락")}). " +
                "노드 위치·연출 수치는 이 에셋에서 조절한다.",
                config);
        }

        public static Sprite LoadArt(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{name}.png");

        [MenuItem("Tools/Tour/Fix Tour Art Import", false, 21)]
        public static void FixArtImport()
        {
            int fixedCount = 0;
            for (int i = 0; i < OwnedArt.Length; i++)
            {
                string path = $"{ArtFolder}/{OwnedArt[i]}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    changed = true;
                }
                if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; changed = true; }
                if (!changed) continue;

                importer.SaveAndReimport();
                fixedCount++;
            }

            if (fixedCount > 0)
                Debug.Log($"[TourMap] 아트 임포트 교정: {fixedCount}장 (Single · Point · Mipmap Off · 무압축)");
        }
    }
}
