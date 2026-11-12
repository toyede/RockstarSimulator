using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>Reveals the existing rank sprite along letter-specific pen strokes.</summary>
    [AddComponentMenu("")]
    internal sealed class ResultRankInk : BaseMeshEffect
    {
        const int Cells = 24;
        const int Steps = 28;
        const float BrushRadiusSquared = .23f * .23f;

        // (start x, start y, end x, end y) in glyph space. Separate segments can lift the pen.
        static readonly Vector4[] A = { new(.20f,.14f,.57f,.86f), new(.57f,.86f,.84f,.20f), new(.34f,.44f,.73f,.48f) };
        static readonly Vector4[] B = { new(.24f,.12f,.39f,.86f), new(.39f,.86f,.70f,.86f), new(.70f,.86f,.78f,.65f), new(.78f,.65f,.56f,.51f), new(.56f,.51f,.80f,.35f), new(.80f,.35f,.68f,.15f), new(.68f,.15f,.24f,.12f) };
        static readonly Vector4[] C = { new(.83f,.75f,.64f,.86f), new(.64f,.86f,.36f,.72f), new(.36f,.72f,.20f,.41f), new(.20f,.41f,.30f,.20f), new(.30f,.20f,.56f,.12f), new(.56f,.12f,.77f,.26f) };
        static readonly Vector4[] D = { new(.23f,.12f,.37f,.86f), new(.37f,.86f,.64f,.81f), new(.64f,.81f,.81f,.63f), new(.81f,.63f,.78f,.39f), new(.78f,.39f,.61f,.22f), new(.61f,.22f,.23f,.12f) };
        static readonly Vector4[] F = { new(.27f,.12f,.43f,.84f), new(.43f,.84f,.85f,.90f), new(.36f,.52f,.69f,.58f) };
        static readonly Vector4[] S = { new(.84f,.83f,.59f,.89f), new(.59f,.89f,.31f,.73f), new(.31f,.73f,.43f,.55f), new(.43f,.55f,.73f,.44f), new(.73f,.44f,.72f,.26f), new(.72f,.26f,.47f,.13f), new(.47f,.13f,.17f,.17f) };
        readonly UIVertex[] _quad = new UIVertex[4];
        readonly Vector4[] _visible = new Vector4[7];
        Vector4[] _path;
        int _step = Steps;

        public void Begin(string rank)
        {
            _path = rank switch { "A" => A, "B" => B, "C" => C, "D" => D, "F" => F, "S" => S, _ => null };
            _step = Steps;
            SetProgress(0f);
        }

        public void SetProgress(float progress)
        {
            int step = Mathf.FloorToInt(Mathf.Clamp01(progress) * Steps);
            if (_path == null) step = Steps;
            if (step == _step) return;
            _step = step;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || _path == null || _step == Steps || mesh.currentVertCount != 4) return;
            if (_step == 0) { mesh.Clear(); return; }

            // Image.Simple emits four vertices ordered BL, TL, TR, BR, already aspect fitted.
            var bl = default(UIVertex);
            var tr = default(UIVertex);
            mesh.PopulateUIVertex(ref bl, 0);
            mesh.PopulateUIVertex(ref tr, 2);
            mesh.Clear();
            float drawn = _step * _path.Length / (float)Steps;
            int visibleCount = Mathf.CeilToInt(drawn);
            for (int i = 0; i < visibleCount; i++)
            {
                Vector4 line = _path[i];
                float t = Mathf.Clamp01(drawn - i);
                _visible[i] = new Vector4(line.x, line.y, Mathf.Lerp(line.x, line.z, t), Mathf.Lerp(line.y, line.w, t));
            }
            for (int y = 0; y < Cells; y++)
            for (int x = 0; x < Cells; x++)
            {
                if (!HasInk((x + .5f) / Cells, (y + .5f) / Cells, visibleCount)) continue;
                float l = x / (float)Cells, r = (x + 1) / (float)Cells;
                float b = y / (float)Cells, t = (y + 1) / (float)Cells;
                _quad[0] = At(bl, tr, l, b);
                _quad[1] = At(bl, tr, l, t);
                _quad[2] = At(bl, tr, r, t);
                _quad[3] = At(bl, tr, r, b);
                mesh.AddUIVertexQuad(_quad);
            }
        }

        bool HasInk(float x, float y, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector4 s = _visible[i];
                float dx = s.z - s.x, dy = s.w - s.y;
                float u = Mathf.Clamp01(((x - s.x) * dx + (y - s.y) * dy) / Mathf.Max(.00001f, dx * dx + dy * dy));
                float ox = x - s.x - dx * u, oy = y - s.y - dy * u;
                if (ox * ox + oy * oy <= BrushRadiusSquared) return true;
            }
            return false;
        }

        static UIVertex At(UIVertex bl, UIVertex tr, float x, float y)
        {
            UIVertex v = bl;
            v.position.x = Mathf.Lerp(bl.position.x, tr.position.x, x);
            v.position.y = Mathf.Lerp(bl.position.y, tr.position.y, y);
            v.uv0.x = Mathf.Lerp(bl.uv0.x, tr.uv0.x, x);
            v.uv0.y = Mathf.Lerp(bl.uv0.y, tr.uv0.y, y);
            return v;
        }
    }
}
