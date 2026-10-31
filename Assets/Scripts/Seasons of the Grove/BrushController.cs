using UnityEngine;
using UnityEngine.InputSystem;

/// 1/2/3 brush | Z/X 1x/2x | P jeda | G grid. Klik-kiri tahan: mengusap, atau (jika mulai di intruder) mengusir.
public class BrushController : MonoBehaviour
{
    enum M { None, Hold, Brush }
    Camera cam; LineRenderer ring;
    M mode; Intruder holding; float holdT;
    readonly RaycastHit[] buf = new RaycastHit[16];

    void Start()
    {
        cam = Camera.main;
        var g = new GameObject("BrushRing");
        ring = g.AddComponent<LineRenderer>();
        ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 32;
        ring.widthMultiplier = Game.Grid.HexSize * 0.05f;
        ring.material = new Material(Shader.Find("Sprites/Default"));
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (int i = 0; i < 32; i++) { float a = i / 32f * Mathf.PI * 2f; ring.SetPosition(i, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a))); }
    }

    Intruder IntruderUnder(Ray r)
    {
        int n = Physics.RaycastNonAlloc(r, buf, 5000f);
        Intruder best = null; float bd = float.MaxValue;
        for (int k = 0; k < n; k++)
        {
            var i = buf[k].collider.GetComponentInParent<Intruder>();
            if (i != null && buf[k].distance < bd) { bd = buf[k].distance; best = i; }
        }
        return best;
    }

    void Update()
    {
        var mouse = Mouse.current; var kb = Keyboard.current;
        if (mouse == null || kb == null || cam == null) return;
        if (Game.Menu) { ring.gameObject.SetActive(false); return; }

        if (!Game.Modal)
        {
            if (kb.digit1Key.wasPressedThisFrame) Game.Brush = BrushType.Benih;
            if (kb.digit2Key.wasPressedThisFrame) Game.Brush = BrushType.Hujan;
            if (kb.digit3Key.wasPressedThisFrame) Game.Brush = BrushType.Sapu;
            if (kb.zKey.wasPressedThisFrame) Game.Clock.speed = 1;
            if (kb.xKey.wasPressedThisFrame) Game.Clock.speed = 2;
            if (kb.pKey.wasPressedThisFrame) Game.Paused = !Game.Paused;
            if (kb.gKey.wasPressedThisFrame) Game.Grid.ShowOverlay = !Game.Grid.ShowOverlay;
        }

        Vector2 mp = mouse.position.ReadValue();
        bool over = Game.PointerOver(mp);
        Ray ray = cam.ScreenPointToRay(mp);

        if (mouse.leftButton.wasPressedThisFrame)
        {
            mode = M.None; holding = null;
            if (!over && !Game.Photo && !Game.Frozen)
            {
                var intr = IntruderUnder(ray);
                if (intr != null) { mode = M.Hold; holding = intr; holdT = 0f; }
                else mode = M.Brush;
            }
        }
        if (!mouse.leftButton.isPressed)
        {
            if (holding != null) holding.SetHoldProgress(0f);
            holding = null; mode = M.None;
        }

        // --- Mengusir: kursor harus tetap di intruder (real-time) ---
        if (mode == M.Hold)
        {
            ring.gameObject.SetActive(false);
            if (holding == null || Game.Frozen || IntruderUnder(ray) != holding)
            {
                if (holding != null) holding.SetHoldProgress(0f);
                holding = null; mode = M.None;
            }
            else
            {
                holdT += Time.unscaledDeltaTime;
                holding.SetHoldProgress(holdT / Game.Cfg.repelHold);
                if (holdT >= Game.Cfg.repelHold) { holding.Repel(); holding = null; mode = M.None; }
            }
            return;
        }

        // --- Kursor & brush ---
        Cell center = null;
        if (!over && !Game.Photo && Pick.Ground(cam, mp, out var pt)) center = Game.Grid.WorldToCell(pt);
        ring.gameObject.SetActive(center != null);
        if (center == null) return;

        float rad = Game.Brush == BrushType.Benih ? 0.8f : 1.9f;
        ring.transform.position = center.pos + Vector3.up * 0.25f;
        ring.transform.localScale = Vector3.one * Game.Grid.HexSize * rad;
        Color rc = !center.buildable ? new Color(1f, 0.3f, 0.3f, 0.85f)
            : Game.Brush == BrushType.Benih ? new Color(0.5f, 1f, 0.5f, 0.9f)
            : Game.Brush == BrushType.Hujan ? new Color(0.5f, 0.8f, 1f, 0.9f) : new Color(1f, 0.9f, 0.5f, 0.9f);
        ring.startColor = ring.endColor = rc;

        if (mode == M.Brush && center.buildable && !Game.Frozen) Paint(center);
    }

    void Paint(Cell center)
    {
        var cfg = Game.Cfg; var grid = Game.Grid; float dt = Time.deltaTime;
        int rad = Game.Brush == BrushType.Benih ? 0 : cfg.brushRadius;
        bool any = false;
        foreach (var c in grid.GetInRadius(center, rad))
        {
            if (!c.buildable) continue;
            switch (Game.Brush)
            {
                case BrushType.Benih: if (grid.Plant(c)) any = true; break;
                case BrushType.Sapu: if (grid.Sweep(c, dt)) any = true; break;
                case BrushType.Hujan:
                    if (c.state == TileState.Terbakar)
                    {
                        any = true;
                        c.fireMeter -= cfg.hujanRate * dt;
                        if (Random.value < 0.4f) GameEvents.Emit("rain", c.pos);
                        if (c.fireMeter <= 0f) grid.Extinguish(c);
                    }
                    else if (cfg.preventiveRain && c.IsHealthyTree)
                    {
                        any = true; grid.Wet(c);
                        if (Random.value < 0.1f) GameEvents.Emit("rain", c.pos);
                    }
                    break;
            }
        }
        if (any && Game.Brush != BrushType.Benih && Sfx.I != null) Sfx.I.BrushTick(Game.Brush, dt);
    }
}
