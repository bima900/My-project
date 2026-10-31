using System.Collections.Generic;
using UnityEngine;

/// Efek suara prosedural (tanpa file audio). Generator Tone/Noise/Mix diadaptasi dari SeasonsOfTheGrove,
/// dipicu lewat GameEvents.Snd. Ganti klip dengan file audio asli saat aset suara tersedia.
public class Sfx : MonoBehaviour
{
    public static Sfx I;
    public bool mute;
    AudioSource a;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly AudioClip[] plant = new AudioClip[5];
    float sweepT;
    static readonly float[] PENT = { 523f, 587f, 659f, 784f, 880f };

    void Awake()
    {
        I = this; GameEvents.Snd = Play;
        a = gameObject.AddComponent<AudioSource>(); a.playOnAwake = false; a.spatialBlend = 0f;
        for (int i = 0; i < 5; i++) plant[i] = Tone("plant" + i, PENT[i], .5f, 1, .22f);
        clips["horn"] = Mix("horn", Tone("h1", 180f, 1.2f, 3, .2f), Tone("h2", 240f, 1.2f, 3, .12f));
        clips["season"] = Mix("season", Tone("s1", 660f, .5f, 0, .15f), Tone("s2", 880f, .6f, 0, .12f));
        clips["bad"] = Tone("bad", 110f, .4f, 2, .2f);
        clips["ok"] = Tone("ok", 520f, .3f, 1, .22f);
        clips["sweep"] = Noise("sweep", .12f, .35f, .25f);
        clips["rain"] = Noise("rain", .14f, .9f, .2f);
        clips["seed"] = Noise("seed", .08f, .1f, .2f);
    }

    public void BrushTick(BrushType b, float dt)
    {
        sweepT -= dt; if (sweepT > 0f) return; sweepT = .14f;
        Play(b == BrushType.Sapu ? "sweep" : b == BrushType.Hujan ? "rain" : "seed", .5f);
    }

    public void Play(string n, float vol = 1f)
    {
        if (mute || a == null) return;
        if (n == "plant") { a.PlayOneShot(plant[Random.Range(0, 5)], vol); return; }
        if (clips.TryGetValue(n, out var c)) a.PlayOneShot(c, vol);
    }

    // wave: 0 sinus, 1 segitiga, 2 kotak, 3 gergaji
    static AudioClip Tone(string name, float f, float dur, int wave, float vol)
    {
        int sr = 44100, n = (int)(sr * dur); var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr, ph = (t * f) % 1f, w;
            if (wave == 0) w = Mathf.Sin(ph * 6.2832f);
            else if (wave == 1) w = Mathf.Abs(ph * 4f - 2f) - 1f;
            else if (wave == 2) w = ph < .5f ? 1f : -1f;
            else w = ph * 2f - 1f;
            d[i] = w * Mathf.Exp(-t * 5f / dur) * Mathf.Min(1f, t * 200f) * vol;
        }
        var c = AudioClip.Create(name, n, 1, sr, false); c.SetData(d, 0); return c;
    }

    static AudioClip Noise(string name, float dur, float cut, float vol)
    {
        int sr = 44100, n = (int)(sr * dur); var d = new float[n]; float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr, x = Random.value * 2f - 1f;
            lp += (x - lp) * cut;
            d[i] = (cut > .5f ? x - lp : lp) * Mathf.Exp(-t * 4f / dur) * vol * 2f;
        }
        var c = AudioClip.Create(name, n, 1, sr, false); c.SetData(d, 0); return c;
    }

    static AudioClip Mix(string name, AudioClip x, AudioClip y)
    {
        int n = Mathf.Max(x.samples, y.samples); var d = new float[n];
        var ax = new float[x.samples]; x.GetData(ax, 0);
        var by = new float[y.samples]; y.GetData(by, 0);
        for (int i = 0; i < n; i++) d[i] = (i < ax.Length ? ax[i] : 0f) + (i < by.Length ? by[i] : 0f);
        var c = AudioClip.Create(name, n, 1, 44100, false); c.SetData(d, 0); return c;
    }
}
