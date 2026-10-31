using UnityEngine;

/// Membuat sprite selalu menghadap kamera.
/// yAxisOnly = true  -> hanya berputar di sumbu Y (pohon tetap tegak, dipakai LOD2 pohon)
/// yAxisOnly = false -> mengikuti rotasi kamera penuh (intruder: selalu terlihat apa pun sudut kameranya)
public class Billboard : MonoBehaviour
{
    public bool yAxisOnly;
    static Camera cam;

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        if (!yAxisOnly) { transform.rotation = cam.transform.rotation; return; }

        var f = cam.transform.forward; f.y = 0;
        if (f.sqrMagnitude < 1e-4f) { f = cam.transform.up; f.y = 0; }   // kamera menunduk lurus
        if (f.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.LookRotation(f);
    }
}
