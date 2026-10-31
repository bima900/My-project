using UnityEngine;

/// Aturan spawn GDD 7.1 + peringatan sebelum muncul (terompet, toast, penanda di peta mini).
public class IntruderManager : MonoBehaviour
{
    public static IntruderManager Instance;
    public bool Warned { get; private set; }
    public Vector3 WarnPos { get; private set; }
    float timer, warnAngle;

    void Awake() { Instance = this; }
    public void ResetTimer() { timer = 0f; Warned = false; }

    static Vector3 RingPos(float ang)
    {
        var g = Game.Grid;
        var dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
        var p = g.Center + dir * (g.Radius + Game.Cfg.spawnMargin);
        p.y = Mathf.Max(g.SampleY(p), Game.WaterY);
        return p;
    }

    void Update()
    {
        var g = Game.Grid; var cfg = Game.Cfg;
        bool can = g.DewasaCount >= cfg.minDewasaToSpawn && g.Density < cfg.densityThreshold;
        if (!can) { timer = 0f; Warned = false; return; }   // di >= 80% jeda diulang penuh saat turun lagi

        float dt = Game.Clock.dt;
        if (dt <= 0f) return;
        timer += dt;

        if (!Warned && timer >= cfg.spawnInterval - cfg.spawnWarning)
        {
            Warned = true; warnAngle = Random.value * Mathf.PI * 2f; WarnPos = RingPos(warnAngle);
            GameEvents.Sound("horn");
            Game.Toast(Game.IntruderName(Game.Clock.CurrentSeason) + " mendekat dari tepi pulau!");
        }
        if (Warned && Random.value < 0.3f) GameEvents.Emit("sparkle", WarnPos);

        if (timer >= cfg.spawnInterval)
        {
            timer = 0f; Warned = false;
            int max = Mathf.Min(g.DewasaCount / cfg.dewasaPerIntruder, cfg.maxIntruders);
            if (Intruder.Active.Count < max) SpawnAt(warnAngle);
        }
    }

    static Intruder SpawnAt(float ang)
    {
        var s = Game.Clock.CurrentSeason;
        bool burner = s == Season.Plain || s == Season.Autumn;   // GDD 7.4
        return Intruder.Spawn(RingPos(ang), burner, s);
    }

    public static Intruder SpawnOne() { return SpawnAt(Random.value * Mathf.PI * 2f); }
}
