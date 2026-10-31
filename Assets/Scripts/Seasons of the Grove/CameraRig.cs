using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// Kamera orbit bebas yang "diikat" ke pulau.
///   RMB drag / Q,E = putar & miring | scroll = zoom KE KURSOR | WASD / MMB drag = geser (dibatasi radius pulau)
///   Spasi / C = kembali ke tengah pulau | menu utama = orbit pelan otomatis.
/// Yaw/pitch/jarak dihaluskan secara eksponensial (pola CamRig prototipe), posisi dihaluskan Cinemachine.
public class CameraRig : MonoBehaviour
{
    public static CameraRig Instance;
    CinemachineFollow follow; Camera cam;
    float yaw = 20f, pitch, dist, yawT, pitchT, distT, minDist, maxDist, panRadius;
    Vector3 focus;
    Tween tw;

    public void Init(CinemachineFollow f)
    {
        Instance = this; follow = f; cam = Camera.main;
        var cfg = Game.Cfg; float R = Game.Grid.Radius;
        focus = Game.Grid.Center;
        panRadius = R * cfg.panLimit;
        distT = dist = R * cfg.startDistRatio; minDist = R * cfg.minDistRatio; maxDist = R * cfg.maxDistRatio;
        pitchT = pitch = cfg.startPitch; yawT = yaw;
        transform.position = focus;
        ApplyOffset();
    }

    void ApplyOffset() { follow.FollowOffset = Quaternion.Euler(pitch, yaw, 0) * Vector3.back * dist; }

    public void GoTo(Vector3 p)
    {
        tw?.Kill();
        p.y = transform.position.y;
        tw = transform.DOMove(p, 0.6f).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    void Update()
    {
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (kb == null || mouse == null || follow == null) return;
        float dt = Time.unscaledDeltaTime;
        var pos = transform.position;

        if (Game.Menu)
        {
            yawT += dt * 3.4f; pitchT = 28f; distT = Game.Grid.Radius * 3f;
            tw?.Kill();
            pos = Vector3.Lerp(pos, focus, dt * 2f);
        }
        else if (!Game.Modal)
        {
            Vector2 mp = mouse.position.ReadValue(), dlt = mouse.delta.ReadValue();
            bool over = Game.PointerOver(mp);

            if (mouse.rightButton.isPressed) { yawT += dlt.x * 0.2f; pitchT = Mathf.Clamp(pitchT - dlt.y * 0.2f, 20f, 85f); }
            if (kb.qKey.isPressed) yawT -= 90f * dt;
            if (kb.eKey.isPressed) yawT += 90f * dt;

            var yq = Quaternion.Euler(0, yaw, 0);
            Vector3 fwd = yq * Vector3.forward, right = yq * Vector3.right, mv = Vector3.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) mv += fwd;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) mv -= fwd;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) mv += right;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) mv -= right;
            if (mv.sqrMagnitude > 0f) { tw?.Kill(); pos += mv.normalized * dist * Game.Cfg.panSpeedPerDist * dt; }
            if (mouse.middleButton.isPressed) { tw?.Kill(); pos -= (right * dlt.x + fwd * dlt.y) * dist * 0.002f; }

            float sc = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(sc) >= 10f) sc /= 120f;
            sc = Mathf.Clamp(sc, -3f, 3f);
            if (!over && Mathf.Abs(sc) > 0.01f)
            {
                float old = distT; distT = Mathf.Clamp(old * Mathf.Pow(0.88f, sc), minDist, maxDist);
                float k = 1f - distT / old;   // zoom ke kursor: target bergeser ke titik di bawah kursor
                if (Pick.Ground(cam, mp, out var g)) { tw?.Kill(); pos.x += (g.x - pos.x) * k; pos.z += (g.z - pos.z) * k; }
            }
            if (kb.spaceKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame) GoTo(focus);
        }

        // tali pengikat ke tengah pulau + ikuti tinggi tanah
        var off = pos - focus; off.y = 0;
        off = Vector3.ClampMagnitude(off, panRadius);
        pos.x = focus.x + off.x; pos.z = focus.z + off.z;
        float gy = Mathf.Max(Game.Grid.SampleY(pos), Game.WaterY);
        pos.y = Mathf.Lerp(transform.position.y, gy, 6f * dt);
        transform.position = pos;
        Game.CamFocus = pos;

        float s = 1f - Mathf.Exp(-8f * dt);
        yaw += (yawT - yaw) * s; pitch += (pitchT - pitch) * s; dist += (distT - dist) * s;
        ApplyOffset();
    }
}
