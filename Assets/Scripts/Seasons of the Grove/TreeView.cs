using UnityEngine;

/// Pasang di root prefab pohon (bersama LODGroup).
///   LOD0 = mesh detail, LOD1 = mesh rendah, LOD2 = SpriteRenderer (+ Billboard yAxisOnly).
/// Satu prefab untuk 4 tahap. Skala tumbuh mulus di dalam tahap, dan "pop" membal saat tahap berganti
/// (ide dari World.LayoutTree prototipe).
public class TreeView : MonoBehaviour
{
    public LODGroup lodGroup;
    public SpriteRenderer lodSprite;
    public Renderer[] meshRenderers;
    public Sprite[] stageSprites;               // opsional, 4 elemen
    public float[] stageScale = { 0.25f, 0.5f, 0.8f, 1f };
    public float baseLodSize = 3f;
    public bool counterScaleSprite = true;
    public string colorProperty = "_BaseColor";
    [HideInInspector] public float worldScale = 1f;

    int lastStage = -1;
    Color lastTint = new Color(-1, -1, -1, -1);
    float curS, targetS, pop, lastS = -1f;
    static MaterialPropertyBlock pb;

    void Reset() => AutoAssign();

    [ContextMenu("Auto-assign")]
    public void AutoAssign()
    {
        lodGroup = GetComponent<LODGroup>();
        lodSprite = GetComponentInChildren<SpriteRenderer>(true);
        var list = new System.Collections.Generic.List<Renderer>();
        foreach (var r in GetComponentsInChildren<MeshRenderer>(true)) list.Add(r);
        meshRenderers = list.ToArray();
    }

    void OnDisable()
    {
        lastStage = -1; lastTint = new Color(-1, -1, -1, -1); pop = 0f; lastS = -1f;
    }

    public void Pop() { pop = 1f; }

    public void Apply(int stage, float progress, Color tint)
    {
        stage = Mathf.Clamp(stage, 0, 3);
        float s0 = stageScale[stage], s1 = stage < 3 ? stageScale[stage + 1] : s0;
        targetS = Mathf.Lerp(s0, s1, progress) * worldScale;

        if (stage != lastStage)
        {
            bool first = lastStage < 0;
            lastStage = stage;
            if (first) curS = targetS; else pop = Mathf.Max(pop, 0.6f);
            if (lodGroup != null) lodGroup.size = baseLodSize / stageScale[stage];
            if (lodSprite != null && stageSprites != null && stage < stageSprites.Length && stageSprites[stage] != null)
                lodSprite.sprite = stageSprites[stage];
        }

        if (tint == lastTint) return;
        lastTint = tint;
        if (lodSprite != null) lodSprite.color = tint;
        pb ??= new MaterialPropertyBlock();
        if (meshRenderers == null) return;
        foreach (var r in meshRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(pb);
            pb.SetColor(colorProperty, tint);
            r.SetPropertyBlock(pb);
        }
    }

    void Update()
    {
        if (lastStage < 0) return;
        float dt = Time.deltaTime;
        if (pop > 0.001f) pop = Mathf.Max(0f, pop * Mathf.Pow(0.93f, dt * 60f) - 0.0005f); else pop = 0f;
        curS = Mathf.Lerp(curS, targetS, 1f - Mathf.Exp(-6f * dt));
        float s = curS * (1f + 0.3f * pop * Mathf.Sin((1f - pop) * 14f));
        if (Mathf.Abs(s - lastS) < 1e-4f) return;
        lastS = s;
        transform.localScale = Vector3.one * s;
        if (lodSprite != null && counterScaleSprite) lodSprite.transform.localScale = Vector3.one * (1f / Mathf.Max(s, 1e-4f));
    }

    // ---------- Fallback greybox: capsule (LOD0) -> cylinder (LOD1) -> sprite (LOD2) ----------
    public static TreeView CreateFallback(Transform parent, float worldScale)
    {
        var go = new GameObject("TreeView(fallback)");
        go.transform.SetParent(parent, false);
        var tv = go.AddComponent<TreeView>();
        tv.worldScale = worldScale; tv.counterScaleSprite = false; tv.baseLodSize = 2.2f;

        Renderer Prim(PrimitiveType t, Vector3 scale)
        {
            var p = GameObject.CreatePrimitive(t);
            Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(go.transform, false);
            p.transform.localPosition = new Vector3(0, 1f, 0);
            p.transform.localScale = scale;
            return p.GetComponent<Renderer>();
        }
        var r0 = Prim(PrimitiveType.Capsule, new Vector3(1.2f, 1f, 1.2f));
        var r1 = Prim(PrimitiveType.Cylinder, new Vector3(1.0f, 1f, 1.0f));
        GreyboxPaint.Set(r0, Color.white); GreyboxPaint.Set(r1, Color.white);

        var sg = new GameObject("LOD2 Sprite");
        sg.transform.SetParent(go.transform, false);
        var sr = sg.AddComponent<SpriteRenderer>();
        sr.sprite = GreyboxSprites.Tree;
        sg.AddComponent<Billboard>().yAxisOnly = true;

        var lg = go.AddComponent<LODGroup>();
        var c = Game.Cfg;
        lg.SetLODs(new[]
        {
            new LOD(c.lod0, new[] { r0 }),
            new LOD(c.lod1, new[] { r1 }),
            new LOD(c.lod2, new Renderer[] { sr }),
        });
        lg.RecalculateBounds();
        tv.lodGroup = lg; tv.lodSprite = sr; tv.meshRenderers = new[] { r0, r1 };
        return tv;
    }
}
