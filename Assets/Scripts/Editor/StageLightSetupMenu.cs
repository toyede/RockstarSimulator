using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 무대 조명 셋업 메뉴. (HypeSetupMenu · AudioSetupMenu 와 같은 방식)
    ///
    /// Tools/Lighting/Setup Stage Lighting 한 번이면:
    ///   1. StageLighting 루트 생성 (+ StageLightController / StageLightEventBridge)
    ///   2. 씬에 이미 있는 Global Light 2D 를 <b>재사용</b> (두 번째 Global Light 를 만들지 않는다)
    ///   3. 좌우 Stage Light 2D(Point) 생성 및 무대 쪽을 비추도록 각도 설정
    ///   4. 컨트롤러에 세 조명 연결
    /// 까지 끝난다. 이미 있는 것은 건드리지 않으므로 여러 번 실행해도 안전하다.
    /// </summary>
    public static class StageLightSetupMenu
    {
        const string PixelCookieFolder = "Assets/Resources/Lighting";
        const string PixelCookiePath = PixelCookieFolder + "/PixelStageLightCookie.png";
        const int PixelCookieSize = 64;
        const int PixelCookieBands = 8;

        [MenuItem("Tools/Lighting/Setup Stage Lighting", false, 0)]
        public static void SetupScene()
        {
            var root = GameObject.Find("StageLighting");
            if (root == null)
            {
                root = new GameObject("StageLighting");
                Undo.RegisterCreatedObjectUndo(root, "Create StageLighting");
            }

            // 1) 기존 Global Light 재사용 — 새로 만들지 않는다
            var global = FindGlobalLight();
            if (global == null)
            {
                Debug.LogWarning("[StageLight] 씬에서 Global Light 2D 를 찾지 못했습니다. " +
                                 "GameObject > Light > Global Light 2D 로 하나 만든 뒤 다시 실행하세요.");
            }
            else if (global.transform.parent == null)
            {
                // 정리 차원에서 루트 밑으로 모아둔다 (컴포넌트·설정은 그대로)
                Undo.SetTransformParent(global.transform, root.transform, "Move Global Light");
            }

            // 2) 좌우 스테이지 라이트
            var left = GetOrCreateStageLight(root.transform, "Left Stage Light 2D", new Vector3(-4.5f, 3.5f, 0f), 160f);
            var right = GetOrCreateStageLight(root.transform, "Right Stage Light 2D", new Vector3(4.5f, 3.5f, 0f), 20f);

            // 3) 컨트롤러 + 브리지
            var controller = root.GetComponent<StageLightController>() ?? Undo.AddComponent<StageLightController>(root);
            if (root.GetComponent<StageLightEventBridge>() == null) Undo.AddComponent<StageLightEventBridge>(root);

            SetObjectField(controller, "globalLight", global);
            SetObjectField(controller, "leftStageLight", left);
            SetObjectField(controller, "rightStageLight", right);
            ApplyPixelStyle(controller, left, right, GetOrCreatePixelCookie());

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log("[StageLight] 셋업 완료. 인스펙터 ⋮ 메뉴의 Debug/Set Chill·Singalong·Mosh 로 먼저 확인하세요. " +
                      "(씬을 Ctrl+S 로 저장할 것)");
        }

        [MenuItem("Tools/Lighting/Apply Pixel Stage Light Style", false, 1)]
        public static void ApplyPixelStageLightStyle()
        {
            var root = GameObject.Find("StageLighting");
            var controller = root != null ? root.GetComponent<StageLightController>() : null;
            if (controller == null)
            {
                Debug.LogWarning(
                    "[StageLight] StageLighting/StageLightController가 없습니다. " +
                    "먼저 Tools/Lighting/Setup Stage Lighting을 실행하세요.");
                return;
            }

            var serialized = new SerializedObject(controller);
            var left = serialized.FindProperty("leftStageLight")?.objectReferenceValue as Light2D;
            var right = serialized.FindProperty("rightStageLight")?.objectReferenceValue as Light2D;
            Sprite cookie = GetOrCreatePixelCookie();
            if (cookie == null) return;

            ApplyPixelStyle(controller, left, right, cookie);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeObject = cookie;
            Debug.Log(
                $"[StageLight] 픽셀 라이트 적용 완료: {PixelCookiePath} " +
                "(씬을 Ctrl+S 로 저장할 것)");
        }

        [MenuItem("Tools/Lighting/Create Pixel Stage Light Cookie Asset", false, 20)]
        public static void CreatePixelStageLightCookieAsset()
        {
            Sprite cookie = GetOrCreatePixelCookie();
            if (cookie == null) return;

            Selection.activeObject = cookie;
            Debug.Log($"[StageLight] 픽셀 라이트 쿠키 준비 완료: {PixelCookiePath}");
        }

        /// <summary>씬에 이미 있는 Global 타입 Light2D 를 찾는다.</summary>
        static Light2D FindGlobalLight()
        {
            var lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].lightType == Light2D.LightType.Global) return lights[i];
            return null;
        }

        /// <summary>무대를 향하는 스포트 형태의 Point Light 2D. 이미 있으면 설정을 덮어쓰지 않는다.</summary>
        static Light2D GetOrCreateStageLight(Transform parent, string name, Vector3 localPosition, float rotationZ)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                var found = existing.GetComponent<Light2D>();
                if (found != null) return found;
            }

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);

            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.pointLightInnerAngle = 40f;   // 스포트라이트 형태
            light.pointLightOuterAngle = 75f;
            light.pointLightInnerRadius = 1.5f;
            light.pointLightOuterRadius = 11f;  // 무대 전체를 덮을 정도
            light.intensity = 1f;
            light.falloffIntensity = 0.6f;
            return light;
        }

        static void ApplyPixelStyle(
            StageLightController controller,
            Light2D left,
            Light2D right,
            Sprite cookie)
        {
            if (controller == null || cookie == null) return;

            Undo.RecordObject(controller, "Apply Pixel Stage Light Style");
            SetObjectField(controller, "pixelLightCookie", cookie);

            ApplyPixelStyleToLight(left, cookie);
            ApplyPixelStyleToLight(right, cookie);
            controller.ApplyPixelLightStyle();
            EditorUtility.SetDirty(controller);
        }

        static void ApplyPixelStyleToLight(Light2D light, Sprite cookie)
        {
            if (light == null || light.lightType != Light2D.LightType.Point) return;

            Undo.RecordObject(light, "Apply Pixel Stage Light Cookie");
            light.lightCookieSprite = cookie;
            light.falloffIntensity = 0.95f;
            light.shadowSoftness = 0.05f;
            light.shadowSoftnessFalloffIntensity = 0.05f;
            EditorUtility.SetDirty(light);
        }

        static Sprite GetOrCreatePixelCookie()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(PixelCookiePath);
            if (existing != null) return existing;

            EnsureFolder(PixelCookieFolder);

            var texture = new Texture2D(
                PixelCookieSize,
                PixelCookieSize,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: true);
            texture.name = "PixelStageLightCookie";

            var colors = new Color32[PixelCookieSize * PixelCookieSize];
            for (int y = 0; y < PixelCookieSize; y++)
            {
                for (int x = 0; x < PixelCookieSize; x++)
                {
                    float nx = ((x + 0.5f) / PixelCookieSize) * 2f - 1f;
                    float ny = ((y + 0.5f) / PixelCookieSize) * 2f - 1f;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    float intensity = Mathf.Clamp01(1f - radius);
                    intensity = Mathf.Pow(intensity, 0.55f);
                    intensity = Mathf.Round(intensity * (PixelCookieBands - 1))
                                / (PixelCookieBands - 1f);

                    byte value = (byte)Mathf.RoundToInt(intensity * 255f);
                    colors[y * PixelCookieSize + x] = new Color32(value, value, value, value);
                }
            }

            texture.SetPixels32(colors);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            File.WriteAllBytes(PixelCookiePath, png);

            AssetDatabase.ImportAsset(PixelCookiePath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(PixelCookiePath) is not TextureImporter importer)
            {
                Debug.LogError($"[StageLight] 픽셀 쿠키 Importer를 찾지 못했습니다: {PixelCookiePath}");
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelCookieSize;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(PixelCookiePath);
        }

    }
}
