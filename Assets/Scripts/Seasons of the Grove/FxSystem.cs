using UnityEngine;

/// Semua partikel lewat SATU ParticleSystem + EmitParams (pola World.Emit prototipe): murah dan tanpa prefab.
/// Jenis: sparkle, dust, rain, ember, smoke, flame, leaf, fly. Ukuran diskalakan ke ukuran sel hex.
public class FxSystem : MonoBehaviour
{
    ParticleSystem ps;

    void Awake()
    {
        GameEvents.Fx = Emit;
        var g = new GameObject("FX");
        ps = g.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false; main.maxParticles = 4000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 1f; main.startSpeed = 0f;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;

        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var r = g.GetComponent<ParticleSystemRenderer>();
        var m = new Material(Shader.Find("Sprites/Default"));
        m.mainTexture = GreyboxSprites.RadialTex(32, Color.white, 1f, 0f, 1.4f);
        r.material = m; r.renderMode = ParticleSystemRenderMode.Billboard;
        ps.Play();
    }

    void Update()
    {
        if (Game.Frozen || Game.Grid == null) return;
        // api: bara, asap, lidah api dari setiap pohon terbakar (asap besar agar terlihat dari jauh)
        foreach (var c in Game.Grid.All)
        {
            if (c.state != TileState.Terbakar) continue;
            if (Random.value < 0.04f) Emit("ember", c.pos);
            if (Random.value < 0.03f) Emit("smoke", c.pos);
            if (Random.value < 0.10f) Emit("flame", c.pos);
        }
    }

    void Emit(string kind, Vector3 p)
    {
        if (ps == null) return;
        float s = Game.Grid != null ? Game.Grid.HexSize : 1f;
        var ep = new ParticleSystem.EmitParams();
        ep.position = p + new Vector3(Random.Range(-.3f, .3f) * s, .3f * s, Random.Range(-.3f, .3f) * s);
        switch (kind)
        {
            case "sparkle":
                ep.velocity = new Vector3(Random.Range(-.3f, .3f), 1f, Random.Range(-.3f, .3f)) * s;
                ep.startColor = new Color(1f, .95f, .63f); ep.startSize = .22f * s; ep.startLifetime = .9f; break;
            case "dust":
                ep.velocity = new Vector3(Random.Range(-.4f, .4f), .6f, Random.Range(-.4f, .4f)) * s;
                ep.startColor = new Color(.78f, .72f, .6f, .8f); ep.startSize = .16f * s; ep.startLifetime = .5f; break;
            case "rain":
                ep.position = p + new Vector3(Random.Range(-.3f, .3f) * s, 2.2f * s, Random.Range(-.3f, .3f) * s);
                ep.velocity = new Vector3(0, -5f * s, 0);
                ep.startColor = new Color(.5f, .75f, 1f); ep.startSize = .08f * s; ep.startLifetime = .4f; break;
            case "ember":
                ep.velocity = new Vector3(Random.Range(-.5f, .5f), 1.3f, Random.Range(-.5f, .5f)) * s;
                ep.startColor = new Color(1f, .7f, .28f); ep.startSize = .1f * s; ep.startLifetime = 1.1f; break;
            case "flame":
                ep.position = p + new Vector3(Random.Range(-.25f, .25f) * s, .7f * s, Random.Range(-.25f, .25f) * s);
                ep.velocity = new Vector3(0, .9f * s, 0);
                ep.startColor = new Color(1f, Random.Range(.4f, .7f), .1f); ep.startSize = .4f * s; ep.startLifetime = .5f; break;
            case "smoke":
                ep.position = p + new Vector3(0, 1f * s, 0);
                ep.velocity = new Vector3(Random.Range(-.2f, .2f), 1.4f, Random.Range(-.2f, .2f)) * s;
                ep.startColor = new Color(.3f, .3f, .3f, .5f); ep.startSize = .9f * s; ep.startLifetime = 3.5f; break;
            case "leaf":
                ep.position = p;
                ep.velocity = new Vector3(Random.Range(-.3f, .3f), -1f, Random.Range(-.3f, .3f)) * s;
                ep.startColor = Palette.Fall[(int)Game.Clock.CurrentSeason]; ep.startSize = .14f * s; ep.startLifetime = 8f; break;
            case "fly":
                ep.position = p + new Vector3(0, (.5f + Random.value * 1.5f) * s, 0);
                ep.velocity = new Vector3(Random.Range(-.1f, .1f), .05f, Random.Range(-.1f, .1f)) * s;
                ep.startColor = new Color(1f, .94f, .63f); ep.startSize = .16f * s; ep.startLifetime = 4f; break;
        }
        ps.Emit(ep, 1);
    }
}
