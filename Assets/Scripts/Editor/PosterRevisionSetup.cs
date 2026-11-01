using System;
using UnityEditor;
using UnityEngine;

namespace ContextStage.EditorTools
{
    public static class PosterRevisionSetup
    {
        [MenuItem("Tools/Art/Apply Poster Title and Entry Revisions")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit Mode only");
            var catalog = StageVisualCatalog.LoadDefault();
            var logo = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/StagePosters/Stage01_HandwrittenTitle_v1.png");
            if (catalog == null || logo == null) throw new InvalidOperationException("Poster catalog or handwritten title missing");
            var alley = catalog.EditorGetOrAddEntry("stage_01").posterPresentation;
            alley.headlineLogo = logo;
            alley.headlineArea = new Rect(.10f, .735f, .80f, .245f);
            var club = catalog.EditorGetOrAddEntry("stage_02").posterPresentation;
            club.entryArea = new Rect(.755f, .06f, .20f, .11f);
            club.entryText = "공연\n시작";
            club.entrySize = 24;
            club.entryAngle = -3;
            var broadcast = catalog.EditorGetOrAddEntry("stage_04").posterPresentation;
            broadcast.headline = "Raccoon Roll\nON AIR!";
            broadcast.headlineSize = 56;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            Debug.Log("[PosterRevision] Stage 1 handwriting / Stage 2 lower-right entry / Stage 4 Raccoon Roll ON AIR!");
        }
    }
}
