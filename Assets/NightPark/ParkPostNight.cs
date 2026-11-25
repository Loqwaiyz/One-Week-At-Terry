using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// All checkpoint values belong to this segment, independently of a night's transient rules.
public sealed class ParkPostNight : MonoBehaviour
{
    private const string ProgressKey = "Terry.PostNight1.Progress.v1";
    public enum Step { ReachCamera, ConnectPower, TuneTransmitter, VerifyFeed, ReturnToEntrance, Complete }
    public Step CurrentStep { get; private set; }
    public bool Active { get; private set; }
    public bool Busy { get; private set; }
    public bool Finished => CurrentStep == Step.Complete;
    public bool CanTravel => Active && !Busy && !Finished;
    public static bool CameraConnected => PlayerPrefs.GetInt(ProgressKey, 0) >= 3;
    public static bool SegmentCompleted => PlayerPrefs.GetInt(ProgressKey, 0) >= 5;
    public static bool HasUnfinishedSegment => PlayerPrefs.GetInt(ProgressKey, 0) is >= 1 and < 5;

    private bool replay;
    private int replayCheckpoint;
    public bool SignalConnected => replay ? replayCheckpoint >= 3 : CameraConnected;

    private void RecordStep(int checkpoint)
    {
        if (replay) replayCheckpoint = checkpoint;
        else Save(checkpoint);
    }

    private ParkRunController player;
    private ParkNightShift night;
    private Transform device;
    private Renderer indicator;
    private Light equipmentLight;
    private Volume dawn;
    private float viewedFor;
    private float shotStarted, shotDuration;
    private string shotCaption;
    private int yard;
    private GUIStyle heading, body, caption, button;
    private static readonly Color Panel = new Color32(17, 29, 34, 245);
    private static readonly Color Accent = new Color32(115, 205, 187, 255);

    public static void MarkPending() => Save(1);
    public static void ResetProgress() => PlayerPrefs.DeleteKey(ProgressKey);
    private static void Save(int checkpoint)
    {
        PlayerPrefs.SetInt(ProgressKey, checkpoint);
        PlayerPrefs.Save();
    }

    public void Begin(ParkRunController controller, ParkNightShift shift, bool isReplay = false)
    {
        player = controller;
        night = shift;
        yard = player.YardPost;
        Active = true;
        replay = isReplay;
        replayCheckpoint = 1;
        int checkpoint = replay ? replayCheckpoint : PlayerPrefs.GetInt(ProgressKey, 1);
        CurrentStep = checkpoint >= 4 ? Step.ReturnToEntrance : checkpoint == 3 ? Step.VerifyFeed :
            checkpoint == 2 ? Step.TuneTransmitter : Step.ReachCamera;
        player.ObjectivePost = checkpoint >= 4 ? 0 : yard;
        CreateEquipment();
        // Runtime-only profile; the scene's night lighting and surveillance profile stay intact.
        dawn = new GameObject("Предрассветная экспозиция").AddComponent<Volume>();
        dawn.isGlobal = true;
        dawn.priority = 900;
        var exposure = dawn.profile.Add<Exposure>();
        exposure.mode.Override(ExposureMode.Fixed);
        exposure.fixedExposure.Override(3.5f);
        StartCoroutine(Intro());
    }

    private IEnumerator Intro()
    {
        Busy = true;
        shotCaption = "Рассвело. Нужно проверить последнюю камеру и выбираться.";
        shotStarted = Time.unscaledTime;
        shotDuration = 3.85f;
        Transform seat = player.PostSeat(player.CurrentPost);
        yield return player.DirectView(seat.position + seat.right * .12f,
            seat.position + seat.forward * 12 + Vector3.up * 2, 1.8f);
        Busy = false;
    }

    private void CreateEquipment()
    {
        Transform seat = player.PostSeat(yard);
        device = new GameObject("Камера блогера 13 · передатчик").transform;
        device.SetPositionAndRotation(seat.position + seat.forward * 2.1f - Vector3.up * .35f, seat.rotation);
        Material material = seat.parent.GetComponentInChildren<Renderer>().sharedMaterial;
        void Part(string name, Vector3 location, Vector3 size, Color color)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(device, false);
            part.transform.localPosition = location;
            part.transform.localScale = size;
            Destroy(part.GetComponent<Collider>());
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(properties);
            if (name == "Индикатор") indicator = renderer;
        }
        Part("Корпус", Vector3.zero, new Vector3(.65f, .35f, .28f), new Color32(45, 80, 82, 255));
        Part("Объектив", new Vector3(-.13f, .03f, -.18f), new Vector3(.19f, .19f, .12f), new Color32(12, 22, 30, 255));
        Part("Антенна", new Vector3(.24f, .35f, .03f), new Vector3(.025f, .5f, .025f), new Color32(145, 165, 165, 255));
        Part("Штатив", new Vector3(0, -.55f, 0), new Vector3(.045f, 1f, .045f), new Color32(145, 165, 165, 255));
        Part("Опора", new Vector3(0, -1.04f, 0), new Vector3(.65f, .04f, .4f), new Color32(45, 80, 82, 255));
        Part("Индикатор", new Vector3(.23f, .03f, -.15f), Vector3.one * .055f, new Color32(252, 191, 103, 255));
        var lamp = new GameObject("Подсветка оборудования");
        lamp.transform.SetParent(device, false);
        lamp.transform.localPosition = new Vector3(-.45f, .7f, -1.1f);
        lamp.transform.LookAt(device.position);
        equipmentLight = lamp.AddComponent<Light>();
        lamp.AddComponent<HDAdditionalLightData>();
        equipmentLight.type = LightType.Spot;
        equipmentLight.lightUnit = LightUnit.Candela;
        equipmentLight.intensity = 350;
        equipmentLight.range = 5;
        equipmentLight.spotAngle = 85;
        equipmentLight.color = new Color(1, .87f, .68f);
        UpdateIndicator();
    }

    private void UpdateIndicator()
    {
        var properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", SignalConnected ? Accent : new Color32(252, 191, 103, 255));
        properties.SetColor("_EmissiveColor", SignalConnected ? Accent * 3 : new Color(1, .5f, .12f) * 2);
        indicator.SetPropertyBlock(properties);
    }

    private void Update()
    {
        if (!Active || Busy || player.IsRunning) return;
        if (CurrentStep == Step.ReachCamera && player.CurrentPost == yard)
        {
            CurrentStep = Step.ConnectPower;
            StartCoroutine(EquipmentShot(1.8f));
        }
        if (CurrentStep == Step.VerifyFeed)
        {
            viewedFor = player.ViewedChannel == yard ? viewedFor + Time.unscaledDeltaTime : 0;
            if (viewedFor >= 2)
            {
                RecordStep(4);
                CurrentStep = Step.ReturnToEntrance;
                player.ObjectivePost = 0;
            }
        }
    }

    private IEnumerator EquipmentShot(float hold)
    {
        Busy = true;
        shotCaption = CurrentStep == Step.ConnectPower ? "Подключаю питание камеры…" : "Настраиваю передатчик…";
        shotStarted = Time.unscaledTime;
        shotDuration = hold + 2.05f;
        Vector3 eye = device.position - device.forward * .95f + Vector3.up * .16f;
        yield return player.DirectView(eye, device.position + Vector3.up * .08f, hold);
        Busy = false;
    }

    public void ConnectPower()
    {
        if (!CanInteract(Step.ConnectPower)) return;
        StartCoroutine(Connect());
    }

    private IEnumerator Connect()
    {
        yield return EquipmentShot(2);
        RecordStep(2);
        CurrentStep = Step.TuneTransmitter;
    }

    public void TuneTransmitter()
    {
        if (!CanInteract(Step.TuneTransmitter)) return;
        StartCoroutine(Tune());
    }

    private IEnumerator Tune()
    {
        yield return EquipmentShot(3);
        RecordStep(3);
        UpdateIndicator();
        CurrentStep = Step.VerifyFeed;
    }

    private bool CanInteract(Step step) => Active && !Busy && !player.IsDirecting &&
        !player.IsRunning && !player.TabletOpen && !player.MapOpen && player.CurrentPost == yard && CurrentStep == step;

    public void LeavePark()
    {
        if (!Active || Busy || CurrentStep != Step.ReturnToEntrance || player.CurrentPost != 0 ||
            player.IsRunning || player.TabletOpen || player.MapOpen) return;
        StartCoroutine(ExitShot());
    }

    private IEnumerator ExitShot()
    {
        Busy = true;
        shotCaption = "Камера работает. Пора выбраться из парка.";
        shotStarted = Time.unscaledTime;
        shotDuration = 4.05f;
        Transform seat = player.PostSeat(0);
        yield return player.DirectView(seat.position + seat.forward * .4f,
            seat.position + seat.forward * 12, 2);
        CompleteSegment();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void Skip()
    {
        if (!Active || Finished) return;
        StopAllCoroutines();
        CompleteSegment();
    }
#endif

    private void CompleteSegment()
    {
        if (!replay)
        {
            PlayerPrefs.SetInt(ProgressKey, 5);
            ParkProgress.CompleteFirstPostNight();
        }
        CurrentStep = Step.Complete;
        player.ObjectivePost = -1;
        player.enabled = false;
        Busy = false;
        if (replay) ParkNightShift.ReturnToMenu();
        else night.StartPendingWalk();
    }

    private void OnDestroy()
    {
        if (device) Destroy(device.gameObject);
        if (dawn)
        {
            Destroy(dawn.profile);
            Destroy(dawn.gameObject);
        }
    }

    public void DrawOverlay()
    {
        if (!Active || Finished) return;
        if (heading == null)
        {
            heading = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, wordWrap = true };
            body = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            caption = new GUIStyle(body) { fontSize = 14, normal = { textColor = Accent } };
            button = new GUIStyle(GUI.skin.button) { fontSize = 17, wordWrap = true, padding = new RectOffset(12, 12, 8, 8) };
        }
        Matrix4x4 previous = GUI.matrix;
        int previousDepth = GUI.depth;
        GUI.depth = -30;
        float scale = Mathf.Min(Screen.width / 1150f, Screen.height / 650f);
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        float width = Screen.width / scale, height = Screen.height / scale;
        float top = player.TabletOpen ? height - 194 : 16;
        Fill(new Rect(16, top, 560, 126), Panel);
        Fill(new Rect(16, top, 4, 126), Accent);
        GUI.Label(new Rect(32, top + 9, 520, 24), "ПОСЛЕ ПЕРВОЙ НОЧИ · 06:00 · ПРОГРЕСС СОХРАНЯЕТСЯ", caption);
        string title = CurrentStep == Step.ReturnToEntrance ? "Вернуться к центральному входу" :
            CurrentStep == Step.VerifyFeed ? "Проверить канал 13 на планшете" :
            CurrentStep == Step.TuneTransmitter ? "Настроить передатчик" :
            CurrentStep == Step.ConnectPower ? "Подключить питание камеры" : "Добраться до хозяйственного двора";
        GUI.Label(new Rect(32, top + 36, 520, 36), title, heading);
        string hint = CurrentStep == Step.ReturnToEntrance ? "Сигнал устойчив. Можно уходить из парка." :
            CurrentStep == Step.VerifyFeed ? "Открой камеры и наблюдай канал 13 в течение 2 секунд." :
            CurrentStep == Step.ReachCamera ? "Карту открывает M. Моя последняя камера так и осталась без связи." :
            player.CurrentPost != yard ? "Передатчик у хозяйственного двора. Открой карту [M]." :
            "Оборудование передо мной. Сначала питание, затем проверка связи.";
        GUI.Label(new Rect(32, top + 76, 520, 42), hint, body);
        if (Busy)
        {
            Fill(new Rect(0, 0, width, 12), Panel);
            Fill(new Rect(0, height - 54, width, 54), Panel);
            GUI.Label(new Rect(width / 2 - 300, height - 44, 600, 34), shotCaption, body);
            Fill(new Rect(0, height - 3, width * Mathf.Clamp01((Time.unscaledTime - shotStarted) / shotDuration), 3), Accent);
        }
        else if (!player.IsRunning && !player.TabletOpen && !player.MapOpen)
        {
            Rect action = new Rect(width / 2 - 180, height - 112, 360, 44);
            if (CanInteract(Step.ConnectPower) && GUI.Button(action, "Подключить питание", button)) ConnectPower();
            else if (CanInteract(Step.TuneTransmitter) && GUI.Button(action, "Настроить передатчик", button)) TuneTransmitter();
            else if (CurrentStep == Step.ReturnToEntrance && player.CurrentPost == 0 && GUI.Button(action, "Покинуть парк", button)) LeavePark();
        }
        GUI.depth = previousDepth;
        GUI.matrix = previous;
    }

    private static void Fill(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
