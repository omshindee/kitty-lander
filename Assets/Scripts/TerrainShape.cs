using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders a PolygonCollider2D as cartoon terrain: a tiled rock fill,
/// a dark outline around every edge and a decorative strip (grass, sand,
/// snow, moon crust) along the edges that face upwards.
/// Edit the collider's points and the visuals rebuild automatically.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(PolygonCollider2D), typeof(MeshFilter), typeof(MeshRenderer))]
public class TerrainShape : MonoBehaviour
{
    [Tooltip("World units covered by one repeat of the fill texture.")]
    [SerializeField] private float fillTileSize = 4f;
    [Tooltip("Height of the top edge strip in world units.")]
    [SerializeField] private float edgeHeight = 0.9f;
    [Tooltip("Edges whose outward normal has at least this much 'up' get the top strip.")]
    [SerializeField, Range(0f, 1f)] private float edgeMinUp = 0.45f;
    [SerializeField] private float outlineWidth = 0.12f;
    [SerializeField] private Color outlineColor = new Color(0.17f, 0.11f, 0.08f, 1f);
    [SerializeField] private int sortingOrder = -5;

    private Mesh mesh;

    public void SetStyle(int order)
    {
        sortingOrder = order;
    }

    private void OnEnable() => Rebuild();
    private void OnValidate() => Rebuild();

    private void OnDisable()
    {
        if (mesh != null)
        {
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            mesh = null;
        }
    }

    public void Rebuild()
    {
        var col = GetComponent<PolygonCollider2D>();
        var mf = GetComponent<MeshFilter>();
        var mr = GetComponent<MeshRenderer>();
        if (col == null || mf == null || mr == null) return;
        mr.sortingOrder = sortingOrder;

        if (mesh == null)
        {
            mesh = new Mesh { name = "TerrainMesh", hideFlags = HideFlags.DontSave };
        }
        mesh.Clear();

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var fillTris = new List<int>();
        var outlineTris = new List<int>();
        var edgeTris = new List<int>();

        for (int p = 0; p < col.pathCount; p++)
        {
            var pts = new List<Vector2>(col.GetPath(p));
            if (pts.Count < 3) continue;
            if (SignedArea(pts) < 0f) pts.Reverse(); // make counter-clockwise

            BuildFill(pts, verts, uvs, colors, fillTris);
            BuildOutline(pts, verts, uvs, colors, outlineTris);
            BuildTopStrip(pts, verts, uvs, colors, edgeTris);
        }

        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.subMeshCount = 3;
        mesh.SetTriangles(fillTris, 0);
        mesh.SetTriangles(outlineTris, 1);
        mesh.SetTriangles(edgeTris, 2);
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;
    }

    // ---------------------------------------------------------------- fill

    private void BuildFill(List<Vector2> pts, List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> tris)
    {
        int start = v.Count;
        Vector2 world = transform.position;
        foreach (var p in pts)
        {
            v.Add(p);
            uv.Add((p + world) / fillTileSize);
            c.Add(Color.white);
        }
        foreach (int i in Triangulate(pts)) tris.Add(start + i);
    }

    // ------------------------------------------------------------- outline

    private void BuildOutline(List<Vector2> pts, List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> tris)
    {
        float hw = outlineWidth * 0.5f;
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = pts[i], b = pts[(i + 1) % n];
            Vector2 dir = (b - a).normalized;
            Vector2 nrm = new Vector2(dir.y, -dir.x) * hw;
            AddQuad(v, uv, c, tris, a - nrm, b - nrm, b + nrm, a + nrm, outlineColor);
            AddDisc(v, uv, c, tris, a, hw, outlineColor);
        }
    }

    // ----------------------------------------------------------- top strip

    private void BuildTopStrip(List<Vector2> pts, List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> tris)
    {
        int n = pts.Count;
        var isTop = new bool[n]; // edge i goes from pts[i] to pts[i+1]
        bool any = false, all = true;
        for (int i = 0; i < n; i++)
        {
            Vector2 d = (pts[(i + 1) % n] - pts[i]).normalized;
            Vector2 outward = new Vector2(d.y, -d.x);
            isTop[i] = outward.y >= edgeMinUp;
            any |= isTop[i];
            all &= isTop[i];
        }
        if (!any || all) return;

        // Find the start of each run of consecutive top edges.
        for (int i = 0; i < n; i++)
        {
            if (!isTop[i] || isTop[(i - 1 + n) % n]) continue;
            var run = new List<Vector2> { pts[i] };
            int j = i;
            while (isTop[j])
            {
                run.Add(pts[(j + 1) % n]);
                j = (j + 1) % n;
                if (j == i) break;
            }
            BuildStrip(run, v, uv, c, tris);
        }
    }

    private void BuildStrip(List<Vector2> run, List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> tris)
    {
        // Extend the run slightly at both ends so it tucks over the corners.
        float ext = edgeHeight * 0.25f;
        run[0] -= (run[1] - run[0]).normalized * ext;
        int last = run.Count - 1;
        run[last] += (run[last] - run[last - 1]).normalized * ext;

        // The edge textures draw the surface line 45% up from the bottom.
        float below = edgeHeight * 0.45f, above = edgeHeight * 0.55f;
        float uScale = 1f / (edgeHeight * 4f); // texture is 4:1
        float dist = 0f;
        int start = v.Count;
        for (int k = 0; k < run.Count; k++)
        {
            Vector2 nrm;
            if (k == 0) nrm = Perp(run[1] - run[0]);
            else if (k == last) nrm = Perp(run[last] - run[last - 1]);
            else
            {
                Vector2 n1 = Perp(run[k] - run[k - 1]), n2 = Perp(run[k + 1] - run[k]);
                nrm = (n1 + n2).normalized;
                float miter = Mathf.Max(0.5f, Vector2.Dot(nrm, n1));
                nrm /= miter;
            }
            if (k > 0) dist += Vector2.Distance(run[k], run[k - 1]);
            float u = dist * uScale;
            v.Add(run[k] - nrm * below); uv.Add(new Vector2(u, 0f)); c.Add(Color.white);
            v.Add(run[k] + nrm * above); uv.Add(new Vector2(u, 1f)); c.Add(Color.white);
            if (k > 0)
            {
                int i0 = start + (k - 1) * 2;
                tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 3);
                tris.Add(i0); tris.Add(i0 + 3); tris.Add(i0 + 2);
            }
        }
    }

    // ------------------------------------------------------------- helpers

    private static Vector2 Perp(Vector2 d)
    {
        d.Normalize();
        return new Vector2(d.y, -d.x); // outward for a CCW polygon
    }

    private static void AddQuad(List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> tris,
        Vector2 a, Vector2 b, Vector2 cc, Vector2 d, Color col)
    {
        int s = v.Count;
        v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
        for (int i = 0; i < 4; i++) { uv.Add(new Vector2(0.5f, 0.5f)); c.Add(col); }
        tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
        tris.Add(s); tris.Add(s + 3); tris.Add(s + 2);
    }

    private static void AddDisc(List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> tris, Vector2 centre, float r, Color col)
    {
        const int seg = 8;
        int s = v.Count;
        v.Add(centre); uv.Add(new Vector2(0.5f, 0.5f)); c.Add(col);
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            v.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            uv.Add(new Vector2(0.5f, 0.5f));
            c.Add(col);
        }
        for (int i = 0; i < seg; i++)
        {
            tris.Add(s); tris.Add(s + 1 + (i + 1) % seg); tris.Add(s + 1 + i);
        }
    }

    private static float SignedArea(List<Vector2> pts)
    {
        float a = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 p = pts[i], q = pts[(i + 1) % pts.Count];
            a += p.x * q.y - q.x * p.y;
        }
        return a * 0.5f;
    }

    /// <summary>Ear-clipping triangulation for a simple counter-clockwise polygon.</summary>
    private static List<int> Triangulate(List<Vector2> pts)
    {
        var result = new List<int>();
        var idx = new List<int>();
        for (int i = 0; i < pts.Count; i++) idx.Add(i);

        int guard = 0;
        while (idx.Count > 3 && guard++ < 10000)
        {
            bool clipped = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int i0 = idx[(i - 1 + idx.Count) % idx.Count], i1 = idx[i], i2 = idx[(i + 1) % idx.Count];
                Vector2 a = pts[i0], b = pts[i1], c = pts[i2];
                if (Cross(b - a, c - b) <= 1e-6f) continue; // reflex vertex
                bool contains = false;
                for (int k = 0; k < idx.Count; k++)
                {
                    int ik = idx[k];
                    if (ik == i0 || ik == i1 || ik == i2) continue;
                    if (PointInTriangle(pts[ik], a, b, c)) { contains = true; break; }
                }
                if (contains) continue;
                result.Add(i0); result.Add(i2); result.Add(i1); // clockwise winding for Unity front faces
                idx.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped) break; // degenerate polygon - give up gracefully
        }
        if (idx.Count == 3) { result.Add(idx[0]); result.Add(idx[2]); result.Add(idx[1]); }
        return result;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }
}
