using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[RequireComponent(typeof(Camera))]
public sealed class ParkRunController : MonoBehaviour
{
    [Serializable]
    private class Post
    {
        public Transform seat;
        public Transform cover;
        public Camera surveillance;
        // Ordered from the seated viewpoint out to the public alley.
        public Transform[] approach;
        public string Name => seat.parent.name;
    }

    [Serializable]
    private class Passage
    {
        public int first;
        public int second;
        public Transform[] corners;
    }

    [SerializeField] private Post[] posts;
    [SerializeField] private Passage[] passages;
    [SerializeField, Min(0.25f)] private float runSeconds = 6f;
    [SerializeField, Range(0f, 85f)] private float lookAngle = 52f;

    private readonly List<Vector3> path = new List<Vector3>();
    private int current;
    private int destination = -1;
    private float elapsed, distance, runDuration;
    private Camera playerCamera;
    private ParkNightShift night;
    private float restingFov;
    private float yaw, targetYaw, pitch;
    private bool lookingAround;
    private readonly HashSet<KeyCode> lookKeys = new HashSet<KeyCode>();
    private float initialRunPitch;
    private const float RunPitch = 45f;
    private GUIStyle titleStyle, hintStyle, mapLabelStyle, tabStyle;
    private const double TabHoverCooldown = .4;
    private const float TabHoverMargin = 8f;
    private double nextTabHoverAt;
    private bool mapOpen, mapKeyHeld, overMapTab;
    private Vector2[] mapPoints;
    private bool overCameraTab;
    private int channel;
    public int ViewedChannel => liveCamera && night.IsChannelAvailable(channel) && !night.IsBroken(channel) ? channel : -1;
    public bool IsDirecting { get; private set; }
    public bool TabletOpen => liveCamera;
    public bool MapOpen => mapOpen;
    public int ObjectivePost { get; set; } = -1;
    public int YardPost => Array.FindIndex(posts, p => p.Name == "Мусорка");
    public Transform PostSeat(int index) => posts[index].seat;
    public int SelectedChannel => liveCamera ? channel : -1;
    private Camera liveCamera;
    private RenderTexture video;

    private void SelectChannel(int index)
    {
        if (!liveCamera) night.PlayTabletOpen();
        else if (channel != index) night.PlayChannelChange();
        CloseTablet();
        channel = index;
        liveCamera = posts[index].surveillance;
        FitVideoToScreen();
        liveCamera.enabled = night.IsChannelAvailable(index);
        mapOpen = false;
        lookingAround = false;
        nextTabHoverAt = Time.unscaledTimeAsDouble + TabHoverCooldown;
    }

    private const float FeedWidthFraction = .73f;
    private Vector2[] cameraPoints;

    private void FitVideoToScreen()
    {
        float feedWidth = Mathf.Max(1f, Screen.width * FeedWidthFraction);
        float feedHeight = Mathf.Max(1f, Screen.height);
        float resolution = Mathf.Min(1f, 1440f / feedWidth, 1080f / feedHeight);
        int width = Mathf.Max(1, Mathf.RoundToInt(feedWidth * resolution));
        int height = Mathf.Max(1, Mathf.RoundToInt(feedHeight * resolution));
        if (!video || video.width != width || video.height != height)
        {
            if (video)
            {
                liveCamera.targetTexture = null;
                video.Release();
                Destroy(video);
            }
            video = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Park camera feed", filterMode = FilterMode.Bilinear
            };
        }
        if (liveCamera.targetTexture != video) liveCamera.targetTexture = video;
    }

    private void CloseTablet(bool playSound = false)
    {
        if (!liveCamera) return;
        if (playSound) night.PlayTabletClose();
        liveCamera.enabled = false;
        liveCamera.targetTexture = null;
        liveCamera = null;
        nextTabHoverAt = Time.unscaledTimeAsDouble + TabHoverCooldown;
    }

    private void OnDisable()
    {
        CloseTablet();
        if (video)
        {
            video.Release();
            Destroy(video);
            video = null;
        }
    }

    private void ToggleMap()
    {
        CloseTablet(playSound: true);
        mapOpen = !mapOpen;
        if (mapOpen) night.PlayMapOpen();
        nextTabHoverAt = Time.unscaledTimeAsDouble + TabHoverCooldown;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) { peekHeld = false; mapKeyHeld = false; overMapTab = overCameraTab = false; lookingAround = false; lookKeys.Clear(); }
    }
    private bool takingCover, peekHeld;
    private float coverBlend, peekBlend;
    public bool UsingCover => takingCover || coverBlend > 0f;
    public bool IsHidden => takingCover && coverBlend >= 1f && peekBlend <= .001f;
    public bool IsPeeking => takingCover && peekBlend > .001f;

    public bool IsRunning => destination >= 0;

    public int CurrentPost => current;
    public bool CanRepairHere => !IsRunning && !UsingCover && !liveCamera && !mapOpen;

    public FirstNightRules CreateNight(int number)
    {
        night = GetComponent<ParkNightShift>();
        var exits = new List<int>[posts.Length];
        for (int i = 0; i < exits.Length; i++) exits[i] = new List<int>();
        foreach (Passage route in passages)
        {
            exits[route.first].Add(route.second);
            exits[route.second].Add(route.first);
        }
        GetComponent<Camera>().cullingMask &= ~(1 << ParkMascots.SurveillanceLayer);
        return new FirstNightRules(exits, Array.FindIndex(posts, p => p.Name == "Цирковой шатёр"),
            Array.FindIndex(posts, p => p.Name == "Черепашье логово"), new System.Random(), number,
            night.IsChannelAvailable(YardPost) ? -1 : YardPost);
    }

    public void PreparePostNight()
    {
        CloseTablet();
        mapOpen = takingCover = peekHeld = lookingAround = false;
        coverBlend = peekBlend = yaw = targetYaw = pitch = 0;
        destination = -1;
        lookKeys.Clear();
        playerCamera.fieldOfView = restingFov;
        transform.SetPositionAndRotation(posts[current].seat.position, posts[current].seat.rotation);
        enabled = true;
    }

    // Shares the same viewpoint and lens as traversal; never introduces a free-fly controller.
    public IEnumerator DirectView(Vector3 position, Vector3 subject, float holdSeconds)
    {
        if (IsRunning || IsDirecting) yield break;
        IsDirecting = true;
        CloseTablet();
        mapOpen = lookingAround = false;
        lookKeys.Clear();
        Vector3 origin = transform.position;
        Quaternion rotation = transform.rotation;
        float fov = playerCamera.fieldOfView;
        Quaternion shotRotation = Quaternion.LookRotation(subject - position);
        for (float t = 0; t < 1; t += Time.unscaledDeltaTime / 1.15f)
        {
            float blend = Mathf.SmoothStep(0, 1, t);
            transform.SetPositionAndRotation(Vector3.Lerp(origin, position, blend), Quaternion.Slerp(rotation, shotRotation, blend));
            playerCamera.fieldOfView = Mathf.Lerp(fov, 46, blend);
            yield return null;
        }
        transform.SetPositionAndRotation(position, shotRotation);
        yield return new WaitForSecondsRealtime(holdSeconds);
        for (float t = 0; t < 1; t += Time.unscaledDeltaTime / .9f)
        {
            float blend = Mathf.SmoothStep(0, 1, t);
            transform.SetPositionAndRotation(Vector3.Lerp(position, origin, blend), Quaternion.Slerp(shotRotation, rotation, blend));
            playerCamera.fieldOfView = Mathf.Lerp(46, fov, blend);
            yield return null;
        }
        transform.SetPositionAndRotation(origin, rotation);
        playerCamera.fieldOfView = fov;
        IsDirecting = false;
    }

    public Transform CreateMascot(ParkMascot kind)
    {
        return ParkMascots.Create(kind, posts[0].cover.GetComponent<Renderer>().sharedMaterial);
    }

    public bool PlaceFalcon(Transform actor, int room)
    {
        Transform seat = posts[room].seat;
        // Try both sides at several heights. Stay inside the actual look limits.
        float side = UnityEngine.Random.value < .5f ? -1 : 1;
        for (int i = 0; i < 24; i++)
        {
            float yawAngle = side * lookAngle * UnityEngine.Random.Range(.65f, 1f);
            side = -side;
            float pitchAngle = UnityEngine.Random.Range(-32f, 12f);
            Vector3 direction = seat.rotation * Quaternion.Euler(pitchAngle, yawAngle, 0) * Vector3.forward;
            float distance = UnityEngine.Random.Range(3.5f, 8f);
            if (Physics.SphereCast(seat.position, .16f, direction, out RaycastHit hit, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                distance = hit.distance - 1.1f;
            if (distance < 1.8f) continue;
            Vector3 point = seat.position + direction * distance;
            if (Physics.CheckSphere(point, 1.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            actor.SetPositionAndRotation(point, Quaternion.LookRotation(Vector3.ProjectOnPlane(-direction, Vector3.up)));
            return true;
        }
        return false;
    }

    public void PlaceVisitor(Transform actor, int room, bool wolf)
    {
        Transform seat = posts[room].seat;
        Vector3 forward = Vector3.ProjectOnPlane(seat.forward, Vector3.up).normalized;
        Vector3 offset = forward * 2.7f + seat.right * (wolf ? -.35f : .55f);
        actor.SetPositionAndRotation(seat.position + offset + Vector3.up * (wolf ? -.22f : .3f),
            Quaternion.LookRotation(-forward));
    }

    public void PlaceMascot(Transform actor, int room, ParkMascot kind)
    {
        var cam = posts[room].surveillance.transform;
        Vector3 centre = posts[room].seat.parent.position;
        Vector3 ground = Vector3.Lerp(centre,cam.position,.55f);
        ground.y = centre.y;
        Vector3 facing = Vector3.ProjectOnPlane(cam.position-ground, Vector3.up).normalized;
        actor.SetPositionAndRotation(ground + Vector3.Cross(Vector3.up,facing)*(kind == ParkMascot.Terry ? 1.25f : kind == ParkMascot.Larry ? -1.25f : 0f),
            Quaternion.LookRotation(facing));
    }

    private void Awake()
    {
        night = GetComponent<ParkNightShift>();
        playerCamera = GetComponent<Camera>();
        restingFov = playerCamera.fieldOfView;
        transform.SetPositionAndRotation(posts[current].seat.position, posts[current].seat.rotation);
    }

    private void Start()
    {
        // Surveillance cameras additionally see a volume with a brighter fixed exposure.
        var exposureObject = new GameObject("Экспозиция камер наблюдения");
        exposureObject.transform.SetParent(transform, false);
        exposureObject.layer = ParkMascots.SurveillanceLayer;
        var volume = exposureObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1000;
        var exposure = volume.profile.Add<Exposure>();
        exposure.mode.Override(ExposureMode.Fixed);
        exposure.fixedExposure.Override(3.5f);
        int exposureMask = 1 << exposureObject.layer;
        foreach (Post post in posts)
            post.surveillance.GetComponent<HDAdditionalCameraData>().volumeLayerMask |= exposureMask;
    }

    private void AddPoint(Vector3 point)
    {
        if (path.Count == 0 || Vector3.Distance(path[path.Count - 1], point) > 0.001f)
            path.Add(point);
    }

    private void BeginRun(Passage passage)
    {
        bool forward = current == passage.first;
        int next = forward ? passage.second : passage.first;
        if (IsRunning || IsDirecting || !night.TryDepart(next)) return;
        mapOpen = false;
        destination = next;
        path.Clear();
        AddPoint(transform.position);
        foreach (Transform step in posts[current].approach) AddPoint(step.position);
        for (int i = 0; i < passage.corners.Length; i++)
            AddPoint(passage.corners[forward ? i : passage.corners.Length - 1 - i].position);
        Transform[] arrival = posts[destination].approach;
        for (int i = arrival.Length - 1; i >= 0; i--) AddPoint(arrival[i].position);
        AddPoint(posts[destination].seat.position);
        distance = 0f;
        for (int i = 1; i < path.Count; i++) distance += Vector3.Distance(path[i - 1], path[i]);
        runDuration = Mathf.Clamp(distance / 7.5f, .25f, Mathf.Max(.25f, runSeconds));
        elapsed = 0f;
        night.PlayRunStep();
        initialRunPitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
        yaw = targetYaw = 0f;
        pitch = 0f;
        lookingAround = false;
    }

    private void LateUpdate()
    {
        if (IsDirecting) return;
        if (liveCamera) FitVideoToScreen();
        if (!IsRunning)
        {
            if (UsingCover)
            {
                UpdateCoverView();
                if (UsingCover && !IsPeeking) return;
            }
            int horizontal = (lookKeys.Contains(KeyCode.D) ? 1 : 0) - (lookKeys.Contains(KeyCode.A) ? 1 : 0);
            int vertical = (lookKeys.Contains(KeyCode.S) ? 1 : 0) - (lookKeys.Contains(KeyCode.W) ? 1 : 0);
            float step = 80f * Time.deltaTime;
            targetYaw = Mathf.Clamp(targetYaw + horizontal * step, -lookAngle, lookAngle);
            pitch = Mathf.Clamp(pitch + vertical * step, -45f, 32f);
            yaw = Mathf.MoveTowards(yaw, targetYaw, 95f * Time.deltaTime);
            transform.rotation = posts[current].seat.rotation * Quaternion.Euler(pitch, yaw, 0);
            return;
        }

        int previousStep = Mathf.FloorToInt(elapsed * 3.4f);
        elapsed += Time.deltaTime;
        if (Mathf.FloorToInt(elapsed * 3.4f) != previousStep && elapsed < runDuration)
            night.PlayRunStep();
        float progress = Mathf.Clamp01(elapsed / runDuration);
        Transform seat = posts[destination].seat;
        if (progress >= 1f)
        {
            playerCamera.fieldOfView = restingFov;
            if (night.CheckEntry(destination)) return;
            current = destination;
            destination = -1;
            transform.SetPositionAndRotation(seat.position, seat.rotation);
            return;
        }

        // Short ease-in and braking intervals, with a constant sprint through the middle.
        const float ramp = .10f;
        float travelled = progress < ramp ? progress * progress / (2f * ramp) :
            progress > 1f - ramp ? 1f - ramp - (1f - progress) * (1f - progress) / (2f * ramp) :
            progress - ramp / 2f;
        float remaining = distance * travelled / (1f - ramp);
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 delta = path[i] - path[i - 1];
            float length = delta.magnitude;
            if (remaining > length && i < path.Count - 1)
            {
                remaining -= length;
                continue;
            }

            float intensity = Mathf.SmoothStep(0f, 1f, Mathf.Min(progress, 1f - progress) / ramp);
            float footfall = elapsed * (Mathf.PI * 3.4f);
            Vector3 position = path[i - 1] + delta * Mathf.Clamp01(remaining / length);
            position.y += intensity * (.34f + .085f * Mathf.Cos(2f * footfall));
            Vector3 heading = Vector3.ProjectOnPlane(delta, Vector3.up);
            float travelYaw = heading.sqrMagnitude > .0001f ? Quaternion.LookRotation(heading).eulerAngles.y : seat.eulerAngles.y;
            float arrival = Mathf.SmoothStep(0f, 1f, (progress - .82f) / .18f);
            float desiredYaw = Mathf.LerpAngle(travelYaw, seat.eulerAngles.y, arrival);
            float runYaw = Mathf.LerpAngle(transform.eulerAngles.y, desiredYaw, 1f - Mathf.Exp(-14f * Time.deltaTime));
            float runPitch = Mathf.SmoothStep(initialRunPitch, RunPitch, progress / .09f);
            runPitch = Mathf.LerpAngle(runPitch, seat.eulerAngles.x, arrival);
            float sway = intensity * Mathf.Sin(footfall);
            position += Quaternion.Euler(0f, runYaw, 0f) * Vector3.right * (.045f * sway);
            playerCamera.fieldOfView = restingFov + 7f * intensity;
            transform.SetPositionAndRotation(position, Quaternion.Euler(runPitch, runYaw, 1.6f * sway));
            break;
        }
    }

    private void UpdateCoverView()
    {
        coverBlend = Mathf.MoveTowards(coverBlend, takingCover ? 1f : 0f, 3f * Time.deltaTime);
        peekBlend = Mathf.MoveTowards(peekBlend, takingCover && peekHeld && coverBlend >= 1f ? 1f : 0f, 4f * Time.deltaTime);
        Post post = posts[current];
        // The front of each counter faces the park. Crouch on the player's side,
        // then raise the viewpoint just above its top while the button is held.
        Vector3 localEye = new Vector3(0f, Mathf.SmoothStep(.08f, 1f, peekBlend), -1.08f);
        Vector3 eye = post.cover.TransformPoint(localEye);
        transform.position = Vector3.Lerp(post.seat.position, eye, Mathf.SmoothStep(0f, 1f, coverBlend));
        if (!IsPeeking)
        {
            pitch = yaw = targetYaw = 0f;
            transform.rotation = post.seat.rotation;
        }
    }

    private void DrawCoverControls(float scale)
    {
        Rect hide = new Rect((Screen.width - 170f * scale) / 2f,
            Screen.height - 46f * scale, 170f * scale, 34f * scale);
        if (!UsingCover)
        {
            if (GUI.Button(hide, "Спрятаться"))
            {
                takingCover = true;
                lookingAround = peekHeld = false;
            }
            return;
        }

        if (!takingCover) return;
        float gap = 8f * scale;
        Rect peek = new Rect(hide.x - hide.width * .5f - gap * .5f, hide.y, hide.width, hide.height);
        Rect leave = new Rect(peek.xMax + gap, hide.y, hide.width, hide.height);
        bool held = GUI.RepeatButton(peek, "Выглянуть · держать");
        if (Event.current.type == EventType.Repaint) peekHeld = held && Application.isFocused;
        if (Event.current.rawType == EventType.MouseUp || Event.current.rawType == EventType.MouseLeaveWindow)
            peekHeld = false;
        if (GUI.Button(leave, "Выйти из укрытия"))
            takingCover = peekHeld = false;
    }

    private void OnGUI()
    {
        DrawControls();
        if (night && night.IsPostNight) night.DrawPostNightOverlay();
    }

    private void DrawControls()
    {
        if (IsDirecting) return;
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            mapLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleCenter, wordWrap = true,
                padding = new RectOffset(1, 1, 0, 0)
            };
        }

        Event key = Event.current;
        if (key.keyCode == KeyCode.W || key.keyCode == KeyCode.A || key.keyCode == KeyCode.S || key.keyCode == KeyCode.D)
        {
            if (key.type == EventType.KeyDown) { lookKeys.Add(key.keyCode); key.Use(); }
            else if (key.type == EventType.KeyUp) { lookKeys.Remove(key.keyCode); key.Use(); }
        }
        if (key.keyCode == KeyCode.M)
        {
            if (key.type == EventType.KeyDown)
            {
                if (!IsRunning && !UsingCover && !mapKeyHeld) ToggleMap();
                mapKeyHeld = true;
                lookingAround = false;
                key.Use();
            }
            else if (key.type == EventType.KeyUp)
            {
                mapKeyHeld = false;
                key.Use();
            }
        }
        if (IsRunning) return;

        float scale = MapScale;
        Rect tab = MapTab;
        if (!night.IsPostNight && !mapOpen && !liveCamera && posts[current].cover)
        {
            DrawCoverControls(scale);
            if (UsingCover) return;
        }
        Rect cameraTab = new Rect(Screen.width - tab.xMax, tab.y, tab.width, tab.height);

        // IMGUI supplies the pointer in the same coordinates as the drawn tab,
        // including the Game view zoom and its letterboxed viewport.
        Event pointer = Event.current;
        if (pointer.rawType == EventType.MouseLeaveWindow)
        {
            overMapTab = overCameraTab = false;
            lookingAround = false;
        }
        else if (pointer.type == EventType.Repaint || pointer.type == EventType.MouseMove ||
                 pointer.type == EventType.MouseDrag || pointer.type == EventType.MouseUp)
        {
            UpdateTabHover(pointer.mousePosition, tab, cameraTab, scale);
        }

        if (liveCamera) DrawTablet();
        if (tabStyle == null) tabStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter };
        tabStyle.fontSize = Mathf.RoundToInt(16f * scale);
        GUI.Box(tab, mapOpen ? "▼   Карта   [M]   ▼" : "▲   Карта   [M]   ▲", tabStyle);
        GUI.Box(cameraTab, liveCamera ? "▼   Камеры   ▼" : "▲   Камеры   ▲", tabStyle);
        if (liveCamera) return;
        bool overTab = tab.Contains(pointer.mousePosition) || cameraTab.Contains(pointer.mousePosition);
        if (mapOpen)
        {
            Rect panel = new Rect(tab.x, tab.y - 272f * scale, 520f * scale, 272f * scale);
            DrawMap(panel, scale);
            if (!overTab) LookOutside(panel);
        }
        else if (!overTab) LookOutside(tab);
    }

    private void UpdateTabHover(Vector2 pointer, Rect mapTab, Rect cameraTab, float scale)
    {
        bool insideMap = mapTab.Contains(pointer);
        bool insideCamera = cameraTab.Contains(pointer);
        if (!lookingAround && Time.unscaledTimeAsDouble >= nextTabHoverAt)
        {
            if (insideMap && !overMapTab) ToggleMap();
            else if (insideCamera && !overCameraTab)
            {
                if (liveCamera) CloseTablet(playSound: true);
                else SelectChannel(channel);
            }
        }

        // Small excursions outside a tab don't count as leaving it. An entry
        // ignored during cooldown is consumed, so waiting over it won't toggle it.
        float margin = TabHoverMargin * scale;
        Rect mapExit = Rect.MinMaxRect(mapTab.xMin - margin, mapTab.yMin - margin,
            mapTab.xMax + margin, mapTab.yMax + margin);
        Rect cameraExit = Rect.MinMaxRect(cameraTab.xMin - margin, cameraTab.yMin - margin,
            cameraTab.xMax + margin, cameraTab.yMax + margin);
        overMapTab = insideMap || (overMapTab && mapExit.Contains(pointer));
        overCameraTab = insideCamera || (overCameraTab && cameraExit.Contains(pointer));
    }

    private float MapScale => Mathf.Min(1.15f, Screen.width * .58f / 520f, Screen.height * .42f / 272f);
    private Rect MapTab
    {
        get
        {
            float scale = MapScale;
            return new Rect(12f * scale, Screen.height - 56f * scale, 210f * scale, 44f * scale);
        }
    }

    private void LookOutside(Rect panel)
    {
        Event input = Event.current;
        if (input.type == EventType.MouseDown && input.button == 0 && !panel.Contains(input.mousePosition))
            lookingAround = true;
        if (input.type == EventType.MouseUp || input.type == EventType.MouseLeaveWindow)
            lookingAround = false;
        if (lookingAround && input.type == EventType.MouseDrag && input.button == 0)
        {
            targetYaw = Mathf.Clamp(targetYaw + input.delta.x * .24f, -lookAngle, lookAngle);
            pitch = Mathf.Clamp(pitch + input.delta.y * .2f, -45f, 32f);
            input.Use();
        }
    }

    private static void Fill(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private void DrawTablet()
    {
        float scale = Mathf.Min(Screen.width / 1150f, Screen.height / 650f);
        float width = Screen.width / scale, height = Screen.height / scale;
        float split = width * FeedWidthFraction;
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        Fill(new Rect(0, 0, width, height), new Color32(32, 38, 40, 255));
        if (!night.IsChannelAvailable(channel) || night.IsBroken(channel))
        {
            Fill(new Rect(0, 0, split, height), new Color32(12, 16, 18, 255));
            GUI.Label(new Rect(split / 2 - 160, height / 2 - 36, 360, 80),
                    night.IsChannelAvailable(channel) ? "Нет сигнала" : "Камера не подключена\nПередатчик у хозяйственного двора", hintStyle);
        }
        else GUI.DrawTexture(new Rect(0, 0, split, height), video, ScaleMode.ScaleToFit, false);
        Fill(new Rect(0, 0, split, 48), new Color32(17, 24, 25, 220));
        GUI.Label(new Rect(220, 6, split - 238, 36), $"КАМ {channel + 1:00}  ·  {posts[channel].Name}", titleStyle);
        GUI.Label(new Rect(split + 16, 17, width - split - 32, 35), "Камеры парка", titleStyle);
        if (night)
            GUI.Label(new Rect(split + 16, 55, width - split - 32, 28), night.IsPostNight ? "После первой ночи · 06:00" : $"Ночь {night.Number} · {night.Hour:00} ч", hintStyle);

        Rect chart = new Rect(split + 16, 92, width - split - 32, height - 184);
        DrawCameraChart(chart);
        GUI.matrix = previous;
    }

    private void DrawCameraChart(Rect chart)
    {
        const float marker = 28f;
        if (cameraPoints == null) cameraPoints = new Vector2[posts.Length];
        // The orientation belongs to the player's post. Switching feeds keeps it fixed.
        float angle = current * 137.5f * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < posts.Length; i++)
        {
            Vector3 location = posts[i].seat.parent.position;
            Vector2 point = new Vector2(location.x * cos + location.z * sin,
                location.x * sin - location.z * cos);
            cameraPoints[i] = point;
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        float fit = Mathf.Min((chart.width - marker) / Mathf.Max(1f, max.x - min.x),
            (chart.height - marker) / Mathf.Max(1f, max.y - min.y));
        Vector2 centre = (min + max) * .5f;
        for (int i = 0; i < posts.Length; i++)
            cameraPoints[i] = chart.center + (cameraPoints[i] - centre) * fit;

        foreach (Passage route in passages)
        {
            Vector2 start = cameraPoints[route.first];
            Vector2 line = cameraPoints[route.second] - start;
            Matrix4x4 chartMatrix = GUI.matrix;
            float degrees = Mathf.Atan2(line.y, line.x) * Mathf.Rad2Deg;
            GUI.matrix = chartMatrix * Matrix4x4.TRS(start, Quaternion.Euler(0, 0, degrees), Vector3.one);
            Fill(new Rect(0, -1, line.magnitude, 2), new Color32(107, 127, 127, 255));
            GUI.matrix = chartMatrix;
        }

        string hovered = null;
        for (int i = 0; i < posts.Length; i++)
        {
            Rect node = new Rect(cameraPoints[i] - Vector2.one * (marker / 2), Vector2.one * marker);
            bool underPointer = node.Contains(Event.current.mousePosition);
            if (underPointer) hovered = posts[i].Name;
            Fill(node, new Color32(145, 165, 165, 255));
            Fill(new Rect(node.x + 2, node.y + 2, marker - 4, marker - 4),
                i == channel || underPointer ? new Color32(38, 116, 104, 255) : new Color32(45, 53, 55, 255));
            GUI.Label(node, (i + 1).ToString("00") + (night.IsChannelAvailable(i) ? "" : " ×"), mapLabelStyle);
            if (GUI.Button(node, GUIContent.none, GUIStyle.none) && i != channel) SelectChannel(i);
        }
        GUI.Label(new Rect(chart.x, chart.yMax + 10, chart.width, 28),
            hovered ?? "Выберите камеру на схеме", hintStyle);
    }

    private void DrawMap(Rect panel, float scale)
    {
        const float width = 520f, height = 272f;
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3(panel.x, panel.y, 0), Quaternion.identity, Vector3.one * scale);
        GUI.BeginGroup(new Rect(0, 0, width, height));
        Fill(new Rect(0, 0, width, height), new Color32(61, 64, 67, 255));
        GUI.Label(new Rect(12, 4, 300, 31), "Карта парка", titleStyle);

        GUI.Label(new Rect(13, 35, 492, 23), "Вы здесь · " + posts[current].Name, hintStyle);

        if (mapPoints == null) mapPoints = new Vector2[posts.Length];
        Bounds bounds = new Bounds(posts[0].seat.parent.position, Vector3.zero);
        foreach (Post post in posts) bounds.Encapsulate(post.seat.parent.position);
        for (int i = 0; i < posts.Length; i++)
        {
            Vector3 p = posts[i].seat.parent.position;
            mapPoints[i] = new Vector2(Mathf.Lerp(54, 466, Mathf.InverseLerp(bounds.min.x, bounds.max.x, p.x)),
                Mathf.Lerp(247, 80, Mathf.InverseLerp(bounds.min.z, bounds.max.z, p.z)));
        }

        foreach (Passage route in passages)
        {
            Vector2 start = mapPoints[route.first];
            Vector2 end = mapPoints[route.second];
            bool adjacent = route.first == current || route.second == current;
            Color color = adjacent ? new Color32(115, 205, 187, 255) : new Color32(119, 124, 128, 255);
            float midX = (start.x + end.x) * .5f;
            Fill(Rect.MinMaxRect(Mathf.Min(start.x, midX) - 1, start.y - 1, Mathf.Max(start.x, midX) + 1, start.y + 1), color);
            Fill(Rect.MinMaxRect(midX - 1, Mathf.Min(start.y, end.y) - 1, midX + 1, Mathf.Max(start.y, end.y) + 1), color);
            Fill(Rect.MinMaxRect(Mathf.Min(midX, end.x) - 1, end.y - 1, Mathf.Max(midX, end.x) + 1, end.y + 1), color);
        }

        for (int i = 0; i < posts.Length; i++)
        {
            Passage exit = null;
            foreach (Passage route in passages)
                if ((route.first == current && route.second == i) || (route.second == current && route.first == i))
                { exit = route; break; }

            Rect node = new Rect(mapPoints[i].x - 48, mapPoints[i].y - 18, 96, 36);
            Color edge = i == current ? new Color32(252, 191, 103, 255) :
                exit != null ? new Color32(115, 205, 187, 255) : new Color32(130, 135, 138, 255);
            Fill(node, edge);
            Fill(new Rect(node.x + 2, node.y + 2, node.width - 4, node.height - 4),
                exit != null ? new Color32(42, 85, 78, 255) : new Color32(53, 55, 58, 255));
            GUI.Label(node, (i == ObjectivePost ? "◆ " : "") + posts[i].Name, mapLabelStyle);
            if (exit != null && GUI.Button(node, GUIContent.none, GUIStyle.none))
            {
                BeginRun(exit);
                break;
            }
        }
        GUI.EndGroup();
        GUI.matrix = previous;
    }
}
