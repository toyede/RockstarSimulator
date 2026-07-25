using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 무대 픽셀 먼지 셋업. 고정 카메라 화면에 아주 미세한 공기감·깊이감만 더한다.
    ///
    /// <c>Tools/VFX/Setup Stage Pixel Dust</c> 한 번이면:
    /// <list type="number">
    /// <item>4×4 픽셀 먼지 PNG 생성 (없을 때만) — Point 필터, 압축 없음, 밉맵 없음</item>
    /// <item>URP Particles/Unlit 투명 머티리얼 생성 (없을 때만)</item>
    /// <item><c>StagePixelDust</c> 루트 + <c>Dust_Back</c> / <c>Dust_Front</c> 생성 (없을 때만)</item>
    /// <item>Main Camera 의 Orthographic Size·Aspect 로 Box 크기와 입자 크기 계산</item>
    /// </list>
    ///
    /// <b>여러 번 실행해도 오브젝트·에셋이 중복 생성되지 않는다.</b>
    /// 이미 있는 것은 참조만 다시 연결하고 값은 덮어쓰지 않는다.
    ///
    /// 깊이감은 <b>가짜다</b> — 실제 패럴랙스 계산을 하지 않고 크기·속도·투명도·정렬 순서 차이만 쓴다.
    /// 카메라가 고정돼 있어 그것으로 충분하다.
    /// </summary>
    public static class StagePixelDustSetup
    {
        const string RootName = "StagePixelDust";
        const string BackName = "Dust_Back";
        const string FrontName = "Dust_Front";

        const string SpriteFolder = "Assets/Sprites/VFX";
        const string SpritePath = SpriteFolder + "/PixelDust.png";
        const string MaterialFolder = "Assets/Materials/VFX";
        const string MaterialPath = MaterialFolder + "/M_PixelDust.mat";

        /// <summary>기준 세로 해상도. ProjectSettings 의 Default Screen Height 와 같다.</summary>
        const float ReferenceVerticalResolution = 1080f;

        /// <summary>스프라이트 4×4 중 실제로 칠해진 심지는 2×2 다. 화면 픽셀 계산에 이 비율을 쓴다.</summary>
        const float SpriteCoreRatio = 0.5f;

        /// <summary>Box 를 카메라 화면보다 이만큼 크게 잡는다 (가장자리에서 입자가 튀어나오지 않게).</summary>
        const float ShapeMargin = 1.15f;

        // 정렬: 배경 0 / 무대 조명 1 / 장식 군중 2~4 / 관객 5~ / 밴드 45 / 너구리 50.
        // 카드·HUD 는 Screen Space Overlay 캔버스라 월드 스프라이트보다 항상 위에 있다.
        const string SortingLayer = "Default";
        const int BackSortingOrder = 4;   // 배경보다 앞, 관객(5)보다 뒤
        const int FrontSortingOrder = 46; // 관객·밴드(45)보다 앞, 너구리(50)보다 뒤

        [MenuItem("Tools/VFX/Setup Stage Pixel Dust", false, 0)]
        public static void Setup()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("[PixelDust] Main Camera 를 찾지 못했습니다. MainCamera 태그를 확인하세요.");
                return;
            }

            if (!camera.orthographic)
            {
                Debug.LogError(
                    "[PixelDust] Main Camera 가 Orthographic 이 아닙니다. " +
                    "이 도구는 2D 고정 카메라를 전제로 크기를 계산합니다.");
                return;
            }

            Texture2D texture = EnsureDustTexture();
            if (texture == null) return;

            Material material = EnsureDustMaterial(texture);
            if (material == null) return;

            float halfHeight = camera.orthographicSize;
            float aspect = camera.aspect > 0.01f ? camera.aspect : 16f / 9f;
            float halfWidth = halfHeight * aspect;

            // 화면 1픽셀에 해당하는 월드 크기. 기준 해상도(1080)에서 계산한다
            float worldUnitsPerPixel = halfHeight * 2f / ReferenceVerticalResolution;

            GameObject root = EnsureRoot(camera);
            ParticleSystem back = EnsureDustSystem(root.transform, BackName);
            ParticleSystem front = EnsureDustSystem(root.transform, FrontName);

            ConfigureBack(back, material, halfWidth, halfHeight, worldUnitsPerPixel);
            ConfigureFront(front, material, halfWidth, halfHeight, worldUnitsPerPixel);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = root;

            Debug.Log(
                $"[PixelDust] 셋업 완료 — 씬 '{EditorSceneManager.GetActiveScene().name}'\n" +
                $"  카메라: Orthographic Size {halfHeight}, Aspect {aspect:0.###}, " +
                $"화면 {halfWidth * 2f:0.##} × {halfHeight * 2f:0.##} 월드 유닛\n" +
                $"  1 화면 픽셀 = {worldUnitsPerPixel:0.#####} 월드 유닛 (기준 세로 {ReferenceVerticalResolution:0})\n" +
                $"  Dust_Back  : 최대 22개 / 정렬 {SortingLayer}:{BackSortingOrder} (배경 앞·관객 뒤)\n" +
                $"  Dust_Front : 최대 9개 / 정렬 {SortingLayer}:{FrontSortingOrder} (관객 앞·카드/HUD 뒤)\n" +
                "  먼지가 너무 잘 보이면 크기보다 Start Color 의 Alpha 를 먼저 낮추세요. " +
                "(씬을 Ctrl+S 로 저장할 것)",
                root);
        }

        // ---------------- 스프라이트 ----------------

        /// <summary>
        /// 4×4 안에 2×2 만 칠한 각진 먼지. 완전한 원이나 부드러운 그라데이션을 쓰지 않는다 —
        /// 픽셀아트에서 둥근 입자는 눈송이나 발광체처럼 보인다.
        /// </summary>
        static Texture2D EnsureDustTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(SpritePath);
            if (existing != null) return existing;

            EnsureFolder(SpriteFolder);

            const int Size = 4;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false, linear: false);
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    bool core = x >= 1 && x <= 2 && y >= 1 && y <= 2;
                    pixels[y * Size + x] = core
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0); // 알파만 0 — 가장자리에 검은 테두리가 생기지 않게
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            File.WriteAllBytes(SpritePath, png);
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);

            if (AssetImporter.GetAtPath(SpritePath) is not TextureImporter importer)
            {
                Debug.LogError($"[PixelDust] 텍스처 Importer 를 찾지 못했습니다: {SpritePath}");
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Size;

            // Mesh Type 은 TextureImporter 에 직접 없고 TextureImporterSettings 를 거쳐야 한다.
            // Tight 로 두면 4×4 중 투명한 가장자리가 잘려 심지 비율 계산이 틀어진다
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            importer.filterMode = FilterMode.Point;          // 픽셀이 뭉개지지 않게
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 32;
            importer.SaveAndReimport();

            Debug.Log($"[PixelDust] 픽셀 먼지 스프라이트를 만들었습니다: {SpritePath} (4×4, 심지 2×2)");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SpritePath);
        }

        // ---------------- 머티리얼 ----------------

        /// <summary>
        /// 투명 알파 블렌딩 Unlit. Additive·Emission·HDR 을 쓰지 않는다 —
        /// 먼지는 발광체가 아니라 공기 중 입자여야 하고, Bloom 을 받으면 흐릿한 원이 된다.
        /// Unlit 이라 2D 조명 색에도 물들지 않는다.
        /// </summary>
        static Material EnsureDustMaterial(Texture2D texture)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                if (existing.HasProperty("_BaseMap") && existing.GetTexture("_BaseMap") == null)
                {
                    existing.SetTexture("_BaseMap", texture);
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError(
                    "[PixelDust] 'Universal Render Pipeline/Particles/Unlit' 셰이더를 찾지 못했습니다. " +
                    "URP 패키지 버전을 확인하세요.");
                return null;
            }

            EnsureFolder(MaterialFolder);

            var material = new Material(shader) { name = "M_PixelDust" };

            SetFloatIfPresent(material, "_Surface", 1f);   // Transparent
            SetFloatIfPresent(material, "_Blend", 0f);     // Alpha (Additive 아님)
            SetFloatIfPresent(material, "_AlphaClip", 0f);
            SetFloatIfPresent(material, "_ZWrite", 0f);
            SetFloatIfPresent(material, "_Cull", (float)CullMode.Off);
            SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_SoftParticlesEnabled", 0f);
            SetFloatIfPresent(material, "_CameraFadingEnabled", 0f);
            SetFloatIfPresent(material, "_DistortionEnabled", 0f);
            SetFloatIfPresent(material, "_EmissionEnabled", 0f);

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);

            material.DisableKeyword("_EMISSION");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;

            AssetDatabase.CreateAsset(material, MaterialPath);
            Debug.Log($"[PixelDust] 먼지 머티리얼을 만들었습니다: {MaterialPath}");
            return AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        }

        static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        // ---------------- 오브젝트 ----------------

        static GameObject EnsureRoot(Camera camera)
        {
            var existing = GameObject.Find(RootName);
            if (existing != null) return existing;

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Stage Pixel Dust");

            // 카메라가 고정이라 자식으로 넣지 않는다. 화면 중앙에만 맞춰 둔다
            Vector3 cameraPosition = camera.transform.position;
            root.transform.position = new Vector3(cameraPosition.x, cameraPosition.y, 0f);
            return root;
        }

        static ParticleSystem EnsureDustSystem(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                var found = existing.GetComponent<ParticleSystem>();
                if (found != null) return found;

                return Undo.AddComponent<ParticleSystem>(existing.gameObject);
            }

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;

            return go.GetComponent<ParticleSystem>() != null
                ? go.GetComponent<ParticleSystem>()
                : Undo.AddComponent<ParticleSystem>(go);
        }

        // ---------------- 파티클 설정 ----------------

        static void ConfigureBack(
            ParticleSystem system,
            Material material,
            float halfWidth,
            float halfHeight,
            float worldUnitsPerPixel)
        {
            Undo.RecordObject(system, "Configure Dust_Back");

            ConfigureCommon(system, halfWidth, halfHeight);

            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
            // 4×4 중 심지가 2×2 이므로, 화면에서 1~2픽셀로 보이려면 쿼드는 그 두 배여야 한다
            main.startSize = new ParticleSystem.MinMaxCurve(
                worldUnitsPerPixel * 1f / SpriteCoreRatio,
                worldUnitsPerPixel * 2f / SpriteCoreRatio);
            main.maxParticles = 22;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.11f),                       // 거의 흰색
                new Color32(0xC7, 0xDC, 0xD0, (byte)(0.09f * 255))); // 팔레트 중성 회색

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 2.0f;

            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.015f, 0.015f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.01f, 0.025f);
            // z 도 반드시 x·y 와 같은 TwoConstants 모드여야 한다.
            // 한 축만 단일 상수로 두면 "Particle Velocity curves must all be in the same mode" 오류가 난다
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ConfigureRenderer(system, material, BackSortingOrder);
        }

        static void ConfigureFront(
            ParticleSystem system,
            Material material,
            float halfWidth,
            float halfHeight,
            float worldUnitsPerPixel)
        {
            Undo.RecordObject(system, "Configure Dust_Front");

            ConfigureCommon(system, halfWidth, halfHeight);

            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.035f, 0.07f);
            main.startSize = new ParticleSystem.MinMaxCurve(
                worldUnitsPerPixel * 2f / SpriteCoreRatio,
                worldUnitsPerPixel * 4f / SpriteCoreRatio);
            main.maxParticles = 9;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.16f),
                new Color32(0x9B, 0xAB, 0xB2, (byte)(0.14f * 255)));

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0.75f;

            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.025f, 0.025f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
            // z 도 반드시 x·y 와 같은 TwoConstants 모드여야 한다.
            // 한 축만 단일 상수로 두면 "Particle Velocity curves must all be in the same mode" 오류가 난다
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ConfigureRenderer(system, material, FrontSortingOrder);
        }

        static void ConfigureCommon(ParticleSystem system, float halfWidth, float halfHeight)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.duration = 10f;
            main.loop = true;
            main.startDelay = 0f;     // prewarm 은 loop=true, startDelay=0 일 때만 유효하다
            main.prewarm = true;      // 씬에 들어오자마자 이미 떠 있어야 한다
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
            main.gravityModifier = 0f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 90f * Mathf.Deg2Rad);
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            // 화면보다 넉넉히 큰 Box 로 화면 전체에 고르게 뿌린다.
            // 특정 조명이나 관객 위치에 몰리지 않게 한다
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
            shape.scale = new Vector3(
                halfWidth * 2f * ShapeMargin,
                halfHeight * 2f * ShapeMargin,
                0.01f);

            // 갑자기 나타나거나 사라지지 않게 앞뒤로 부드럽게 사라진다
            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(1f, 0.70f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            // 쓰지 않는 모듈은 명시적으로 꺼 둔다 (기본값에 기대지 않는다).
            // 모듈은 구조체로 반환되므로 지역 변수에 받아서 꺼야 한다 — 반환값에 직접 대입할 수 없다
            DisableModule(system);
        }

        static void DisableModule(ParticleSystem system)
        {
            var noise = system.noise; noise.enabled = false;
            var collision = system.collision; collision.enabled = false;
            var trails = system.trails; trails.enabled = false;
            var lights = system.lights; lights.enabled = false;
            var subEmitters = system.subEmitters; subEmitters.enabled = false;
            var trigger = system.trigger; trigger.enabled = false;
            var sizeOverLifetime = system.sizeOverLifetime; sizeOverLifetime.enabled = false;
            var rotationOverLifetime = system.rotationOverLifetime; rotationOverLifetime.enabled = false;
            var forceOverLifetime = system.forceOverLifetime; forceOverLifetime.enabled = false;
            var inheritVelocity = system.inheritVelocity; inheritVelocity.enabled = false;
            var textureSheet = system.textureSheetAnimation; textureSheet.enabled = false;
        }

        static void ConfigureRenderer(ParticleSystem system, Material material, int sortingOrder)
        {
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;

            Undo.RecordObject(renderer, "Configure Dust Renderer");

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = material;
            renderer.trailMaterial = null;
            renderer.sortingLayerName = SortingLayer;
            renderer.sortingOrder = sortingOrder;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            // 화면에서 지나치게 작아져 깜빡이거나, 지나치게 커지는 것을 막는다
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 0.5f;

            EditorUtility.SetDirty(renderer);
        }
    }
}
