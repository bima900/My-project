using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// Siang-malam mengikuti jam game (1 siklus = 1 hari), langit/ambient/kabut per musim (dihaluskan saat pergantian),
/// bayangan awan, daun/salju musiman, kunang-kunang malam. Logika UpdateEnv diadaptasi dari World.cs prototipe.
/// Juga mengirim global shader "_SeasonTint" untuk dipakai shader pohon/rumput (GDD 11: warna musim lewat shader).
public class EnvironmentController : MonoBehaviour
{
    public float Night, Dn = 1f;
    Light sun; Material skyM; Camera cam;
    readonly List<Transform> clouds = new List<Transform>();
    Color sky, tint = Color.white; float leafAcc;

    void Start()
    {
        cam = Camera.main;
        foreach (var l in FindObjectsByType<Light>())
            if (l.type == LightType.Directional) { sun = l; break; }
        if (sun == null) { sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; }
        sun.shadows = LightShadows.Soft; sun.shadowStrength = .9f;
        RenderSettings.sun = sun;

        var sh = Shader.Find("Skybox/Procedural");
        if (sh != null) { skyM = new Material(sh); RenderSettings.skybox = skyM; cam.clearFlags = CameraClearFlags.Skybox; }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        QualitySettings.shadowDistance = Mathf.Max(60f, Game.Grid.Radius * 6f);   // (URP: atur di URP Asset)

        sky = Palette.Sky[(int)Game.Clock.CurrentSeason];
        BuildClouds();
    }

    void BuildClouds()
    {
        var g = Game.Grid; float s = g.HexSize; var root = new GameObject("CloudShadows").transform;
        for (int c = 0; c < 5; c++)
        {
            var t = new GameObject("cloud").transform; t.SetParent(root, false);
            t.position = new Vector3(g.Center.x + Random.Range(-1f, 1f) * g.Radius * 1.2f, g.Center.y + g.Radius * 1.1f, g.Center.z + Random.Range(-1f, 1f) * g.Radius);
            for (int j = 0; j < 3; j++)
            {
                var sp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(sp.GetComponent<Collider>());
                sp.transform.SetParent(t, false);
                sp.transform.localPosition = new Vector3((j * 2f - 2f) * s, Random.value * .4f * s, (Random.value - .5f) * s);
                sp.transform.localScale = new Vector3(3.6f, 2f, 3f) * s;
                var r = sp.GetComponent<Renderer>();
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly; r.sharedMaterial = GreyboxPaint.Mat;
            }
            clouds.Add(t);
        }
    }

    void Update()
    {
        var clk = Game.Clock; var cfg = Game.Cfg; float dt = Time.deltaTime;
        int se = (int)clk.CurrentSeason;

        float hh = 1f, sa = -.6f;
        if (cfg.dayLength > 0f && cfg.dayNight) { sa = clk.DayFraction * Mathf.PI * 2f - .6f; hh = Mathf.Cos(sa); }
        Dn = Mathf.Clamp(.5f + hh * .6f, .2f, 1f);
        Night = Mathf.Max(0f, 1.2f - Dn * 1.4f);

        var sv = new Vector3(Mathf.Sin(sa) * .9f, hh * .95f + .05f, -.4f).normalized;
        var lv = hh >= 0f ? sv : -sv; if (lv.y < .08f) lv.y = .08f;
        sun.transform.rotation = Quaternion.LookRotation(-lv);
        sun.intensity = hh >= 0f ? .35f + 1.0f * Mathf.Min(1f, hh * 1.7f) : .15f;
        sun.color = hh >= 0f ? Color.Lerp(Palette.Hex(0xff9a6a), Color.white, Mathf.Clamp01(hh * 2.4f)) : Palette.Hex(0x93aaff);

        float k = 1f - Mathf.Exp(-.8f * dt);   // pergantian musim mulus
        sky = Color.Lerp(sky, Palette.Sky[se], k);
        tint = Color.Lerp(tint, Color.Lerp(Color.white, Palette.Leaf[se], .35f), k);
        Shader.SetGlobalColor("_SeasonTint", tint);

        var skyNow = Color.Lerp(sky * .15f, sky, Dn); skyNow.a = 1f;
        RenderSettings.ambientSkyColor = skyNow * .75f;
        RenderSettings.ambientEquatorColor = skyNow * .55f;
        RenderSettings.ambientGroundColor = skyNow * .3f;
        RenderSettings.fogColor = skyNow;
        float R = Game.Grid.Radius;
        RenderSettings.fogStartDistance = R * 2.8f; RenderSettings.fogEndDistance = R * 12f;
        if (skyM != null)
        {
            skyM.SetFloat("_AtmosphereThickness", Mathf.Lerp(.6f, 1.3f, Dn));
            skyM.SetFloat("_Exposure", .3f + 1f * Dn);
            skyM.SetColor("_SkyTint", Color.Lerp(sky, Palette.Hex(0x808080), .4f));
            skyM.SetFloat("_SunSize", hh > -.05f ? .06f : 0f);
        }

        float s = Game.Grid.HexSize;
        foreach (var c in clouds)
        {
            c.position += Vector3.right * .35f * s * dt;
            if (c.position.x > Game.Grid.Center.x + R * 1.6f) c.position = new Vector3(Game.Grid.Center.x - R * 1.6f, c.position.y, c.position.z);
        }

        if (Game.Frozen) return;
        // daun / bunga / salju jatuh di sekitar fokus kamera (musim Plain tidak ada)
        if (se != 0)
        {
            leafAcc += dt * 8f;
            while (leafAcc >= 1f)
            {
                leafAcc -= 1f; var f = Game.CamFocus;
                GameEvents.Emit("leaf", new Vector3(f.x + Random.Range(-.8f, .8f) * R, f.y + s * 4f, f.z + Random.Range(-.8f, .8f) * R));
            }
        }
        if (Night > .3f && Random.value < dt * 2f)
        {
            var cell = Game.Grid.All[Random.Range(0, Game.Grid.All.Count)];
            if (cell.buildable) GameEvents.Emit("fly", cell.pos);
        }
    }
}
