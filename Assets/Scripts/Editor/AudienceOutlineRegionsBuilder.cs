using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ContextStage.Editor
{
    public static class AudienceOutlineRegionsBuilder
    {
        // 현재 아트에서 몸과 분리된 가로 그림자가 확인된 애니메이션만 처리한다.
        const string ArtFolder = "Assets/Sprites/Crowd/Animated/Chill/Chill_Hype";
        const string AssetPath = "Assets/Resources/AudienceOutlineRegions.asset";

        [MenuItem("Tools/Audience/Rebuild Outline Regions")]
        public static void Rebuild()
        {
            var entries = new List<AudienceOutlineRegions.Entry>();
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder });
            System.Array.Sort(guids, System.StringComparer.Ordinal);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var texture = new Texture2D(2, 2);
                try
                {
                    if (!texture.LoadImage(File.ReadAllBytes(path))) continue;
                    Color32[] pixels = texture.GetPixels32();
                    foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if (!(asset is Sprite sprite)) continue;
                        Rect slice = sprite.rect;
                        var rect = new RectInt(Mathf.RoundToInt(slice.x), Mathf.RoundToInt(slice.y),
                            Mathf.RoundToInt(slice.width), Mathf.RoundToInt(slice.height));
                        var bands = new List<Vector2Int>();
                        int first = -1;
                        for (int y = 0; y < rect.height; y++)
                        {
                            bool occupied = false;
                            for (int x = 0; x < rect.width; x++)
                            {
                                // 취향 간파 셰이더의 0.5 알파 경계와 일치시킨다.
                                if (pixels[(rect.y + y) * texture.width + rect.x + x].a <= 127) continue;
                                occupied = true;
                                break;
                            }
                            if (occupied && first < 0) first = y;
                            if (first >= 0 && (!occupied || y == rect.height - 1))
                            {
                                bands.Add(new Vector2Int(first, occupied ? y + 1 : y));
                                first = -1;
                            }
                        }
                        if (bands.Count < 2) continue;
                        int body = 0;
                        for (int i = 1; i < bands.Count; i++)
                            if (bands[i].y - bands[i].x > bands[body].y - bands[body].x) body = i;
                        // 몸이 차지하는 연속 행을 기준으로 분리된 그림자와 상단 잔여 표시만 제외한다.
                        if (bands[body].y - bands[body].x < rect.height * 0.25f) continue;
                        entries.Add(new AudienceOutlineRegions.Entry
                        {
                            sprite = sprite,
                            minimumPixelY = body > 0 ? (bands[body - 1].y + bands[body].x) * 0.5f : 0f,
                            maximumPixelY = body < bands.Count - 1 ? (bands[body].y + bands[body + 1].x) * 0.5f : rect.height
                        });
                    }
                }
                finally { Object.DestroyImmediate(texture); }
            }
            var data = AssetDatabase.LoadAssetAtPath<AudienceOutlineRegions>(AssetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<AudienceOutlineRegions>();
                AssetDatabase.CreateAsset(data, AssetPath);
            }
            data.SetEntries(entries.ToArray());
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            Debug.Log($"[Audience] Outline regions rebuilt: {entries.Count} sprites.");
        }
    }
}
