using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// A small portrait stage shared by the tablet feed and the full-screen attack.
internal sealed class ParkScareView
{
    private const int Layer = 29;
    private readonly GameObject stage;
    private readonly Transform pivot;
    private readonly RenderTexture picture;
    private readonly Camera lens;
    private readonly ParkMascot kind;
    private readonly double started;
    private readonly float duration;
    internal bool Active => Time.unscaledTimeAsDouble - started < duration;

    internal ParkScareView(ParkRunController player, ParkMascot kind) : this(player.CreateMascot(kind), kind) { }

    internal ParkScareView(Transform actor, ParkMascot kind)
    {
        this.kind = kind;
        duration = 1.35f;
        started = Time.unscaledTimeAsDouble;
        stage = new GameObject("Скример " + kind);
        stage.transform.position = new Vector3(0, -600, 0);
        lens = stage.AddComponent<Camera>();
        lens.cullingMask = 1 << Layer;
        lens.fieldOfView = 42;
        lens.nearClipPlane = .04f;
        lens.farClipPlane = 20;
        var hd = stage.AddComponent<HDAdditionalCameraData>();
        hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        hd.backgroundColorHDR = new Color(.012f,.006f,.009f);
        picture = new RenderTexture(640,360,24) { name = "Портрет аниматроника" };
        lens.targetTexture = picture;
        pivot = new GameObject("Поворот головы").transform;
        pivot.SetParent(stage.transform, false);
        Transform face = actor.Find(kind == ParkMascot.Terry ? "Морда" : "Голова");
        actor.SetParent(pivot,false);
        actor.localRotation = Quaternion.Euler(0,180,0);
        float scale = 1f / face.localScale.y;
        actor.localScale = Vector3.one * scale;
        actor.localPosition = -(actor.localRotation * (face.localPosition * scale));
        foreach (Transform part in actor) part.gameObject.layer = Layer;

        var lamp = new GameObject("Свет скримера");
        lamp.transform.SetParent(stage.transform,false);
        lamp.transform.localPosition = new Vector3(-1.35f,1.1f,-.5f);
        var light = lamp.AddComponent<Light>();
        light.type = LightType.Point;
        lamp.AddComponent<HDAdditionalLightData>();
        light.lightUnit = LightUnit.Candela;
        light.intensity = 1600;
        light.range = 12;
        light.color = new Color(1,.83f,.71f);
        Animate();
    }

    internal void Animate()
    {
        float t = Mathf.Clamp01((float)(Time.unscaledTimeAsDouble - started) / duration);
        float approach = Mathf.SmoothStep(0,1,Mathf.Clamp01(t * 2.4f));
        float remaining = 1 - approach;
        float x = 0, y = 0, roll = 0, yaw = 0;
        switch (kind)
        {
            case ParkMascot.Terry: yaw = 22 * remaining; break;
            case ParkMascot.Umi: y = -2 * remaining + .32f * Mathf.Sin(approach * Mathf.PI); break;
            case ParkMascot.Kesha: x = 1.7f * remaining; roll = 24 * remaining + 8 * Mathf.Sin(14 * t); break;
            case ParkMascot.Falcon: y = 1.8f * remaining; x = -1.35f * remaining; roll = -30 * remaining; break;
            case ParkMascot.Wolf: x = -2 * remaining; yaw = -35 * remaining; break;
            case ParkMascot.Elephant: y = -.6f * remaining; roll = 5 * Mathf.Sin(9*t); break;
        }
        pivot.localPosition = new Vector3(x,y,Mathf.Lerp(4.8f,1.72f,approach) + .06f*Mathf.Sin(22*t));
        pivot.localRotation = Quaternion.Euler(0,yaw,roll);
    }

    internal void Draw(Rect area) => GUI.DrawTexture(area,picture,ScaleMode.ScaleAndCrop,false);

    internal void Dispose()
    {
        lens.enabled = false;
        lens.targetTexture = null;
        Object.Destroy(stage);
        picture.Release();
        Object.Destroy(picture);
    }
}
