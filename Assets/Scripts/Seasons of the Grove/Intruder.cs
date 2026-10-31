using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// Intruder = sprite 2D yang SELALU menghadap kamera. Root (collider+logika) > Pivot (Billboard) > Sprite (DOTween).
public class Intruder : MonoBehaviour
{
    enum S { Seek, Act, Flee }
    public static readonly List<Intruder> Active = new();
    public bool Burner { get; private set; }

    S state; Cell target; float actT, actDur;
    Tween anim; Transform vis; SpriteRenderer bar; bool struggling; Vector3 baseScale;

    public static Intruder Spawn(Vector3 pos, bool burner, Season season)
    {
        var root = new GameObject(burner ? "Pembakar" : "Penebang");
        root.transform.position = pos;
        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, 1f, 0); col.height = 2.4f; col.radius = 1f;
        var i = root.AddComponent<Intruder>();
        i.Burner = burner; i.Build(season); i.SnapToGround();
        return i;
    }

    void Build(Season season)
    {
        var a = Game.Assets;
        var skins = Burner ? a.burnerSkins : a.cutterSkins;
        Sprite sp = (skins != null && skins.Length > 0) ? skins[Mathf.Min((int)season, skins.Length - 1)] : null;

        var piv = new GameObject("Pivot");
        piv.transform.SetParent(transform, false);
        piv.AddComponent<Billboard>().yAxisOnly = false;

        var s = new GameObject("Sprite");
        s.transform.SetParent(piv.transform, false);
        var sr = s.AddComponent<SpriteRenderer>();
        if (sp != null) sr.sprite = sp;
        else { sr.sprite = GreyboxSprites.Person; sr.color = Burner ? new Color(1f, 0.4f, 0.1f) : new Color(0.5f, 0.3f, 0.15f); }
        baseScale = Vector3.one * a.intruderScale;
        s.transform.localScale = baseScale;
        vis = s.transform;

        var b = new GameObject("HoldBar");
        b.transform.SetParent(piv.transform, false);
        b.transform.localPosition = new Vector3(0, 2.7f * a.intruderScale, 0);
        b.transform.localScale = new Vector3(0.01f, 0.15f, 1f);
        bar = b.AddComponent<SpriteRenderer>();
        bar.sprite = GreyboxSprites.White; bar.sortingOrder = 5;
        b.SetActive(false);
    }

    void OnEnable() => Active.Add(this);
    void OnDestroy() { anim?.Kill(); Release(); Active.Remove(this); }

    void SnapToGround()
    {
        var p = transform.position;
        p.y = Mathf.Max(Game.Grid.SampleY(p), Game.WaterY);
        transform.position = p;
    }

    void Update()
    {
        float dt = Game.Clock.dt; var cfg = Game.Cfg;
        if (state == S.Seek)
        {
            if (target == null || !target.IsHealthyTree)
            {
                Release(); target = FindTarget();
                if (target == null) { BeginFlee(); return; }
                target.claimedBy = this;
            }
            var to = target.pos - transform.position; to.y = 0;
            float d = to.magnitude;
            if (d < Game.Grid.HexSize * 0.6f)
            {
                state = S.Act; actT = 0; actDur = Random.Range(cfg.actMin, cfg.actMax);   // 3-4 detik (GDD 7.2)
                StartActAnim();
            }
            else { transform.position += to / d * cfg.intruderSpeed * dt; SnapToGround(); }
        }
        else if (state == S.Act)
        {
            if (target == null || !target.IsHealthyTree) { Release(); state = S.Seek; StopAnim(); return; }
            actT += dt;
            if (actT >= actDur)
            {
                if (Burner) Game.Grid.Ignite(target); else Game.Grid.Kill(target, KillReason.Chopped);
                BeginFlee();
            }
        }
        else
        {
            var dir = transform.position - Game.Grid.Center; dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.right;
            transform.position += dir.normalized * cfg.intruderSpeed * 1.4f * dt;
            SnapToGround();
            if (dir.magnitude > Game.Grid.Radius + cfg.spawnMargin) Destroy(gameObject);
        }
    }

    Cell FindTarget()
    {
        Cell best = null; float bd = float.MaxValue;
        foreach (var c in Game.Grid.All)
        {
            if (!c.IsHealthyTree || c.claimedBy != null) continue;
            float d = (c.pos - transform.position).sqrMagnitude;
            if (d < bd) { bd = d; best = c; }
        }
        return best;
    }

    void Release()
    {
        if (target != null && target.claimedBy == this) target.claimedBy = null;
        target = null;
    }

    void BeginFlee()
    {
        Release(); state = S.Flee; StopAnim(); struggling = false;
        if (bar != null) bar.gameObject.SetActive(false);
    }

    void StopAnim()
    {
        anim?.Kill(); anim = null;
        if (vis != null) { vis.localRotation = Quaternion.identity; vis.localScale = baseScale; }
    }

    void StartActAnim()
    {
        StopAnim();
        anim = vis.DOPunchScale(new Vector3(0.15f, -0.15f, 0.15f) * baseScale.x, 0.6f, 6, 0.5f).SetLoops(-1);
    }

    public void SetHoldProgress(float p)
    {
        if (state == S.Flee) return;
        bar.gameObject.SetActive(p > 0f);
        bar.transform.localScale = new Vector3(Mathf.Max(0.01f, p * 1.4f), 0.15f, 1f);
        if (p > 0f && !struggling)
        {
            struggling = true; StopAnim();
            anim = vis.DOShakeRotation(0.4f, new Vector3(0, 0, 25f), 25).SetLoops(-1);
        }
        else if (p <= 0f && struggling)
        {
            struggling = false; StopAnim();
            if (state == S.Act) StartActAnim();
        }
    }

    public void Repel()
    {
        if (state == S.Flee) return;
        BeginFlee();
        GameEvents.Sound("ok");
        Game.Toast("Intruder diusir");
    }
}
