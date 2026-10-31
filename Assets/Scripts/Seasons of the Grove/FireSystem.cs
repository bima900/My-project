using System.Collections.Generic;
using UnityEngine;

public class FireSystem : MonoBehaviour
{
    float acc; int tick;
    readonly List<Cell> burning = new();
    readonly HashSet<Cell> toIgnite = new();

    void Update()
    {
        acc += Game.Clock.dt;
        while (acc >= 1f) { acc -= 1f; Tick(); }
    }

    float Chance(Cell n)
    {
        if (n.wet > 0f) return 0f;
        switch (n.state)
        {
            case TileState.Tunas: return Game.Cfg.chanceTunas;
            case TileState.Muda: return Game.Cfg.chanceMuda;
            case TileState.Dewasa: return Game.Cfg.chanceDewasa;
            default: return 0f;   // Benih kebal
        }
    }

    void Tick()
    {
        tick++;
        var cfg = Game.Cfg; var grid = Game.Grid;
        burning.Clear();

        foreach (var c in grid.All)
        {
            if (c.state != TileState.Terbakar) continue;
            c.hp -= cfg.hpPerTick;
            if (c.hp <= 0f) grid.Kill(c, KillReason.Burned);
            else burning.Add(c);
        }

        if (tick % cfg.spreadEveryTicks != 0) return;

        toIgnite.Clear();
        foreach (var b in burning)
            foreach (var n in grid.GetNeighbors(b))
            {
                float p = Chance(n);
                if (p > 0f && !toIgnite.Contains(n) && Random.value < p) toIgnite.Add(n);
            }
        foreach (var n in toIgnite) grid.Ignite(n);
    }
}
