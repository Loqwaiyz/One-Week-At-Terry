using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[RequireComponent(typeof(Camera))]
public sealed class ParkMainMenu : MonoBehaviour
{
    public enum Page { Home, Settings, Extra, ConfirmNewGame }
    [SerializeField] private Material mascotMaterial;
    [SerializeField] private Vector3 mascotPosition = new Vector3(6, 0, -54);
    public Page CurrentPage { get; private set; }
    public bool Loading { get; private set; }
    public bool TerryVisible => mascot && mascot.gameObject.activeSelf;
    public bool ContinueAvailable => ParkProgress.CanContinue;

    private const string DefaultCaption = "НЕ ВСЁ В ПАРКЕ ОСТАЁТСЯ НА СВОИХ МЕСТАХ";
    private static readonly string[] SecretCaptions = {
        "тише-тише", "ай ай ааааай", "быстрее, чернь", "67", "Вывести главную гипотезу игрового проекта"
    };
    private string homeCaption = DefaultCaption;
    private bool missingTerry, silentPortrait;
    private GUIStyle errorSubtitle;
    private Transform mascot;
    private ParkScareView scarePreview;
    private AudioSource previewAudio;
    private AudioClip previewScream;
    private Light overhead, rim;
    private GameObject fixture;
    private AudioSource ambience, effects;
    private AudioClip hum, crackle;
    private Volume grading;
    private Texture2D scrim, scanlines, white;
    private Vector3 cameraOrigin;
    private Quaternion cameraRotation;
    private float fade = 1, nextFlicker, flickerStarted = -10;
    private bool disappear;
    private int selected, extraIndex;
    private GUIStyle title, subtitle, item, small, body, panelTitle, slider, sliderThumb;
    private static readonly Color Ink = new Color32(9, 16, 21, 255);
    private static readonly Color Paper = new Color32(231, 225, 210, 255);
    private static readonly Color Muted = new Color32(149, 163, 164, 255);
    private static readonly Color Amber = new Color32(238, 170, 99, 255);
    private static readonly string[] Names = { "ТЕРРИ", "ЛАРРИ", "УМИ", "КЕША", "САВЕЛИЙ", "КЛЫК", "ГОД" };
    private static readonly ParkMascot[] Kinds = { ParkMascot.Terry, ParkMascot.Larry, ParkMascot.Umi, ParkMascot.Kesha, ParkMascot.Falcon, ParkMascot.Wolf, ParkMascot.Elephant };
    private static readonly int[] UnlockNight = { 1, 1, 2, 3, 5, 7, 7 };
    private static readonly string[] Notes = {
        "Символ парка. Его положение можно определить по камерам.\nКогда он рядом — меняй локацию.",
        "После его визита камеры теряют сигнал.\nДождись ухода Ларри, прежде чем начинать ремонт.",
        "Маленькую фигуру легко пропустить.\nУкрытие помогает, пока ты не выглядываешь.",
        "Требует внимания к своему каналу.\nНе прерывай наблюдение слишком рано.",
        "Прислушивайся к крыльям.\nФонарик должен быть направлен на угрозу.",
        "Вой становится ближе.\nСначала ослепи его, затем уходи подальше.",
        "Одного способа защиты недостаточно.\nСопоставляй изображение и звуковые сигналы."
    };

    private void Awake()
    {
        cameraOrigin = transform.position;
        cameraRotation = transform.rotation;
        Time.timeScale = 1;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        ParkOptions.Apply();
        // One roll per menu scene, including returns from a night.
        int roll = Random.Range(0, 600);
        if (roll < SecretCaptions.Length) homeCaption = SecretCaptions[roll];
        else if (roll == SecretCaptions.Length)
        {
            missingTerry = true;
            homeCaption = "ОШИБКА 404: ОБЪЕКТ «ТЕРРИ» НЕ НАЙДЕН";
        }
        CreateTextures();
        ShowMascot(0);
        CreateLighting();
        ambience = gameObject.AddComponent<AudioSource>();
        ambience.loop = true;
        ambience.volume = .24f;
        hum = MakeSound(false);
        ambience.clip = hum;
        ambience.Play();
        effects = gameObject.AddComponent<AudioSource>();
        effects.volume = .18f;
        crackle = MakeSound(true);
        previewAudio = gameObject.AddComponent<AudioSource>();
        previewAudio.playOnAwake = false;
        nextFlicker = Time.unscaledTime + Random.Range(4, 7);
    }

    private void CreateLighting()
    {
        Light Lamp(string name, Vector3 position, Color color, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform.parent);
            go.transform.position = position;
            go.transform.LookAt(mascotPosition + Vector3.up * 1.6f);
            var light = go.AddComponent<Light>();
            go.AddComponent<HDAdditionalLightData>();
            light.type = LightType.Spot;
            light.lightUnit = LightUnit.Candela;
            light.intensity = intensity;
            light.spotAngle = 78;
            light.range = 14;
            light.color = color;
            light.shadows = LightShadows.Soft;
            return light;
        }
        overhead = Lamp("Мерцающий фонарь над Терри", mascotPosition + new Vector3(0, 4.8f, -1.4f), new Color(1, .69f, .4f), 1900);
        rim = Lamp("Холодный контур Терри", mascotPosition + new Vector3(-3, 2.7f, 2), new Color(.3f, .56f, .85f), 900);
        fixture = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fixture.name = "Старый парковый светильник";
        fixture.transform.position = overhead.transform.position + Vector3.up * .1f;
        fixture.transform.localScale = new Vector3(.7f, .06f, .7f);
        fixture.GetComponent<Collider>().enabled = false;
        fixture.GetComponent<Renderer>().sharedMaterial = mascotMaterial;
        var lightProfile = new GameObject("Экспозиция главного меню");
        grading = lightProfile.AddComponent<Volume>();
        grading.isGlobal = true;
        grading.priority = 1100;
        var exposure = grading.profile.Add<Exposure>();
        exposure.mode.Override(ExposureMode.Fixed);
        exposure.fixedExposure.Override(3.5f);
    }

    private void ShowMascot(int index)
    {
        if (mascot) Destroy(mascot.gameObject);
        mascot = ParkMascots.Create(Kinds[index], mascotMaterial);
        mascot.position = mascotPosition;
        mascot.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.position - mascotPosition, Vector3.up));
        var parts = mascot.GetComponentsInChildren<Renderer>();
        float bottom = float.PositiveInfinity;
        foreach (var part in parts) bottom = Mathf.Min(bottom, part.bounds.min.y);
        float hover = Kinds[index] == ParkMascot.Falcon ? .85f : 0f;
        float floor = mascotPosition.y;
        if (Physics.Raycast(mascotPosition + Vector3.up * .65f, Vector3.down, out RaycastHit hit, 2f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) floor = hit.point.y;
        mascot.position += Vector3.up * (floor + hover - bottom);
        foreach (var renderer in parts)
        {
            renderer.gameObject.layer = 0;
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }
        if (missingTerry && CurrentPage != Page.Extra) mascot.gameObject.SetActive(false);
        flickerStarted = -10;
    }

    public void OpenPage(Page page)
    {
        if (Loading) return;
        PlayerPrefs.Save();
        StopPreview();
        CurrentPage = page;
        if (page == Page.Home) { selected = 0; ShowMascot(0); }
        else if (page == Page.Extra) { extraIndex = 0; ShowMascot(0); }
    }

    public void RequestNewGame()
    {
        if (Loading) return;
        if (ParkProgress.CanContinue) OpenPage(Page.ConfirmNewGame);
        else StartNewGame();
    }

    public void StartNewGame()
    {
        if (Loading) return;
        ParkProgress.NewGame();
        StartCoroutine(Launch(1));
    }

    public void ContinueGame()
    {
        if (Loading || !ContinueAvailable) return;
        StartCoroutine(Launch(ParkProgress.ContinueNight));
    }

    private IEnumerator Launch(int number)
    {
        Loading = true;
        if (missingTerry)
        {
            silentPortrait = true;
            ambience.Stop();
            effects.Stop();
            previewAudio.Stop();
            mascot.gameObject.SetActive(true);
            const int portraitLayer = 29;
            foreach (var part in mascot.GetComponentsInChildren<Renderer>())
                part.gameObject.layer = portraitLayer;
            var camera = GetComponent<Camera>();
            camera.cullingMask = 1 << portraitLayer;
            var hd = GetComponent<HDAdditionalCameraData>();
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            hd.backgroundColorHDR = Color.black;
            transform.LookAt(mascot.position + Vector3.up * 1.7f);
            overhead.intensity = 1900;
            rim.enabled = true;
            yield return new WaitForSecondsRealtime(2f);
            ParkNightShift.StartFromMenu(number);
            yield break;
        }
        for (float t = 0; t < .45f; t += Time.unscaledDeltaTime)
        {
            fade = Mathf.Clamp01(t / .45f);
            yield return null;
        }
        fade = 1;
        if (ParkProgress.PendingWalk != 0) ParkNightShift.ResumeWalk(ParkProgress.PendingWalk);
        else ParkNightShift.StartFromMenu(number);
    }

    public void QuitGame()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Update()
    {
        if (silentPortrait) return;
        if (scarePreview != null)
        {
            if (!scarePreview.Active || Input.GetKeyDown(KeyCode.Escape)) StopPreview();
            else scarePreview.Animate();
            return;
        }
        float now = Time.unscaledTime;
        if (!Loading) fade = Mathf.MoveTowards(fade, 0, Time.unscaledDeltaTime * 1.8f);
        if (Input.GetKeyDown(KeyCode.Escape) && CurrentPage != Page.Home) OpenPage(Page.Home);
        if (!Loading && CurrentPage == Page.Home)
        {
            if (Input.GetKeyDown(KeyCode.DownArrow)) selected = (selected + 1) % 5;
            if (Input.GetKeyDown(KeyCode.UpArrow)) selected = (selected + 4) % 5;
            if (Input.GetKeyDown(KeyCode.Return)) ActivateItem(selected);
        }
        if (!Loading && CurrentPage == Page.Extra)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) PlayPreview();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) SelectExtra(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) SelectExtra(1);
        }
        if (silentPortrait) return;
        bool atmosphere = ParkOptions.Atmosphere && CurrentPage == Page.Home;
        transform.position = cameraOrigin + new Vector3(Mathf.Sin(now * .19f) * .035f, Mathf.Sin(now * .27f) * .018f, 0);
        transform.rotation = cameraRotation * Quaternion.Euler(Mathf.Sin(now * .17f) * .12f, Mathf.Sin(now * .21f) * .2f, 0);
        if (atmosphere && now >= nextFlicker)
        {
            flickerStarted = now;
            nextFlicker = now + Random.Range(6, 11);
            disappear = Random.value < .7f;
            effects.PlayOneShot(crackle);
        }
        float elapsed = now - flickerStarted;
        bool blackout = atmosphere && elapsed < .65f && (elapsed < .2f || elapsed > .4f);
        overhead.intensity = blackout ? 5 : 1900 * (.96f + .04f * Mathf.PerlinNoise(now * 1.8f, 0));
        bool visible = (!missingTerry || CurrentPage == Page.Extra) && !(atmosphere && disappear && elapsed < 1.35f);
        if (CurrentPage == Page.Extra && ParkProgress.CompletedNight < UnlockNight[extraIndex]) visible = false;
        mascot.gameObject.SetActive(visible);
        rim.enabled = !blackout && visible;
    }

    private void ActivateItem(int index)
    {
        switch (index)
        {
            case 0: RequestNewGame(); break;
            case 1: ContinueGame(); break;
            case 2: OpenPage(Page.Settings); break;
            case 3: OpenPage(Page.Extra); break;
            case 4: QuitGame(); break;
        }
    }

    private void CreateTextures()
    {
        white = Texture2D.whiteTexture;
        scrim = new Texture2D(128, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int i = 0; i < 128; i++)
        {
            float alpha = Mathf.Lerp(.96f, .07f, Mathf.SmoothStep(0, 1, i / 127f));
            scrim.SetPixel(i, 0, new Color(Ink.r, Ink.g, Ink.b, alpha));
        }
        scrim.Apply();
        scanlines = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        for (int i = 0; i < 4; i++) scanlines.SetPixel(0, i, new Color(0, 0, 0, i == 0 ? .15f : 0));
        scanlines.Apply();
    }

    private static AudioClip MakeSound(bool broken)
    {
        const int rate = 22050;
        float[] data = new float[broken ? rate / 3 : rate * 4];
        var random = new System.Random(187);
        float previous = 0;
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate;
            previous = .96f * previous + .04f * ((float)random.NextDouble() * 2 - 1);
            data[i] = broken ? previous * Mathf.Pow(1 - (float)i / data.Length, 3) * 1.5f :
                .075f * Mathf.Sin(2 * Mathf.PI * 55 * t) + previous * .16f;
        }
        var clip = AudioClip.Create(broken ? "Треск светильника" : "Гул закрытого парка", data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void Styles()
    {
        if (title != null) return;
        GUIStyle Text(int size, Color color, FontStyle weight = FontStyle.Normal) => new GUIStyle(GUI.skin.label)
        { fontSize = size, fontStyle = weight, wordWrap = true, normal = { textColor = color } };
        title = Text(66, Paper, FontStyle.Bold);
        subtitle = Text(14, Amber);
        errorSubtitle = Text(14, new Color32(239, 83, 74, 255));
        item = Text(23, Paper);
        small = Text(14, Muted);
        body = Text(18, Paper);
        panelTitle = Text(34, Paper, FontStyle.Bold);
        slider = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = 3 };
        slider.normal.background = white;
        sliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = 12, fixedHeight = 16 };
        sliderThumb.normal.background = sliderThumb.hover.background = sliderThumb.active.background = white;
    }

    private bool Row(Rect rect, string label, bool enabled = true, bool chosen = false)
    {
        bool hover = enabled && rect.Contains(Event.current.mousePosition);
        bool highlight = hover || chosen;
        if (highlight) Fill(rect, new Color(.6f, .37f, .2f, .12f));
        if (highlight) Fill(new Rect(rect.x, rect.y + 8, 3, rect.height - 16), Amber);
        Color old = GUI.color;
        GUI.color = enabled ? Color.white : new Color(1, 1, 1, .3f);
        GUI.Label(new Rect(rect.x + 19, rect.y + 6, rect.width - 24, rect.height - 8), label, item);
        GUI.color = old;
        bool previous = GUI.enabled;
        GUI.enabled = enabled && !Loading;
        bool pressed = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        GUI.enabled = previous;
        if (pressed) effects.PlayOneShot(crackle, .25f);
        return pressed;
    }

    private void OnGUI()
    {
        if (silentPortrait) return;
        if (scarePreview != null)
        {
            scarePreview.Draw(new Rect(0, 0, Screen.width, Screen.height));
            return;
        }
        Styles();
        Matrix4x4 previous = GUI.matrix;
        float scale = Mathf.Min(Screen.width / 1150f, Screen.height / 650f);
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        float width = Screen.width / scale, height = Screen.height / scale;
        GUI.DrawTexture(new Rect(0, 0, width, height), scrim);
        GUI.DrawTextureWithTexCoords(new Rect(0, 0, width, height), scanlines, new Rect(0, 0, 1, Screen.height / 4f));
        Fill(new Rect(34, 32, 7, 7), new Color(.85f, .18f, .12f, .5f + .5f * Mathf.Sin(Time.unscaledTime * 2)));
        GUI.Label(new Rect(50, 24, 420, 26), "REC  /  АРХИВ ПАРАНОРМАЛЬНЫХ ЯВЛЕНИЙ", small);
        GUI.Label(new Rect(width - 292, 24, 270, 26), "CAM 01  //  ПАРК ЗАКРЫТ", small);
        if (CurrentPage == Page.Home) DrawHome(height);
        else if (CurrentPage == Page.Settings) DrawSettings();
        else if (CurrentPage == Page.Extra) DrawExtra();
        else DrawNewGameConfirmation();
        GUI.Label(new Rect(72, height - 42, 620, 26), CurrentPage == Page.Home ?
            "↑ ↓  выбор     ENTER  подтвердить" : CurrentPage == Page.Extra ?
            (Kinds[extraIndex] == ParkMascot.Larry ? "← →  записи     ESC  главное меню" : "← →  записи     ENTER  скример     ESC  главное меню") : "ESC  главное меню · настройки сохраняются", small);
        GUI.Label(new Rect(width - 225, height - 42, 210, 26), TerryVisible ? "СИГНАЛ УСТОЙЧИВ" : "ОБЪЕКТ НЕ ОБНАРУЖЕН", small);
        if (fade > 0) Fill(new Rect(0, 0, width, height), new Color(Ink.r, Ink.g, Ink.b, fade));
        GUI.matrix = previous;
    }

    private void DrawHome(float height)
    {
        GUI.Label(new Rect(72, 86, 500, 160), "ONE WEEK\nAT TERRY", title);
        GUI.Label(new Rect(76, 250, 470, 30), homeCaption, missingTerry ? errorSubtitle : subtitle);
        string[] labels = { "Новая игра", "Продолжить", "Настройки", "Экстра", "Выход" };
        for (int i = 0; i < labels.Length; i++)
        {
            Rect row = new Rect(72, 300 + i * 48, 355, 44);
            bool enabled = i != 1 || ContinueAvailable;
            if (row.Contains(Event.current.mousePosition) && enabled && Event.current.type == EventType.MouseMove) selected = i;
            if (Row(row, labels[i], enabled, selected == i && enabled)) ActivateItem(i);
        }
        string progress = !ContinueAvailable ? "Продолжение откроется после первой ночи" :
            ParkProgress.PendingWalk != 0 ? $"Сохранено: прогулка после дня {ParkProgress.PendingWalk}" :
            ParkPostNight.HasUnfinishedSegment ? "Сохранено: после первой ночи" : $"Сохранено: ночь {ParkProgress.ContinueNight}";
        GUI.Label(new Rect(91, 548, 450, 40), progress, small);
    }

    private void DrawSettings()
    {
        GUI.Label(new Rect(72, 105, 500, 50), "НАСТРОЙКИ", panelTitle);
        GUI.Label(new Rect(76, 174, 380, 35), $"Общая громкость   {ParkOptions.Volume * 100:0}%", body);
        Color previous = GUI.color;
        GUI.color = Amber;
        float volume = GUI.HorizontalSlider(new Rect(80, 222, 330, 24), ParkOptions.Volume, 0, 1, slider, sliderThumb);
        GUI.color = previous;
        if (!Mathf.Approximately(volume, ParkOptions.Volume)) ParkOptions.SetVolume(volume);
        GUI.Label(new Rect(76, 270, 400, 34), "Качество изображения", body);
        int quality = QualitySettings.GetQualityLevel();
        if (Row(new Rect(72, 308, 355, 44), QualitySettings.names[quality] + "   ›"))
            ParkOptions.SetQuality((quality + 1) % QualitySettings.names.Length);
        if (Row(new Rect(72, 366, 410, 44), "Полный экран: " + (ParkOptions.Fullscreen ? "вкл." : "выкл."))) ParkOptions.SetFullscreen(!ParkOptions.Fullscreen);
        if (Row(new Rect(72, 424, 470, 44), "Мерцание и исчезновения: " + (ParkOptions.Atmosphere ? "вкл." : "выкл."))) ParkOptions.SetAtmosphere(!ParkOptions.Atmosphere);
        if (Row(new Rect(72, 512, 355, 44), "‹  Назад")) OpenPage(Page.Home);
    }

    private void DrawExtra()
    {
        GUI.Label(new Rect(72, 105, 500, 50), "ЭКСТРА / МАСКОТЫ", panelTitle);
        bool unlocked = ParkProgress.CompletedNight >= UnlockNight[extraIndex];
        GUI.Label(new Rect(76, 181, 380, 45), unlocked ? Names[extraIndex] : "НЕИЗВЕСТНЫЙ ОБЪЕКТ", item);
        GUI.Label(new Rect(76, 245, 410, 160), unlocked ? Notes[extraIndex] :
            "Материалы ещё не найдены.\n\nЗаверши соответствующую ночь, чтобы открыть запись расследования.", body);
        if (Kinds[extraIndex] != ParkMascot.Larry && Row(new Rect(72, 354, 355, 44), "Посмотреть скример", unlocked)) PlayPreview();
        if (Row(new Rect(72, 414, 175, 44), "‹  Назад")) SelectExtra(-1);
        if (Row(new Rect(258, 414, 175, 44), "Вперёд  ›")) SelectExtra(1);
        GUI.Label(new Rect(91, 472, 355, 28), $"ЗАПИСЬ {extraIndex + 1:00} / 07", small);
        if (Row(new Rect(72, 524, 355, 44), "‹  Главное меню")) OpenPage(Page.Home);
    }

    private void PlayPreview()
    {
        if (Loading || CurrentPage != Page.Extra || scarePreview != null ||
            ParkProgress.CompletedNight < UnlockNight[extraIndex]) return;
        ParkMascot kind = Kinds[extraIndex];
        if (kind == ParkMascot.Larry) return;
        scarePreview = new ParkScareView(ParkMascots.Create(kind, mascotMaterial), kind);
        if (!previewScream) previewScream = Resources.Load<AudioClip>("ParkJumpscare");
        if (previewScream) previewAudio.PlayOneShot(previewScream, .48f);
    }

    private void StopPreview()
    {
        scarePreview?.Dispose();
        scarePreview = null;
        if (previewAudio) previewAudio.Stop();
    }

    public void SelectExtra(int direction)
    {
        extraIndex = (extraIndex + direction + Names.Length) % Names.Length;
        ShowMascot(extraIndex);
    }

    private void DrawNewGameConfirmation()
    {
        GUI.Label(new Rect(72, 135, 480, 90), "НОВОЕ\nРАССЛЕДОВАНИЕ", panelTitle);
        GUI.Label(new Rect(76, 265, 410, 120), "Начать с первой ночи?\nСохранённое прохождение будет заменено. Настройки останутся.", body);
        if (Row(new Rect(72, 420, 355, 44), "Начать заново")) StartNewGame();
        if (Row(new Rect(72, 478, 355, 44), "Вернуться")) OpenPage(Page.Home);
    }

    private void Fill(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, white);
        GUI.color = previous;
    }

    private void OnDisable()
    {
        StopPreview();
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        if (mascot) Destroy(mascot.gameObject);
        if (overhead) Destroy(overhead.gameObject);
        if (rim) Destroy(rim.gameObject);
        if (fixture) Destroy(fixture);
        if (grading) { Destroy(grading.profile); Destroy(grading.gameObject); }
        if (scrim) Destroy(scrim);
        if (scanlines) Destroy(scanlines);
        if (hum) Destroy(hum);
        if (crackle) Destroy(crackle);
    }
}
