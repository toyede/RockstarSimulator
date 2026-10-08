using ContextStage;
using GameJamKit;
using UnityEditor;
using UnityEngine;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// Tools/Tour/Setup Ending Catalog   Resources/Tour/TourEndingCatalog.asset 생성 (있으면 유지, 빈 항목만 채움)
    /// Tools/Save/Reset All Save Data    PlayerPrefs 기반 저장(튜토리얼 완료·로컬 순위 등) 전부 삭제 — 테스트 빌드 확인용
    /// </summary>
    public static class TourEndingSetup
    {
        const string ResourcesFolder = "Assets/Resources/Augments/Tour";
        const string CatalogPath = ResourcesFolder + "/TourEndingCatalog.asset";

        [MenuItem("Tools/Tour/Setup Ending Catalog", false, 24)]
        public static void SetupCatalog()
        {
            EditorSetupUtility.EnsureFolder(ResourcesFolder);

            var catalog = AssetDatabase.LoadAssetAtPath<TourEndingCatalog>(CatalogPath);
            bool created = catalog == null;
            if (created)
            {
                catalog = ScriptableObject.CreateInstance<TourEndingCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.EnsureDefaults();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TourEndingSetup] {(created ? "생성" : "갱신")}: {CatalogPath}. 엔딩 일러스트는 인스펙터의 entries[*].illustration 에 넣는다.");
        }

        [MenuItem("Tools/Save/Reset All Save Data", false, 200)]
        public static void ResetAllSaveData()
        {
            if (!EditorUtility.DisplayDialog("저장 데이터 초기화",
                    "튜토리얼 완료 기록·로컬 순위 등 PlayerPrefs 저장을 전부 지웁니다. 계속할까요?", "지우기", "취소"))
                return;
            Save.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[Save] 저장 데이터를 전부 삭제했습니다 (테스트 빌드 첫 실행 상태).");
        }
    }
}
