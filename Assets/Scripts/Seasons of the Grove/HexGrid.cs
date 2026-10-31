using System.Collections.Generic;
using UnityEngine;

/// Grid hex pointy-top (axial) di atas Terrain.
/// Overlay seluruh sel = SATU mesh dengan vertex color (1 draw call); hanya sel yang berubah yang ditulis ulang
/// (pola "dirty" dari World.cs prototipe: satu tekstur tanah, update hanya yang berubah).
public class HexGrid : MonoBehaviour
{
    public readonly List<Cell> All = new();
    readonly Dictionary<Vector2Int, Cell> map = new();

    public Vector3 Center;
    public float Radius, HexSize;
    public int TreeCount, DewasaCount, BurningCount, BuildableCount;
    public float Density => BuildableCount == 0 ? 0f : (float)TreeCount / BuildableCount;
    public float MapAspect => 1.5f * Game.Cfg.gridH / (S3 * (Game.Cfg.gridW + 0.5f));

    bool showOverlay = true;
    public bool ShowOverlay
    {
        get => showOverlay;
        set { showOverlay = value; foreach (var c in All) if (c.buildable) SetOverlay(c); }
    }

    Vector3 gridOrigin;
    float refreshTimer;
    Mesh overlayMesh; Color[] oCols; bool oDirty;

    static readonly Vector2Int[] Dirs = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, -1), new(-1, 1) };
    static readonly float S3 = Mathf.Sqrt(3f);
    const int VPC = 13;   // vertex per sel

    static readonly Color KotorCol = new(0.35f, 0.25f, 0.18f, 0.65f);
    static readonly Color BersihCol = new(0.8f, 0.7f, 0.5f, 0.35f);
    static readonly Color TreeGroundCol = new(0.2f, 0.35f, 0.15f, 0.25f);
    static readonly Color[] StageCol =
    {
        new(0.75f, 0.92f, 0.5f), new(0.5f, 0.85f, 0.4f), new(0.22f, 0.65f, 0.28f), new(0.08f, 0.42f, 0.18f)
    };

    void Awake() => Build();

    // ---------- Build ----------
    void Build()
    {
        var cfg = Game.Cfg; var t = Game.Terrain;
        HexSize = cfg.hexSize;
        Vector3 origin = Vector3.zero;
        if (t != null)
        {
            var td = t.terrainData;
            origin = t.transform.position + new Vector3(td.size.x / 2f, 0, td.size.z / 2f);
            if (cfg.fitToTerrain)
            {
                float w = S3 * (cfg.gridW + 0.5f), d = 1.5f * (cfg.gridH - 1) + 2f;
                HexSize = cfg.fitRatio * Mathf.Min(td.size.x / w, td.size.z / d);
            }
        }
        origin += new Vector3(cfg.gridOffset.x, 0, cfg.gridOffset.y);
        float cx = HexSize * S3 * (cfg.gridW - 0.5f) / 2f, cz = 1.5f * HexSize * (cfg.gridH - 1) / 2f;
        gridOrigin = new Vector3(origin.x - cx, 0, origin.z - cz);

        var root = new GameObject("Grid").transform;
        var prefab = Game.Assets.treePrefab;
        var verts = new List<Vector3>(); var tris = new List<int>();

        for (int r = 0; r < cfg.gridH; r++)
            for (int col = 0; col < cfg.gridW; col++)
            {
                int q = col - (r - (r & 1)) / 2;
                var c = new Cell { coord = new Vector2Int(q, r) };
                var p = gridOrigin + new Vector3(HexSize * S3 * (q + r / 2f), 0, HexSize * 1.5f * r);
                p.y = SampleY(p);
                c.pos = p;
                c.buildable = IsBuildable(c);
                All.Add(c); map[c.coord] = c;
                if (!c.buildable) continue;

                c.vStart = verts.Count;
                AddHex(c.pos, verts, tris);

                var tile = new GameObject($"Hex {q},{r}");
                tile.transform.SetParent(root, false);
                tile.transform.position = c.pos;

                TreeView tv = prefab != null ? Instantiate(prefab, tile.transform) : TreeView.CreateFallback(tile.transform, HexSize / 2.5f);
                var jitter = Random.insideUnitCircle * HexSize * 0.15f;
                tv.transform.localPosition = new Vector3(jitter.x, 0, jitter.y);
                tv.transform.localRotation = Quaternion.Euler(0, Random.value * 360f, 0);
                tv.gameObject.SetActive(false);
                c.view = tv;

                var fb = new GameObject("FireBar");
                fb.transform.SetParent(tile.transform, false);
                fb.transform.localPosition = new Vector3(0, HexSize * 1.15f, 0);
                fb.transform.localScale = new Vector3(0.01f, HexSize * 0.08f, 1f);
                var sr = fb.AddComponent<SpriteRenderer>();
                sr.sprite = GreyboxSprites.White; sr.color = new Color(0.3f, 0.65f, 1f); sr.sortingOrder = 5;
                fb.AddComponent<Billboard>().yAxisOnly = false;
                fb.SetActive(false);
                c.fireBar = sr;
            }

        // overlay: satu mesh untuk semua sel buildable
        oCols = new Color[verts.Count];
        overlayMesh = new Mesh { name = "GridOverlay" };
        overlayMesh.SetVertices(verts); overlayMesh.SetTriangles(tris, 0);
        overlayMesh.SetColors(oCols);
        overlayMesh.RecalculateNormals(); overlayMesh.RecalculateBounds();
        var og = new GameObject("GridOverlay");
        og.AddComponent<MeshFilter>().sharedMesh = overlayMesh;
        var mr = og.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));   // transparan + vertex color
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        Vector3 sum = Vector3.zero; int n = 0;
        foreach (var c in All) if (c.buildable) { sum += c.pos; n++; }
        BuildableCount = n;
        Center = n > 0 ? sum / n : origin;
        foreach (var c in All) if (c.buildable) Radius = Mathf.Max(Radius, Vector3.Distance(Flat(c.pos), Flat(Center)));
        Radius += HexSize;
        if (n == 0) Debug.LogWarning("Tidak ada sel buildable. Cek waterLevel / ukuran terrain / maxSlope.");

        foreach (var c in All) if (c.buildable) Refresh(c);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

    public float SampleY(Vector3 p)
    {
        var t = Game.Terrain;
        return t == null ? 0f : t.SampleHeight(p) + t.transform.position.y;
    }

    bool IsBuildable(Cell c)
    {
        var t = Game.Terrain; var cfg = Game.Cfg;
        if (t == null) return true;
        var td = t.terrainData; var tp = t.transform.position;
        float minY = float.MaxValue;
        for (int i = -1; i < 6; i++)
        {
            Vector3 p = c.pos;
            if (i >= 0)
            {
                float a = Mathf.Deg2Rad * (60f * i - 30f);
                p += new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * HexSize * 0.8f;
            }
            if (p.x < tp.x || p.x > tp.x + td.size.x || p.z < tp.z || p.z > tp.z + td.size.z) return false;
            minY = Mathf.Min(minY, SampleY(p));
        }
        if (minY < Game.WaterY + cfg.shoreMargin) return false;
        float steep = td.GetSteepness((c.pos.x - tp.x) / td.size.x, (c.pos.z - tp.z) / td.size.z);
        return steep <= cfg.maxSlope;
    }

    // Hex mengikuti kontur terrain (pusat + 2 cincin), koordinat dunia
    void AddHex(Vector3 c, List<Vector3> v, List<int> t)
    {
        int b = v.Count;
        v.Add(Rim(c, 0, 0f));
        for (int i = 0; i < 6; i++) v.Add(Rim(c, i, 0.5f));   // b+1..b+6
        for (int i = 0; i < 6; i++) v.Add(Rim(c, i, 1f));     // b+7..b+12
        for (int i = 0; i < 6; i++)
        {
            int j = (i + 1) % 6;
            Tri(v, t, b, b + 1 + i, b + 1 + j);
            Tri(v, t, b + 1 + i, b + 7 + i, b + 7 + j);
            Tri(v, t, b + 1 + i, b + 7 + j, b + 1 + j);
        }
    }
    Vector3 Rim(Vector3 c, int i, float k)
    {
        float a = Mathf.Deg2Rad * (60f * i - 30f);
        var w = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * HexSize * 0.95f * k;
        w.y = SampleY(w) + 0.12f;
        return w;
    }
    static void Tri(List<Vector3> v, List<int> t, int a, int b, int c)
    {
        if (Vector3.Cross(v[b] - v[a], v[c] - v[a]).y < 0) (b, c) = (c, b);
        t.Add(a); t.Add(b); t.Add(c);
    }

    // ---------- Query ----------
    public IEnumerable<Cell> GetNeighbors(Cell c)
    {
        foreach (var d in Dirs)
            if (map.TryGetValue(c.coord + d, out var n)) yield return n;
    }

    public IEnumerable<Cell> GetInRadius(Cell c, int radius)
    {
        yield return c;
        if (radius >= 1) foreach (var n in GetNeighbors(c)) yield return n;
    }

    public Cell WorldToCell(Vector3 world)
    {
        var p = world - gridOrigin; float s = HexSize;
        float fx = (S3 / 3f * p.x - p.z / 3f) / s;
        float fz = (2f / 3f * p.z) / s;
        float fy = -fx - fz;
        float rx = Mathf.Round(fx), ry = Mathf.Round(fy), rz = Mathf.Round(fz);
        float dx = Mathf.Abs(rx - fx), dy = Mathf.Abs(ry - fy), dz = Mathf.Abs(rz - fz);
        if (dx > dy && dx > dz) rx = -ry - rz;
        else if (dy > dz) ry = -rx - rz;
        else rz = -rx - ry;
        map.TryGetValue(new Vector2Int((int)rx, (int)rz), out var cell);
        return cell;
    }

    /// Koordinat 0..1 pada peta mini (x ke kanan, y ke atas = +z).
    public Vector2 WorldToUV(Vector3 w)
    {
        var p = w - gridOrigin; float s = HexSize; var cfg = Game.Cfg;
        return new Vector2((p.x + S3 * s * 0.5f) / (S3 * s * (cfg.gridW + 0.5f)), (p.z + 0.75f * s) / (1.5f * s * cfg.gridH));
    }
    public Vector3 UVToWorld(Vector2 uv)
    {
        float s = HexSize; var cfg = Game.Cfg;
        var w = gridOrigin + new Vector3(uv.x * S3 * s * (cfg.gridW + 0.5f) - S3 * s * 0.5f, 0, uv.y * 1.5f * s * cfg.gridH - 0.75f * s);
        w.y = Mathf.Max(SampleY(w), Game.WaterY);
        return w;
    }

    // ---------- Mutasi state ----------
    public bool Plant(Cell c)
    {
        if (!c.buildable || c.state != TileState.Bersih) return false;
        c.state = TileState.Benih; c.age = 0; c.stageTimer = 0; c.hp = Game.Cfg.treeHP; c.wet = 0;
        Refresh(c); c.view.Pop();
        GameEvents.Sound("plant");
        for (int j = 0; j < 4; j++) GameEvents.Emit("sparkle", c.pos);
        return true;
    }

    /// Menyapu bertahap (progres 0..1) seperti prototipe, bukan instan.
    public bool Sweep(Cell c, float dt)
    {
        if (!c.buildable || c.state != TileState.Kotor) return false;
        c.clean += dt * Game.Cfg.sweepSpeed;
        if (Random.value < 0.3f) GameEvents.Emit("dust", c.pos);
        if (c.clean >= 1f) { c.clean = 0; c.state = TileState.Bersih; Refresh(c); }
        else SetOverlay(c);
        return true;
    }

    public void Kill(Cell c, KillReason reason = KillReason.Reset)
    {
        bool wasTree = c.IsLiving;
        c.state = TileState.Kotor;
        c.age = 0; c.stageTimer = 0; c.fireMeter = 0; c.clean = 0; c.wet = 0;
        Refresh(c);
        if (!wasTree) return;
        switch (reason)
        {
            case KillReason.Burned: for (int j = 0; j < 3; j++) GameEvents.Emit("smoke", c.pos); break;
            case KillReason.Chopped:
                GameEvents.Sound("bad");
                for (int j = 0; j < 4; j++) GameEvents.Emit("dust", c.pos);
                break;
            case KillReason.Aged:
                for (int j = 0; j < 6; j++) GameEvents.Emit("leaf", c.pos + Vector3.up * HexSize * 1.2f);
                break;
        }
    }

    public void Ignite(Cell c)
    {
        if (!c.IsHealthyTree || c.wet > 0f) return;   // pohon basah kebal api
        c.stageBeforeFire = c.state; c.state = TileState.Terbakar; c.fireMeter = Game.Cfg.fireMeter;
        Refresh(c);
    }

    public void Extinguish(Cell c)
    {
        if (c.state != TileState.Terbakar) return;
        c.state = c.stageBeforeFire; c.fireMeter = 0; c.wet = 3f;
        Refresh(c);
        GameEvents.Sound("ok");
    }

    public void Wet(Cell c) { if (c.IsHealthyTree) c.wet = Game.Cfg.wetDuration; }

    // ---------- Visual ----------
    void Update()
    {
        int trees = 0, dewasa = 0, burning = 0;
        foreach (var c in All)
        {
            if (!c.buildable) continue;
            if (c.IsLiving) trees++;
            if (c.state == TileState.Dewasa) dewasa++;
            if (c.state == TileState.Terbakar) burning++;
        }
        TreeCount = trees; DewasaCount = dewasa; BurningCount = burning;

        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f) { refreshTimer = 0.2f; foreach (var c in All) Refresh(c); }
    }

    void LateUpdate()
    {
        if (!oDirty) return;
        oDirty = false;
        overlayMesh.SetColors(oCols);
    }

    Color OverlayColor(Cell c)
    {
        if (!showOverlay) return Color.clear;
        if (c.state == TileState.Kotor) return Color.Lerp(KotorCol, BersihCol, c.clean);
        if (c.state == TileState.Bersih) return BersihCol;
        return TreeGroundCol;
    }

    void SetOverlay(Cell c)
    {
        var col = OverlayColor(c);
        if (oCols[c.vStart] == col) return;
        for (int i = 0; i < VPC; i++) oCols[c.vStart + i] = col;
        oDirty = true;
    }

    public void Refresh(Cell c)
    {
        if (!c.buildable) return;
        SetOverlay(c);

        bool tree = c.IsLiving, burning = c.state == TileState.Terbakar;
        if (c.view.gameObject.activeSelf != tree) c.view.gameObject.SetActive(tree);
        if (c.fireBar.gameObject.activeSelf != burning) c.fireBar.gameObject.SetActive(burning);
        if (!tree) return;

        var cfg = Game.Cfg;
        int i = Mathf.Clamp(c.StageIndex, 0, 3);
        var sb = burning ? c.stageBeforeFire : c.state;
        float prog = sb < TileState.Dewasa ? Mathf.Clamp01(c.stageTimer / cfg.stageDuration) : 0f;

        // Aset asli: tint putih (warna daun dibuat di material). Greybox: warna per tahap + sedikit warna musim.
        Color col = Game.Assets.treePrefab == null
            ? Color.Lerp(StageCol[i], Palette.Leaf[(int)Game.Clock.CurrentSeason], 0.22f)
            : Color.white;

        float life = cfg.lifespanSeasons * cfg.daysPerSeason * cfg.dayLength;
        if (c.age > life * 0.75f)   // musim terakhir: menguning (GDD 5)
            col = Color.Lerp(col, new Color(0.9f, 0.8f, 0.2f), Mathf.InverseLerp(life * 0.75f, life, c.age));

        if (burning)
        {
            // hangus bertahap menurut HP (gradien arang -> abu, dari prototipe) + kilau oranye
            float flick = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f + c.coord.x);
            col = Color.Lerp(new Color(0.16f, 0.14f, 0.12f), new Color(0.42f, 0.35f, 0.23f), Mathf.Clamp01(c.hp / cfg.treeHP));
            col = Color.Lerp(col, new Color(1f, 0.45f, 0.1f), 0.3f * flick * Mathf.Clamp01(c.fireMeter / cfg.fireMeter));
            c.fireBar.transform.localScale = new Vector3(Mathf.Max(0.01f, c.fireMeter / cfg.fireMeter) * HexSize * 0.8f, HexSize * 0.08f, 1f);
        }
        else if (c.wet > 0f) col = Color.Lerp(col, new Color(0.31f, 0.66f, 1f), 0.3f);

        c.view.Apply(i, prog, col);
    }
}
