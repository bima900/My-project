using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;
using UnityEngine.InputSystem;

/// Pasang di GameObject kosong. Urutan: event bus -> jam -> Terrain+air -> grid -> sistem -> audio/efek/lingkungan -> kamera -> UI.
[DefaultExecutionOrder(-200)]
public class GreyboxBootstrap : MonoBehaviour
{
    public GameConfig config = new GameConfig();
    public AssetRefs assets = new AssetRefs();

    void Awake()
    {
        GameEvents.Reset();
        Game.Cfg = config; Game.Assets = assets; Game.Brush = BrushType.Benih;
        Game.Menu = false; Game.Paused = false; Game.Photo = false; Game.Modal = false; Game.ToastT = 0f;
        GameFlow.Started = false;
        Intruder.Active.Clear();

        Game.Clock = gameObject.AddComponent<GameClock>();
        var sfx = gameObject.AddComponent<Sfx>();
        gameObject.AddComponent<FxSystem>();
        IslandBuilder.Prepare();
        Game.Grid = gameObject.AddComponent<HexGrid>();
        gameObject.AddComponent<TreeSystem>();
        gameObject.AddComponent<FireSystem>();
        gameObject.AddComponent<IntruderManager>();

        var cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.7f, 0.85f);
        if (!cam.TryGetComponent<CinemachineBrain>(out _)) cam.gameObject.AddComponent<CinemachineBrain>();

        var target = new GameObject("CameraTarget");
        target.transform.position = Game.Grid.Center;
        var vcam = new GameObject("CM Camera").AddComponent<CinemachineCamera>();
        vcam.Follow = target.transform; vcam.LookAt = target.transform;
        var lens = vcam.Lens; lens.NearClipPlane = 0.3f; lens.FarClipPlane = 2000f; vcam.Lens = lens;
        var follow = vcam.gameObject.AddComponent<CinemachineFollow>();
        var ts = follow.TrackerSettings;
        ts.BindingMode = BindingMode.WorldSpace;
        ts.PositionDamping = Vector3.one * 0.25f;
        follow.TrackerSettings = ts;
        vcam.gameObject.AddComponent<CinemachineRotationComposer>();
        target.AddComponent<CameraRig>().Init(follow);

        gameObject.AddComponent<EnvironmentController>();   // Start-nya memakai kamera & grid
        gameObject.AddComponent<BrushController>();
        gameObject.AddComponent<GameUI>().Setup(sfx);        // menyetel Game.Menu sesuai config.startInMenu
        if (!config.startInMenu) GameFlow.NewGame();
    }

#if UNITY_EDITOR || DEBUG
    // Tombol debug
    void Update()
    {
        var kb = Keyboard.current; if (kb == null || Game.Menu) return;
        var g = Game.Grid; var cfg = Game.Cfg;

        if (kb.f1Key.wasPressedThisFrame)   // isi ~60% hutan dewasa
        {
            float life = cfg.lifespanSeasons * cfg.daysPerSeason * cfg.dayLength;
            foreach (var c in g.All)
            {
                if (!c.buildable) continue;
                if (Random.value < 0.6f)
                { c.state = TileState.Dewasa; c.age = Random.value * life * 0.5f; c.stageTimer = 0; c.hp = cfg.treeHP; }
                else c.state = TileState.Bersih;
                g.Refresh(c);
            }
        }
        if (kb.f2Key.wasPressedThisFrame) IntruderManager.SpawnOne();
        if (kb.f3Key.wasPressedThisFrame)
        {
            var list = g.All.FindAll(c => c.IsHealthyTree);
            if (list.Count > 0) g.Ignite(list[Random.Range(0, list.Count)]);
        }
        if (kb.f4Key.wasPressedThisFrame) foreach (var c in g.All) if (c.buildable) g.Kill(c, KillReason.Reset);
    }
#endif
}
