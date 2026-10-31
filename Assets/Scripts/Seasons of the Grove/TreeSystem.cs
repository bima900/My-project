using UnityEngine;

/// Pertumbuhan 4 tahap + umur (gugur setelah 4 musim) + pengeringan pohon basah. Pohon terbakar dijeda.
public class TreeSystem : MonoBehaviour
{
    void Update()
    {
        var cfg = Game.Cfg; float dt = Game.Clock.dt;
        if (dt <= 0f) return;
        float life = cfg.lifespanSeasons * cfg.daysPerSeason * cfg.dayLength;
        float mult = Game.Clock.CurrentSeason == Season.Winter ? 1f - cfg.winterGrowthSlow : 1f;

        foreach (var c in Game.Grid.All)
        {
            if (!c.IsHealthyTree) continue;
            if (c.wet > 0f) c.wet -= dt;
            c.age += dt;
            if (c.age >= life) { Game.Grid.Kill(c, KillReason.Aged); continue; }
            if (c.state < TileState.Dewasa)
            {
                c.stageTimer += dt * mult;
                if (c.stageTimer >= cfg.stageDuration)
                {
                    c.stageTimer = 0;
                    c.state = (TileState)((int)c.state + 1);
                    Game.Grid.Refresh(c);
                }
            }
        }
    }
}
