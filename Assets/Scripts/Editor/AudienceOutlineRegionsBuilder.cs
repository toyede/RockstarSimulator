using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ContextStage.Editor
{
    public static class AudienceOutlineRegionsBuilder
    {
        const string AssetPath = "Assets/Resources/AudienceOutlineRegions.asset";
        const string PrefabPath = "Assets/Prefabs/Audience/AudienceMember.prefab";
        const int AtlasSize = 4096;
        const int Padding = 2;

        public sealed class BakedMask
        {
            public Sprite sprite;
            public RectInt sourceRect;
            public byte[] pixels;
            public int keptPixels;
            public int removedPixels;
            public RectInt atlasRect;
            public int page;
        }

        public static BakedMask Bake(Color32[] pixels, int width, int height,
            AudienceOutlineRegions.Correction correction)
        {
            if (pixels.Length != width * height) throw new ArgumentException("Pixel dimensions do not match.");
            var labels = new int[pixels.Length];
            var queue = new int[pixels.Length];
            bool Excluded(int x, int y)
            {
                if (correction.excludeRects == null) return false;
                foreach (RectInt rect in correction.excludeRects)
                    if (rect.Contains(new Vector2Int(x, y))) return true;
                return false;
            }
            for (int i = 0; i < labels.Length; i++)
                if (pixels[i].a < 128 || Excluded(i % width, i / width)) labels[i] = -1;
            int components = 0, largest = 0, largestCount = 0;
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != 0) continue;
                int head = 0, tail = 1;
                queue[0] = i; labels[i] = ++components;
                while (head < tail)
                {
                    int at = queue[head++], x = at % width, y = at / width;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        int next = ny * width + nx;
                        if (labels[next] != 0) continue;
                        labels[next] = components; queue[tail++] = next;
                    }
                }
                if (tail > largestCount) { largest = components; largestCount = tail; }
            }
            if (largest == 0) throw new InvalidOperationException("No opaque body found: " + correction.sprite);
            var selected = new bool[components + 1]; selected[largest] = true;
            if (correction.includeSeeds != null)
                foreach (Vector2Int seed in correction.includeSeeds)
                {
                    if (seed.x < 0 || seed.x >= width || seed.y < 0 || seed.y >= height || labels[seed.y * width + seed.x] <= 0)
                        throw new InvalidOperationException($"Invalid body seed {seed}: {correction.sprite}");
                    selected[labels[seed.y * width + seed.x]] = true;
                }
            var support = new byte[pixels.Length];
            int minX = width, minY = height, maxX = -1, maxY = -1, kept = 0, removed = 0;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int at = y * width + x, id = labels[at];
                bool keep = id > 0 && selected[id];
                if (id > 0) { if (keep) kept++; else removed++; }
                // 본체의 반투명 가장자리는 보존하되 다른 불투명 덩어리로 확장하지 않는다.
                if (id < 0 && pixels[at].a > 0 && !Excluded(x, y))
                    for (int dy = -2; dy <= 2 && !keep; dy++)
                    for (int dx = -2; dx <= 2 && !keep; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        int neighbor = labels[ny * width + nx];
                        keep = neighbor > 0 && selected[neighbor];
                    }
                if (!keep) continue;
                support[at] = 255;
                minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
            }
            var bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            var cropped = new byte[bounds.width * bounds.height];
            for (int y = 0; y < bounds.height; y++)
                Array.Copy(support, (bounds.y + y) * width + bounds.x, cropped, y * bounds.width, bounds.width);
            return new BakedMask { sprite = correction.sprite, sourceRect = bounds,
                pixels = cropped, keptPixels = kept, removedPixels = removed };
        }

        [MenuItem("Tools/Audience/Rebuild Outline Regions")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Rebuild masks in Edit Mode.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var serialized = new SerializedObject(prefab.GetComponent<AudienceMemberActor>());
            var property = serialized.GetIterator();
            var sprites = new HashSet<Sprite>();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is Sprite sprite)
                    sprites.Add(sprite);
            var data = AssetDatabase.LoadAssetAtPath<AudienceOutlineRegions>(AssetPath);
            var corrections = data.Corrections.Where(c => c.sprite != null).ToDictionary(c => c.sprite);
            var baked = new List<BakedMask>(sprites.Count);
            // 프리팹이 참조하는 프레임만 읽으며 원본 Importer는 변경하지 않는다.
            foreach (var sheet in sprites.GroupBy(AssetDatabase.GetAssetPath).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var texture = new Texture2D(2, 2);
                try
                {
                    if (!texture.LoadImage(File.ReadAllBytes(sheet.Key))) throw new IOException(sheet.Key);
                    var pixels = texture.GetPixels32();
                    foreach (Sprite sprite in sheet.OrderBy(s => s.name, StringComparer.Ordinal))
                    {
                        Rect rect = sprite.rect;
                        int x = Mathf.RoundToInt(rect.x), y = Mathf.RoundToInt(rect.y);
                        int width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
                        var slice = new Color32[width * height];
                        for (int row = 0; row < height; row++)
                            Array.Copy(pixels, (y + row) * texture.width + x, slice, row * width, width);
                        corrections.TryGetValue(sprite, out var correction); correction.sprite = sprite;
                        baked.Add(Bake(slice, width, height, correction));
                    }
                }
                finally { Object.DestroyImmediate(texture); }
            }
            // 본체 영역만 모아 원본 시트 크기의 마스크를 프레임마다 보관하지 않는다.
            var ordered = baked.OrderByDescending(b => b.sourceRect.height)
                .ThenBy(b => AssetDatabase.GetAssetPath(b.sprite), StringComparer.Ordinal)
                .ThenBy(b => b.sprite.name, StringComparer.Ordinal).ToArray();
            int page = 0, cursorX = Padding, cursorY = Padding, rowHeight = 0;
            var heights = new List<int>();
            foreach (BakedMask mask in ordered)
            {
                int width = mask.sourceRect.width, height = mask.sourceRect.height;
                if (width + Padding * 2 > AtlasSize || height + Padding * 2 > AtlasSize)
                    throw new InvalidOperationException("Mask exceeds atlas size: " + mask.sprite);
                if (cursorX + width + Padding > AtlasSize) { cursorX = Padding; cursorY += rowHeight + Padding * 2; rowHeight = 0; }
                if (cursorY + height + Padding > AtlasSize)
                {
                    heights.Add(Mathf.NextPowerOfTwo(cursorY - Padding));
                    page++; cursorX = Padding; cursorY = Padding; rowHeight = 0;
                }
                mask.page = page; mask.atlasRect = new RectInt(cursorX, cursorY, width, height);
                cursorX += width + Padding * 2; rowHeight = Mathf.Max(rowHeight, height);
            }
            heights.Add(Mathf.NextPowerOfTwo(cursorY + rowHeight + Padding));
            var atlases = new Texture2D[heights.Count];
            for (int i = 0; i < heights.Count; i++)
            {
                var pixels = new byte[AtlasSize * heights[i]];
                foreach (BakedMask mask in ordered.Where(m => m.page == i))
                    for (int y = 0; y < mask.atlasRect.height; y++)
                        Array.Copy(mask.pixels, y * mask.atlasRect.width, pixels,
                            (mask.atlasRect.y + y) * AtlasSize + mask.atlasRect.x, mask.atlasRect.width);
                string path = $"Assets/Resources/AudienceOutlineMask_{i:00}.png";
                // 선택 여부는 1비트로 충분하다. 해상도 손실 없이 메모리를 1/8로 줄인다.
                var packed = new byte[pixels.Length / 8];
                for (int at = 0; at < pixels.Length; at++)
                    if (pixels[at] != 0) packed[at >> 3] |= (byte)(1 << (at & 7));
                var texture = new Texture2D(AtlasSize / 8, heights[i], TextureFormat.R8, false, true);
                try
                {
                    texture.LoadRawTextureData(packed); texture.Apply(false);
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(texture); }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false; importer.mipmapEnabled = false;
                importer.isReadable = false; importer.alphaSource = TextureImporterAlphaSource.None;
                importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                var fallback = importer.GetDefaultPlatformTextureSettings();
                fallback.maxTextureSize = AtlasSize; fallback.format = TextureImporterFormat.R8;
                fallback.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(fallback);
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                    name = "Standalone", overridden = true, maxTextureSize = AtlasSize,
                    format = TextureImporterFormat.R8, textureCompression = TextureImporterCompression.Uncompressed });
                importer.SaveAndReimport();
                atlases[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            data.SetEntries(ordered.Select(m => new AudienceOutlineRegions.Entry {
                sprite = m.sprite, mask = atlases[m.page], sourcePixelRect = new Rect(
                    m.sourceRect.x, m.sourceRect.y, m.sourceRect.width, m.sourceRect.height),
                maskUvRect = new Rect((float)m.atlasRect.x / AtlasSize, (float)m.atlasRect.y / heights[m.page],
                    (float)m.atlasRect.width / AtlasSize, (float)m.atlasRect.height / heights[m.page]) }).ToArray());
            EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
            Debug.Log($"[Audience] Body masks: {baked.Count} frames, {atlases.Length} atlases, " +
                $"{baked.Sum(m => m.removedPixels)} unrelated opaque pixels excluded.");
        }
    }
}
