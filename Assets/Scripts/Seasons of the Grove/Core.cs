using System;
using UnityEngine;

public enum TileState { Kotor, Bersih, Benih, Tunas, Muda, Dewasa, Terbakar }
/// Urutan siklus musim (sama dengan prototipe SeasonsOfTheGrove): Plain -> Spring -> Autumn -> Winter.
public enum Season { Plain, Spring, Autumn, Winter }
public enum BrushType { Benih, Hujan, Sapu }
public enum KillReason { Burned, Chopped, Aged, Reset }

[Serializable]
public class GameConfig
{
    [Header("Alur")]
    public bool startInMenu = true;
    public bool autoSlow = true;            // percepatan turun ke 1x saat ada api/intruder (GDD 17)
    public bool dayNight = true;            // siklus siang-malam (visual saja)
    public bool preventiveRain = true;      // brush Hujan di pohon sehat = basah sementara, kebal api (GDD 6.3)

    [Header("Grid")]
    public int gridW = 20, gridH = 20;
    public bool fitToTerrain = true;
    [Range(0.3f, 1f)] public float fitRatio = 0.75f;
    public float hexSize = 2.5f;
    public Vector2 gridOffset;

    [Header("Pulau & Air")]
    public float waterLevel = 5f;
    public float shoreMargin = 0.5f;
    public float maxSlope = 35f;
    public bool generateIslandIfMissing = true;
    public float generatedSize = 120f, generatedHeight = 25f;
    public int generatedSeed = 7;

    [Header("Waktu (GDD 17)")]
    public float dayLength = 240f;
    public int daysPerSeason = 3;

    [Header("Pohon (GDD 5)")]
    public float stageDuration = 120f;
    [Range(0, 0.4f)] public float winterGrowthSlow = 0.175f;
    public int lifespanSeasons = 4;

    [Header("Api & Brush (GDD 4, 6)")]
    public float treeHP = 100f;
    public float hpPerTick = 5f;
    public int spreadEveryTicks = 4;
    public float chanceTunas = 0.13f, chanceMuda = 0.24f, chanceDewasa = 0.37f;
    public float fireMeter = 100f;
    public float hujanRate = 60f;           // pengurangan meter api per detik saat diusap
    public float wetDuration = 12f;         // detik pohon basah kebal api
    public float sweepSpeed = 3.5f;         // 1 / sweepSpeed = detik untuk membersihkan satu sel
    public int brushRadius = 1;

    [Header("Intruder (GDD 7)")]
    public float spawnInterval = 120f;
    public float spawnWarning = 4f;         // peringatan sebelum spawn (detik game)
    public int minDewasaToSpawn = 5;
    public int dewasaPerIntruder = 5;
    public int maxIntruders = 6;
    [Range(0, 1)] public float densityThreshold = 0.8f;
    public float actMin = 3f, actMax = 4f;
    public float repelHold = 2.5f;
    public float intruderSpeed = 3f;
    public float spawnMargin = 8f;

    [Header("LOD pohon (fallback greybox)")]
    public float lod0 = 0.25f, lod1 = 0.08f, lod2 = 0.01f;

    [Header("Kamera (rasio terhadap radius pulau)")]
    public float panLimit = 0.9f;
    public float startDistRatio = 2.2f, minDistRatio = 0.3f, maxDistRatio = 4f;
    public float panSpeedPerDist = 0.7f;
    public float startPitch = 50f;
}

[Serializable]
public class AssetRefs
{
    public Terrain terrain;
    public Transform waterPlane;
    public Material waterMaterial;
    public TreeView treePrefab;
    public Sprite[] burnerSkins;         // index = Season (Plain, Spring, Autumn, Winter); 1 elemen = dipakai semua
    public Sprite[] cutterSkins;
    public float intruderScale = 1f;
    public Texture2D logo;               // kosong = Resources/Logo
}

public class Cell
{
    public Vector2Int coord;
    public Vector3 pos;
    public bool buildable;
    public TileState state = TileState.Kotor;
    public TileState stageBeforeFire;
    public float stageTimer, age, hp, fireMeter, clean, wet;
    public int vStart;
    public Intruder claimedBy;
    public TreeView view;
    public SpriteRenderer fireBar;

    public bool IsHealthyTree => state >= TileState.Benih && state <= TileState.Dewasa;
    public bool IsLiving => state >= TileState.Benih;
    public int StageIndex => (int)(state == TileState.Terbakar ? stageBeforeFire : state) - (int)TileState.Benih;
}

public static class Game
{
    public static GameConfig Cfg;
    public static AssetRefs Assets;
    public static HexGrid Grid;
    public static GameClock Clock;
    public static BrushType Brush;
    public static Terrain Terrain;
    public static float WaterY = -10000f;

    public static bool Menu, Paused, Photo, Modal;
    public static bool Frozen => Menu || Paused;
    public static Vector3 CamFocus;

    public static string ToastText = ""; public static float ToastT;
    public static void Toast(string s) { ToastText = s; ToastT = 5f; }

    public static Func<Vector2, bool> PointerOverUI;
    public static bool PointerOver(Vector2 p) => PointerOverUI != null && PointerOverUI(p);

    public static string IntruderName(Season s) => (s == Season.Plain || s == Season.Autumn) ? "Pembakar" : "Penebang";
}

/// Event bus sederhana: logika memicu suara/efek tanpa tahu siapa yang memutarnya (pola dari Sim.cs prototipe).
public static class GameEvents
{
    public static Action<string, float> Snd;
    public static Action<string, Vector3> Fx;
    public static void Reset() { Snd = null; Fx = null; }
    public static void Sound(string n, float v = 1f) { Snd?.Invoke(n, v); }
    public static void Emit(string k, Vector3 p) { Fx?.Invoke(k, p); }
}

public static class GameFlow
{
    public static bool Started;
    public static void NewGame()
    {
        foreach (var c in Game.Grid.All) if (c.buildable) Game.Grid.Kill(c, KillReason.Reset);
        foreach (var i in Intruder.Active.ToArray()) if (i != null) UnityEngine.Object.Destroy(i.gameObject);
        Intruder.Active.Clear();
        Game.Clock.total = 0; Game.Clock.speed = 1; Game.Clock.ResetSeason();
        if (IntruderManager.Instance != null) IntruderManager.Instance.ResetTimer();
        Started = true;
    }
}

public static class Palette
{
    public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
    // indeks = Season
    public static readonly Color[] Leaf = { Hex(0x4fa84c), Hex(0x9be070), Hex(0xe08a2e), Hex(0xcfe0e8) };
    public static readonly Color[] Sky = { Hex(0xbfe0f0), Hex(0xcfe9f2), Hex(0xe8c79a), Hex(0xcfd9e3) };
    public static readonly Color[] Fall = { new Color(.85f, .8f, .6f), new Color(1f, .7f, .8f), new Color(.85f, .5f, .17f), Color.white };
    public static readonly Color[] Accent = { new Color(.42f, .7f, .35f), new Color(.9f, .55f, .7f), new Color(.91f, .56f, .18f), new Color(.5f, .66f, .88f) };
}

public static class Pick
{
    static readonly RaycastHit[] buf = new RaycastHit[16];
    static readonly Plane flat = new Plane(Vector3.up, Vector3.zero);
    /// Titik pada Terrain di bawah kursor (fallback bidang y=0 bila tidak ada Terrain).
    public static bool Ground(Camera cam, Vector2 screen, out Vector3 p)
    {
        var r = cam.ScreenPointToRay(screen);
        int n = Physics.RaycastNonAlloc(r, buf, 5000f);
        float best = float.MaxValue; p = default; bool ok = false;
        for (int i = 0; i < n; i++)
            if (buf[i].collider is TerrainCollider && buf[i].distance < best) { best = buf[i].distance; p = buf[i].point; ok = true; }
        if (!ok && Game.Terrain == null && flat.Raycast(r, out float d)) { p = r.GetPoint(d); ok = true; }
        return ok;
    }
}

public static class GreyboxPaint
{
    static Material mat;
    static MaterialPropertyBlock pb;
    public static Material Mat
    {
        get
        {
            if (mat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                mat = new Material(sh);
            }
            return mat;
        }
    }
    public static void Set(Renderer r, Color c)
    {
        pb ??= new MaterialPropertyBlock();
        r.sharedMaterial = Mat;
        r.GetPropertyBlock(pb);
        pb.SetColor("_BaseColor", c);
        pb.SetColor("_Color", c);
        r.SetPropertyBlock(pb);
    }
}

public static class GreyboxSprites
{
    static Sprite tree, person, white;

    static Sprite Make(int w, int h, Func<int, int, bool> inside, Vector2 pivot, float ppu)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = inside(x, y) ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
        t.SetPixels32(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, w, h), pivot, ppu);
    }

    public static Sprite White => white ??= Make(4, 4, (x, y) => true, new Vector2(0.5f, 0.5f), 4);

    public static Sprite Tree => tree ??= Make(64, 128,
        (x, y) => ((x - 32) * (x - 32) + (y - 88) * (y - 88) <= 900) || (x >= 28 && x < 36 && y < 64),
        new Vector2(0.5f, 0f), 64);

    public static Sprite Person => person ??= Make(32, 64,
        (x, y) => (x >= 8 && x < 24 && y < 40) || ((x - 16) * (x - 16) + (y - 52) * (y - 52) <= 81),
        new Vector2(0.5f, 0f), 32);

    /// Gradien radial (alpha memudar ke tepi) untuk partikel dan vignette.
    public static Texture2D RadialTex(int n, Color col, float inner, float outer, float power = 1f)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + .5f) / n * 2 - 1, dy = (y + .5f) / n * 2 - 1;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                t.SetPixel(x, y, new Color(col.r, col.g, col.b, Mathf.Lerp(inner, outer, Mathf.Pow(d, power))));
            }
        t.Apply();
        t.hideFlags = HideFlags.HideAndDontSave;
        return t;
    }
}
