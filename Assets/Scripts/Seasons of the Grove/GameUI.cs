using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// UI IMGUI tanpa Canvas/prefab: menu utama, HUD, peta mini, jeda, pengaturan, cara bermain, mode foto.
/// Pola helper (Panel/Btn/Bar/Txt, daftar rect untuk memblokir brush, skala u, PlayerPrefs) dari SeasonsOfTheGrove.
public class GameUI : MonoBehaviour
{
    public static GameUI I;
    bool pauseMenu, how, settings, showMap = true;
    int shadowLevel = 2, aaLevel = 2, nKotor, nClean, nTree;
    float cntT;
    Sfx A;
    Texture2D logo, tPanel, tEdge, tBtn, tHov, tOn, tBar, tWhite, tDim, tVig, tMap;
    Color32[] mapPx;
    GUIStyle st;
    List<Rect> rects = new List<Rect>(), next = new List<Rect>();
    float u = 1f;
    static readonly string[] SHADOW = { "Mati", "Rendah", "Tinggi" };
    static readonly Color32[] MAPC =
    {
        new Color32(89, 64, 46, 255), new Color32(204, 179, 128, 255), new Color32(191, 235, 128, 255), new Color32(128, 217, 102, 255),
        new Color32(56, 166, 71, 255), new Color32(20, 107, 46, 255), new Color32(255, 90, 25, 255)
    };
    static readonly Color32 WATER = new Color32(40, 100, 140, 255);

    static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        t.SetPixel(0, 0, c); t.Apply(); t.hideFlags = HideFlags.HideAndDontSave; return t;
    }

    public void Setup(Sfx a)
    {
        I = this; A = a;
        logo = Game.Assets.logo != null ? Game.Assets.logo : Resources.Load<Texture2D>("Logo");
        tPanel = Solid(new Color(.29f, .19f, .11f, .86f)); tEdge = Solid(new Color(.16f, .1f, .06f, .95f));
        tBtn = Solid(new Color(.45f, .31f, .18f, .95f)); tHov = Solid(new Color(.58f, .41f, .24f, .98f)); tOn = Solid(new Color(.3f, .55f, .24f, .98f));
        tBar = Solid(new Color(.1f, .06f, .03f, .9f)); tWhite = Solid(Color.white); tDim = Solid(new Color(.05f, .09f, .06f, .72f));
        tVig = GreyboxSprites.RadialTex(128, new Color(.16f, .08f, .03f), 0f, .6f, 2.6f);
        var cfg = Game.Cfg;
        tMap = new Texture2D(cfg.gridW * 2 + 1, cfg.gridH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        mapPx = new Color32[tMap.width * tMap.height];
        showMap = PlayerPrefs.GetInt("grove_map", 1) == 1;
        shadowLevel = PlayerPrefs.GetInt("grove_shadow", 2); aaLevel = PlayerPrefs.GetInt("grove_aa", 2);
        A.mute = PlayerPrefs.GetInt("grove_mute", 0) == 1;
        ApplyQuality();
        Game.PointerOverUI = Over;
        Game.Menu = cfg.startInMenu;
    }

    void ApplyQuality()
    {
        QualitySettings.shadows = shadowLevel == 0 ? ShadowQuality.Disable : shadowLevel == 1 ? ShadowQuality.HardOnly : ShadowQuality.All;
        QualitySettings.antiAliasing = aaLevel == 0 ? 0 : aaLevel == 1 ? 2 : 4;   // (URP: atur di URP Asset)
    }
    void SaveP()
    {
        PlayerPrefs.SetInt("grove_map", showMap ? 1 : 0); PlayerPrefs.SetInt("grove_shadow", shadowLevel);
        PlayerPrefs.SetInt("grove_aa", aaLevel); PlayerPrefs.SetInt("grove_mute", A.mute ? 1 : 0); PlayerPrefs.Save();
    }

    bool Blocking => Game.Menu || pauseMenu || how || settings;
    bool Over(Vector2 mp)
    {
        if (Blocking) return true;
        var g = new Vector2(mp.x, Screen.height - mp.y);
        foreach (var r in rects) if (r.Contains(g)) return true;
        return false;
    }

    void StartGame(bool fresh)
    {
        if (fresh || !GameFlow.Started) { GameFlow.NewGame(); Game.Toast("Sapu tanah kotor, lalu tanam benih"); }
        Game.Menu = false; Game.Paused = false; pauseMenu = false;
    }
    void ToMenu() { Game.Menu = true; pauseMenu = false; Game.Photo = false; Game.Paused = false; }
    void OnEsc()
    {
        if (how || settings) { how = settings = false; return; }
        if (Game.Photo) { Game.Photo = false; return; }
        if (!Game.Menu) { pauseMenu = !pauseMenu; Game.Paused = pauseMenu; }
    }

    // ---------- per frame ----------
    void Update()
    {
        Game.Modal = Blocking;
        if (Game.ToastT > 0f) Game.ToastT -= Time.unscaledDeltaTime;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.escapeKey.wasPressedThisFrame) OnEsc();
            if (kb.hKey.wasPressedThisFrame && !Game.Menu) Game.Photo = !Game.Photo;
        }
        cntT -= Time.unscaledDeltaTime;
        if (cntT <= 0f) { cntT = .25f; Count(); if (showMap) UpdateMap(); }
    }

    void Count()
    {
        nKotor = nClean = nTree = 0;
        foreach (var c in Game.Grid.All)
        {
            if (!c.buildable) continue;
            if (c.state == TileState.Kotor) nKotor++; else if (c.state == TileState.Bersih) nClean++; else nTree++;
        }
    }

    void UpdateMap()
    {
        for (int i = 0; i < mapPx.Length; i++) mapPx[i] = WATER;
        int w = tMap.width;
        foreach (var c in Game.Grid.All)
        {
            if (!c.buildable) continue;
            int row = c.coord.y, col = c.coord.x + (row - (row & 1)) / 2;
            int px = 2 * col + (row & 1);
            var color = MAPC[(int)c.state];
            mapPx[row * w + px] = color; mapPx[row * w + px + 1] = color;
        }
        tMap.SetPixels32(mapPx); tMap.Apply(false);
    }

    string Hint()
    {
        if (Game.ToastT > 0f) return Game.ToastText;
        var g = Game.Grid; var cfg = Game.Cfg;
        if (nClean == 0 && nTree == 0 && nKotor > 0) return "Pilih Sapu (3) lalu usap tanah kotor";
        if (nClean > 0 && nTree == 0) return "Pilih Benih (1) lalu usap tanah bersih";
        if (g.DewasaCount < cfg.minDewasaToSpawn) return "Beri jarak antar benih agar api tidak merambat. Tunggu pohon tumbuh";
        if (g.Density >= cfg.densityThreshold) return "Hutan rapat: pengganggu berhenti datang";
        return "";
    }

    // ---------- helper gambar ----------
    void Reg(Rect r) { if (Event.current.type == EventType.Repaint) next.Add(r); }
    void Box(Rect r, Texture2D fill, float bw)
    {
        GUI.DrawTexture(r, tEdge);
        GUI.DrawTexture(new Rect(r.x + bw, r.y + bw, r.width - 2 * bw, r.height - 2 * bw), fill);
    }
    void Panel(Rect r) { Reg(r); Box(r, tPanel, 2.5f * u); }
    void Txt(Rect r, string s, float size, Color c, TextAnchor a = TextAnchor.MiddleLeft, bool bold = false)
    {
        st.fontSize = Mathf.RoundToInt(size); st.alignment = a; st.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        st.normal.textColor = new Color(0, 0, 0, .5f); GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), s, st);
        st.normal.textColor = c; GUI.Label(r, s, st);
    }
    bool Btn(Rect r, string s, bool on = false, float size = 15f)
    {
        Reg(r); bool hov = r.Contains(Event.current.mousePosition);
        Box(r, on ? tOn : hov ? tHov : tBtn, 2f * u);
        bool c = GUI.Button(r, GUIContent.none, GUIStyle.none);
        Txt(r, s, size * u, Color.white, TextAnchor.MiddleCenter, true);
        return c;
    }
    void Bar(Rect r, float f, Color c)
    {
        GUI.DrawTexture(r, tBar);
        GUI.color = c; GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(f), r.height), tWhite); GUI.color = Color.white;
    }
    void Init()
    {
        if (st != null) return;
        st = new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Overflow, richText = false };
        try { var f = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Trebuchet MS", "Arial", "Helvetica", "Verdana" }, 24); if (f) st.font = f; } catch { }
    }

    void OnGUI()
    {
        if (Game.Grid == null || A == null) return;
        Init(); u = Mathf.Max(.7f, Screen.height / 800f);
        if (Event.current.type == EventType.Repaint) next.Clear();
        if (Game.Menu) DrawMenu(); else DrawGame();
        DrawModals();
        if (Event.current.type == EventType.Repaint) { var t = rects; rects = next; next = t; }
    }

    // ---------- menu utama ----------
    void DrawMenu()
    {
        float w0 = Screen.width, h0 = Screen.height;
        GUI.DrawTexture(new Rect(0, 0, w0, h0), tVig);
        float lw = Mathf.Min(w0 * .4f, 440f * u), lh = logo ? lw * logo.height / logo.width : lw * .5f;
        var lr = new Rect(w0 * .07f, h0 * .06f, lw, lh);
        if (logo) GUI.DrawTexture(lr, logo, ScaleMode.ScaleToFit, true);
        else Txt(lr, "Penanaman Hutan", 46 * u, Color.white, TextAnchor.MiddleCenter, true);
        float cx = lr.x + lw / 2f, y = lr.yMax + h0 * .05f, iw = 380f * u, ih = 58f * u;
        var items = new List<string>();
        if (GameFlow.Started) items.Add("LANJUTKAN");
        items.Add(GameFlow.Started ? "BARU" : "MULAI"); items.Add("CARA BERMAIN"); items.Add("PENGATURAN");
        foreach (var it in items)
        {
            var r = new Rect(cx - iw / 2f, y, iw, ih); Reg(r); bool hov = r.Contains(Event.current.mousePosition);
            Txt(r, it, (hov ? 42f : 38f) * u, Color.white, TextAnchor.MiddleCenter, true);
            if (hov) GUI.DrawTexture(new Rect(cx - 90f * u, r.yMax - 6f * u, 180f * u, 3f * u), tWhite);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                if (it == "LANJUTKAN") StartGame(false);
                else if (it == "BARU" || it == "MULAI") StartGame(true);
                else if (it == "CARA BERMAIN") how = true; else settings = true;
            }
            y += ih + 6f * u;
        }
    }

    // ---------- HUD ----------
    void DrawGame()
    {
        float w0 = Screen.width, h0 = Screen.height;
        var g = Game.Grid; var cfg = Game.Cfg; var clk = Game.Clock; int se = (int)clk.CurrentSeason;
        GUI.DrawTexture(new Rect(0, 0, w0, h0), tVig);
        if (Game.Photo)
        {
            if (Btn(new Rect(w0 - 190 * u, 10 * u, 180 * u, 36 * u), "Keluar mode foto")) Game.Photo = false;
            return;
        }

        // kiri atas: hari + musim
        var sc = new Rect(10 * u, 10 * u, 270 * u, 70 * u); Panel(sc);
        Txt(new Rect(sc.x + 10 * u, sc.y + 4 * u, 170 * u, 26 * u), "Hari " + clk.Day + " | " + clk.CurrentSeason, 19 * u, Color.white, TextAnchor.MiddleLeft, true);
        Txt(new Rect(sc.xMax - 90 * u, sc.y + 4 * u, 80 * u, 26 * u), Game.Paused ? "JEDA" : clk.EffectiveSpeed + "x", 15 * u, Color.white, TextAnchor.MiddleRight);
        Bar(new Rect(sc.x + 10 * u, sc.y + 32 * u, sc.width - 20 * u, 9 * u), clk.SeasonProgress, Palette.Accent[se]);
        Txt(new Rect(sc.x + 10 * u, sc.y + 44 * u, sc.width - 14 * u, 22 * u), "Pengganggu musim ini: " + Game.IntruderName(clk.CurrentSeason), 12 * u, Color.white);

        // kanan atas: kerapatan hutan + penanda ambang
        var st2 = new Rect(w0 - 280 * u, 10 * u, 270 * u, 84 * u); Panel(st2);
        Txt(new Rect(st2.x + 10 * u, st2.y + 4 * u, 170 * u, 24 * u), "Kerapatan hutan", 15 * u, Color.white);
        Txt(new Rect(st2.xMax - 90 * u, st2.y + 4 * u, 80 * u, 24 * u), Mathf.RoundToInt(g.Density * 100f) + "%", 16 * u, Color.white, TextAnchor.MiddleRight, true);
        var br = new Rect(st2.x + 10 * u, st2.y + 30 * u, st2.width - 20 * u, 10 * u);
        Bar(br, g.Density, g.Density >= cfg.densityThreshold ? new Color(.4f, .95f, .5f) : Palette.Accent[se]);
        GUI.DrawTexture(new Rect(br.x + br.width * cfg.densityThreshold - 1f, br.y - 3 * u, 2f, br.height + 6 * u), tWhite);
        Txt(new Rect(st2.x + 10 * u, st2.y + 46 * u, st2.width - 14 * u, 30 * u),
            "Dewasa " + g.DewasaCount + "  Terbakar " + g.BurningCount + "  Intruder " + Intruder.Active.Count, 13 * u, Color.white);

        // tombol waktu & alat
        float bx = w0 - 280 * u, by = 102 * u, bw = 43 * u, bh = 32 * u, gp = 4 * u;
        if (Btn(new Rect(bx, by, bw, bh), Game.Paused ? ">" : "II", Game.Paused, 13)) Game.Paused = !Game.Paused;
        if (Btn(new Rect(bx + (bw + gp), by, bw, bh), "1x", clk.speed == 1, 13)) clk.speed = 1;
        if (Btn(new Rect(bx + 2 * (bw + gp), by, bw, bh), "2x", clk.speed == 2, 13)) clk.speed = 2;
        if (Btn(new Rect(bx + 3 * (bw + gp), by, bw, bh), "Foto", false, 11)) Game.Photo = true;
        if (Btn(new Rect(bx + 4 * (bw + gp), by, bw, bh), "Peta", showMap, 11)) { showMap = !showMap; SaveP(); }
        if (Btn(new Rect(bx + 5 * (bw + gp), by, bw, bh), A.mute ? "OFF" : "SND", false, 11)) { A.mute = !A.mute; SaveP(); }

        // bawah: hotbar + pesan
        string[] bn = { "Benih (1)", "Hujan (2)", "Sapu (3)" };
        float hw = 120 * u, hh = 52 * u, hx = (w0 - 3 * hw - 2 * 6 * u) / 2f, hy = h0 - hh - 14 * u;
        Panel(new Rect(hx - 8 * u, hy - 8 * u, 3 * hw + 2 * 6 * u + 16 * u, hh + 16 * u));
        for (int i = 0; i < 3; i++) if (Btn(new Rect(hx + i * (hw + 6 * u), hy, hw, hh), bn[i], (int)Game.Brush == i, 16)) Game.Brush = (BrushType)i;
        string msg = Hint();
        if (msg != "")
        {
            var mr = new Rect(w0 / 2f - 330 * u, hy - 52 * u, 660 * u, 36 * u); Reg(mr);
            GUI.DrawTexture(mr, tDim); Txt(mr, msg, 15 * u, Color.white, TextAnchor.MiddleCenter);
        }
        if (cfg.autoSlow && clk.speed > 1 && clk.EffectiveSpeed < clk.speed)
            Txt(new Rect(12 * u, h0 - 40 * u, 520 * u, 28 * u), "Dilambatkan ke 1x (ada api atau intruder)", 14 * u, Color.white);

        if (showMap) DrawMap(w0, h0);
    }

    Rect Dot(Rect mr, Vector3 world, float sz)
    {
        var uv = Game.Grid.WorldToUV(world);
        uv.x = Mathf.Clamp01(uv.x); uv.y = Mathf.Clamp01(uv.y);
        return new Rect(mr.x + uv.x * mr.width - sz / 2f, mr.yMax - uv.y * mr.height - sz / 2f, sz, sz);
    }

    void DrawMap(float w0, float h0)
    {
        float ms = 170f * u, mh = ms * Game.Grid.MapAspect;
        var mr = new Rect(w0 - ms - 14 * u, h0 - mh - 14 * u, ms, mh);
        Panel(new Rect(mr.x - 4 * u, mr.y - 4 * u, ms + 8 * u, mh + 8 * u));
        GUI.DrawTexture(mr, tMap, ScaleMode.StretchToFill);
        GUI.color = new Color(1f, .2f, .2f);
        foreach (var o in Intruder.Active) if (o != null) GUI.DrawTexture(Dot(mr, o.transform.position, 6 * u), tWhite);
        var im = IntruderManager.Instance;
        if (im != null && im.Warned && Mathf.Sin(Time.unscaledTime * 8f) > 0f) { GUI.color = new Color(1f, .9f, .2f); GUI.DrawTexture(Dot(mr, im.WarnPos, 9 * u), tWhite); }
        GUI.color = new Color(1f, .83f, .3f);
        var cr = Dot(mr, Game.CamFocus, 16 * u);
        GUI.DrawTexture(new Rect(cr.x, cr.y, cr.width, 2), tWhite); GUI.DrawTexture(new Rect(cr.x, cr.yMax - 2, cr.width, 2), tWhite);
        GUI.DrawTexture(new Rect(cr.x, cr.y, 2, cr.height), tWhite); GUI.DrawTexture(new Rect(cr.xMax - 2, cr.y, 2, cr.height), tWhite);
        GUI.color = Color.white;

        var ev = Event.current;
        if (ev.type == EventType.MouseDown && ev.button == 0 && mr.Contains(ev.mousePosition))
        {
            var uv = new Vector2((ev.mousePosition.x - mr.x) / mr.width, (mr.yMax - ev.mousePosition.y) / mr.height);
            if (CameraRig.Instance != null) CameraRig.Instance.GoTo(Game.Grid.UVToWorld(uv));
            ev.Use();
        }
    }

    // ---------- modal ----------
    void Modal(string title, string body, System.Action extra = null)
    {
        float w0 = Screen.width, h0 = Screen.height; GUI.DrawTexture(new Rect(0, 0, w0, h0), tDim);
        float pw = Mathf.Min(640f * u, w0 - 30), ph = Mathf.Min(560f * u, h0 - 30);
        var r = new Rect((w0 - pw) / 2f, (h0 - ph) / 2f, pw, ph); Panel(r);
        Txt(new Rect(r.x + 18 * u, r.y + 10 * u, pw - 36 * u, 34 * u), title, 24 * u, Color.white, TextAnchor.MiddleLeft, true);
        st.wordWrap = true;
        Txt(new Rect(r.x + 18 * u, r.y + 52 * u, pw - 36 * u, ph - 130 * u), body, 15 * u, Color.white, TextAnchor.UpperLeft);
        st.wordWrap = false;
        if (extra != null) extra();
        if (Btn(new Rect(r.center.x - 70 * u, r.yMax - 54 * u, 140 * u, 40 * u), "Tutup", false, 16)) { how = settings = false; }
    }

    void DrawModals()
    {
        float w0 = Screen.width, h0 = Screen.height;
        if (pauseMenu && !Game.Menu && !how && !settings)
        {
            GUI.DrawTexture(new Rect(0, 0, w0, h0), tDim);
            var r = new Rect(w0 / 2 - 170 * u, h0 / 2 - 170 * u, 340 * u, 340 * u); Panel(r);
            Txt(new Rect(r.x, r.y + 10 * u, r.width, 40 * u), "Jeda", 26 * u, Color.white, TextAnchor.MiddleCenter, true);
            float y = r.y + 62 * u;
            if (Btn(new Rect(r.x + 30 * u, y, r.width - 60 * u, 48 * u), "Lanjut", false, 17)) { pauseMenu = false; Game.Paused = false; }
            if (Btn(new Rect(r.x + 30 * u, y + 56 * u, r.width - 60 * u, 48 * u), "Cara Bermain", false, 17)) how = true;
            if (Btn(new Rect(r.x + 30 * u, y + 112 * u, r.width - 60 * u, 48 * u), "Pengaturan", false, 17)) settings = true;
            if (Btn(new Rect(r.x + 30 * u, y + 168 * u, r.width - 60 * u, 48 * u), "Menu Utama", false, 17)) ToMenu();
        }
        if (how) Modal("Cara Bermain",
            "TUJUAN\nTidak ada menang atau kalah. Rawat hutanmu; pohon yang tua gugur setelah 4 musim, jadi tanam ulang terus. " +
            "Saat kerapatan hutan mencapai 80%, pengganggu berhenti datang.\n\n" +
            "ALAT (1-3)\n1 Benih: tanam di tanah bersih. Beri jarak, api merambat ke tetangga (benih kebal api).\n" +
            "2 Hujan: usap pohon terbakar sampai bar biru habis. Di pohon sehat, hujan membuatnya basah dan kebal api sementara.\n" +
            "3 Sapu: usap tanah kotor sampai bersih (sisa kebakaran dan tunggul juga).\n\n" +
            "PENGGANGGU\nPlain dan Autumn: Pembakar menyulut. Spring dan Winter: Penebang menebang. Klik-tahan intruder 2,5 detik untuk mengusir.\n\n" +
            "KAMERA\nWASD geser, klik kanan + geser atau Q/E putar, roda zoom ke kursor, klik tengah + geser, Spasi kembali ke tengah pulau. Klik peta mini untuk pindah.\n" +
            "Z/X kecepatan 1x/2x, P jeda, G tampil/sembunyi grid, H mode foto, Esc jeda.");
        if (settings) Modal("Pengaturan", "", () =>
        {
            float pw = Mathf.Min(640f * u, w0 - 30), ph = Mathf.Min(560f * u, h0 - 30);
            float x = (w0 - pw) / 2f + 40 * u, y = (h0 - ph) / 2f + 70 * u, bw = 200 * u, bh = 40 * u;
            Txt(new Rect(x, y, 200 * u, bh), "Suara", 17 * u, Color.white);
            if (Btn(new Rect(x + 260 * u, y, bw, bh), A.mute ? "Mati" : "Nyala")) { A.mute = !A.mute; SaveP(); }
            y += 54 * u; Txt(new Rect(x, y, 200 * u, bh), "Bayangan", 17 * u, Color.white);
            if (Btn(new Rect(x + 260 * u, y, bw, bh), SHADOW[shadowLevel])) { shadowLevel = (shadowLevel + 1) % 3; ApplyQuality(); SaveP(); }
            y += 54 * u; Txt(new Rect(x, y, 200 * u, bh), "Anti-aliasing", 17 * u, Color.white);
            if (Btn(new Rect(x + 260 * u, y, bw, bh), aaLevel == 0 ? "Mati" : aaLevel == 1 ? "2x" : "4x")) { aaLevel = (aaLevel + 1) % 3; ApplyQuality(); SaveP(); }
            y += 54 * u; Txt(new Rect(x, y, 200 * u, bh), "Peta mini", 17 * u, Color.white);
            if (Btn(new Rect(x + 260 * u, y, bw, bh), showMap ? "Tampil" : "Sembunyi")) { showMap = !showMap; SaveP(); }
            y += 54 * u; Txt(new Rect(x, y, 200 * u, bh), "Layar penuh", 17 * u, Color.white);
            if (Btn(new Rect(x + 260 * u, y, bw, bh), Screen.fullScreen ? "Nyala" : "Mati")) Screen.fullScreen = !Screen.fullScreen;
        });
    }
}
