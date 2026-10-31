using System;
using UnityEngine;

/// Jam game sendiri (bukan Time.timeScale). dt dibatasi 0,1 detik agar lonjakan frame tidak merusak simulasi.
[DefaultExecutionOrder(-100)]
public class GameClock : MonoBehaviour
{
    public int speed = 1;          // 1 atau 2
    public float dt, total;
    public event Action SeasonChanged;
    Season last;

    public int Day => (int)(total / Game.Cfg.dayLength) + 1;
    public Season CurrentSeason => (Season)(((Day - 1) / Game.Cfg.daysPerSeason) % 4);
    public float DayFraction => (total % Game.Cfg.dayLength) / Game.Cfg.dayLength;
    public float SeasonProgress
    {
        get { float d = ((Day - 1) % Game.Cfg.daysPerSeason) + DayFraction; return d / Game.Cfg.daysPerSeason; }
    }
    /// Kecepatan nyata: otomatis 1x bila ada api/intruder dan autoSlow aktif.
    public float EffectiveSpeed
    {
        get
        {
            float s = speed;
            if (Game.Cfg.autoSlow && s > 1f && Game.Grid != null && (Game.Grid.BurningCount > 0 || Intruder.Active.Count > 0)) s = 1f;
            return s;
        }
    }
    public void ResetSeason() { last = Season.Plain; }

    void Update()
    {
        dt = Game.Frozen ? 0f : Mathf.Min(Time.deltaTime, 0.1f) * EffectiveSpeed;
        total += dt;
        var s = CurrentSeason;
        if (s != last)
        {
            last = s;
            if (total > 0.01f)
            {
                GameEvents.Sound("season");
                Game.Toast("Musim berganti: " + s + " (" + Game.IntruderName(s) + ")");
            }
            SeasonChanged?.Invoke();
        }
    }
}
