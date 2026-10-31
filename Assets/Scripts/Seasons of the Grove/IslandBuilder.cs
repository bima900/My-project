using UnityEngine;

/// Menyiapkan Terrain + air. Memakai Terrain/air milik kamu bila ada; kalau tidak, membuat pulau prosedural.
public static class IslandBuilder
{
    public static void Prepare()
    {
        var cfg = Game.Cfg; var a = Game.Assets;
        Terrain t = a.terrain != null ? a.terrain : Terrain.activeTerrain;
        if (t == null && cfg.generateIslandIfMissing) t = Generate(cfg);
        Game.Terrain = t;

        Transform water = a.waterPlane;
        if (water == null && t != null) water = MakeWater(t, a);
        if (water != null)
        {
            // collider air tidak boleh menghalangi raycast brush
            foreach (var c in water.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            Game.WaterY = water.position.y;
        }
        else Game.WaterY = -10000f;
    }

    static Transform MakeWater(Terrain t, AssetRefs a)
    {
        var td = t.terrainData; var p = t.transform.position;
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = "Water";
        float ext = Mathf.Max(td.size.x, td.size.z) * 4f;
        go.transform.localScale = new Vector3(ext / 10f, 1f, ext / 10f);   // Plane = 10x10 unit
        go.transform.position = new Vector3(p.x + td.size.x / 2f, Game.Cfg.waterLevel, p.z + td.size.z / 2f);
        var r = go.GetComponent<Renderer>();
        if (a.waterMaterial != null) r.sharedMaterial = a.waterMaterial;
        else
        {
            var sh = Shader.Find("Greybox/Water");
            if (sh != null) r.sharedMaterial = new Material(sh);
            else GreyboxPaint.Set(r, new Color(0.2f, 0.5f, 0.7f));
        }
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go.transform;
    }

    static Terrain Generate(GameConfig cfg)
    {
        int res = 257;
        var td = new TerrainData { heightmapResolution = res };
        td.size = new Vector3(cfg.generatedSize, cfg.generatedHeight, cfg.generatedSize);

        var h = new float[res, res];
        float off = cfg.generatedSeed * 13.7f;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float nx = x / (res - 1f), ny = y / (res - 1f);
                float d = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.5f, 0.5f)) / 0.5f;
                float f = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d / 0.8f));
                float n = Mathf.PerlinNoise(nx * 5f + off, ny * 5f + off);
                float n2 = Mathf.PerlinNoise(nx * 14f + off, ny * 14f + off);
                h[y, x] = Mathf.Lerp(0.04f, 0.55f, f) * (0.8f + 0.3f * n + 0.1f * n2);
            }
        td.SetHeights(0, 0, h);

        td.terrainLayers = new[] { Layer(new Color(0.86f, 0.78f, 0.55f)), Layer(new Color(0.38f, 0.6f, 0.3f)) };
        int ar = 128; td.alphamapResolution = ar;
        var am = new float[ar, ar, 2];
        for (int y = 0; y < ar; y++)
            for (int x = 0; x < ar; x++)
            {
                float hy = h[Mathf.RoundToInt(y / (ar - 1f) * (res - 1)), Mathf.RoundToInt(x / (ar - 1f) * (res - 1))] * cfg.generatedHeight;
                float s = Mathf.InverseLerp(cfg.waterLevel + 2.5f, cfg.waterLevel + 0.5f, hy);   // pasir dekat air
                am[y, x, 0] = s; am[y, x, 1] = 1f - s;
            }
        td.SetAlphamaps(0, 0, am);

        var go = Terrain.CreateTerrainGameObject(td);
        go.name = "Island Terrain (generated)";
        return go.GetComponent<Terrain>();
    }

    static TerrainLayer Layer(Color c)
    {
        var tex = new Texture2D(4, 4);
        for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
        tex.Apply();
        return new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(6, 6) };
    }
}
