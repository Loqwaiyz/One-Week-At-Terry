using System;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[RequireComponent(typeof(ParkRunController))]
public sealed class ParkNightShift : MonoBehaviour
{
    private const int LastNight = 7;
    private Transform falcon, wolf, elephant;
    private AudioClip trumpet;
    private AudioSource warning, encounterAudio;
    private AudioClip footsteps, scratching, chirp, clank;
    private int heardFailures;
    private double showLitFalconUntil;
    private AudioClip wingbeat, howl;
    private FirstNightRules.Visitor shownVisitor;
    private int shownRoom = -1;
    private const int MaxCharges = 15;
    private const double Duration = 420.0;
    private const double FlashDuration = 1.0;

    private Light beam;
    private int charges = MaxCharges;
    private double startTime, flashEnds;
    private ParkRunController player;
    public int Hour { get; private set; }
    private GUIStyle victoryTime, victoryTitle;
    private bool completed, lost, selectingNight, loading;
    private FirstNightRules rules;
    private static int nextNight = 1;
    public int Number { get; private set; } = 1;
    private Transform terry, larry, umi, kesha;
    private ParkPostNight postNight;
    public bool IsPostNight => postNight && postNight.Active;
    public bool IsChannelAvailable(int index) => index != GetComponent<ParkRunController>().YardPost ||
        ((IsPostNight || Number > 1) && ParkPostNight.CameraConnected);

    public void DrawPostNightOverlay() => postNight.DrawOverlay();
    private int keshaPost = -1;
    private int umiPost = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSelection() => nextNight = 1;

    public static void StartFromMenu(int number)
    {
        nextNight = Mathf.Clamp(number, 1, LastNight);
        Time.timeScale = 1;
        SceneManager.LoadSceneAsync(ParkProgress.GameScene);
    }

    public static void ReturnToMenu()
    {
        Time.timeScale = 1;
        SceneManager.LoadSceneAsync(ParkProgress.MenuScene);
    }
    private int terryPost = -1, larryPost = -1;
    private double NightTime => Time.unscaledTimeAsDouble - startTime;
    public bool IsBroken(int channel) => !IsPostNight && rules != null && rules.IsBroken(channel);

    public bool TryDepart()
    {
        if (IsPostNight) return postNight.CanTravel;
        if (completed || !rules.CanDepart(NightTime)) return false;
        rules.Depart();
        return true;
    }

    private void Start()
    {
        player = GetComponent<ParkRunController>();
        Number = nextNight;
        nextNight = 1;
        ParkProgress.RecordStarted(Number);
        rules = player.CreateNight(Number);
        terry = player.CreateMascot(ParkMascot.Terry);
        larry = player.CreateMascot(ParkMascot.Larry);
        if (Number >= 2)
        {
            umi = player.CreateMascot(ParkMascot.Umi);
            umi.gameObject.SetActive(false);
        }
        if (Number >= 3)
        {
            kesha = player.CreateMascot(ParkMascot.Kesha);
            kesha.gameObject.SetActive(false);
        }
        encounterAudio = gameObject.AddComponent<AudioSource>();
        encounterAudio.loop = true;
        footsteps = CreateCue(ParkMascot.Terry);
        scratching = CreateCue(ParkMascot.Umi);
        chirp = CreateCue(ParkMascot.Kesha);
        clank = CreateCue(ParkMascot.Larry);
        if (Number >= 5)
        {
            falcon = player.CreateMascot(ParkMascot.Falcon);
            wolf = player.CreateMascot(ParkMascot.Wolf);
            falcon.gameObject.SetActive(false);
            wolf.gameObject.SetActive(false);
            if (Number == 7)
            {
                elephant = player.CreateMascot(ParkMascot.Elephant);
                elephant.gameObject.SetActive(false);
                trumpet = CreateCue(ParkMascot.Elephant);
            }
            var emitter = new GameObject("Звук крыльев");
            emitter.transform.SetParent(transform, false);
            warning = emitter.AddComponent<AudioSource>();
            warning.spatialBlend = 1;
            warning.minDistance = 6;
            warning.dopplerLevel = 0;
            warning.loop = true;
            wingbeat = CreateWarning(false);
            howl = CreateWarning(true);
        }
        var lamp = new GameObject("Фонарик");
        lamp.transform.SetParent(transform, false);
        lamp.transform.localPosition = new Vector3(.08f, -.08f, .12f);
        beam = lamp.AddComponent<Light>();
        beam.type = LightType.Spot;
        lamp.AddComponent<HDAdditionalLightData>();
        beam.lightUnit = UnityEngine.Rendering.LightUnit.Candela;
        beam.intensity = 2800f;
        beam.color = new Color(1f, .94f, .81f);
        beam.range = 42f;
        beam.spotAngle = 56f;
        beam.innerSpotAngle = 28f;
        beam.shadows = LightShadows.Soft;
        beam.enabled = false;
        startTime = Time.unscaledTimeAsDouble;
        if (Number == 1 && ParkPostNight.HasUnfinishedSegment) BeginPostNight();
    }

    private static AudioClip CreateCue(ParkMascot kind)
    {
        const int rate = 22050;
        float[] samples = new float[rate * 2];
        var random = new System.Random(31);
        double grain = 0, phase = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double t = (double)i / rate;
            double noise = 2 * random.NextDouble() - 1;
            grain = .8 * grain + .2 * noise;
            double pulse, sound;
            switch (kind)
            {
                case ParkMascot.Elephant:
                    phase += 2 * Math.PI * (190 + 140 * Math.Sin(Math.PI * Math.Min(t, .75) / .75)) / rate;
                    sound = t < .75 ? Math.Sin(Math.PI * t / .75) *
                        (.45 * Math.Sin(phase) + .27 * Math.Sin(3*phase) + .13 * Math.Sin(5*phase)) : 0;
                    break;
                case ParkMascot.Terry:
                    pulse = t % .94;
                    sound = Math.Exp(-12 * pulse) * (.72 * Math.Sin(2 * Math.PI * (96 * pulse - 10 * pulse * pulse)) + .55 * grain);
                    break;
                case ParkMascot.Kesha:
                    pulse = t < .28 ? t : t - .28;
                    phase += 2 * Math.PI * (1250 + 510 * Math.Sin(14 * pulse)) / rate;
                    sound = pulse < .21 ? .53 * Math.Pow(Math.Sin(Math.PI * pulse / .21), 2) * Math.Sin(phase) : 0;
                    break;
                case ParkMascot.Umi:
                    pulse = t % .18;
                    sound = t < .54 ? 1.8 * grain * Math.Pow(Math.Sin(Math.PI * pulse / .18), 4) : 0;
                    break;
                default:
                    sound = Math.Exp(-9 * t) * (.46 * Math.Sin(2 * Math.PI * 620 * t) + .32 * Math.Sin(2 * Math.PI * 917 * t) + .5 * grain);
                    break;
            }
            samples[i] = Mathf.Clamp((float)(sound * Math.Min(1, t * 180) * Math.Min(1, (2 - t) * 100)), -1f, 1f);
        }
        var clip = AudioClip.Create(kind.ToString(), samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void UpdateEncounterAudio()
    {
        AudioClip cue = rules.AttackAt >= 0 ? footsteps : rules.Kesha >= 0 ? chirp :
            rules.Umi >= 0 ? scratching :
            rules.Intruder == FirstNightRules.Visitor.Elephant && rules.Response != FirstNightRules.ElephantResponse.Hide ? howl : null;
        if (encounterAudio.clip != cue)
        {
            encounterAudio.Stop();
            encounterAudio.clip = cue;
            if (cue) encounterAudio.Play();
        }
        encounterAudio.volume = cue == scratching && rules.UmiAttackAt < 0 ? .36f :
            cue == chirp && player.SelectedChannel != rules.Kesha ? .46f : .9f;
        if (rules.Failures != heardFailures) encounterAudio.PlayOneShot(clank, .8f);
        heardFailures = rules.Failures;
    }

    private static AudioClip CreateWarning(bool wolfCall)
    {
        const int rate = 22050;
        var samples = new float[rate * 2];
        var noise = new System.Random(53);
        double phase = 0, rustle = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double t = (double)i / rate;
            double envelope = Math.Pow(Math.Sin(Math.PI * t / 2), 2);
            phase += 2 * Math.PI * (255 + 75 * Math.Sin(Math.PI * t / 2)) / rate;
            rustle = .75 * rustle + .25 * (2 * noise.NextDouble() - 1);
            double sound = wolfCall ? .65 * Math.Sin(phase) + .2 * Math.Sin(2 * phase) :
                rustle * Math.Pow(Math.Sin(4 * Math.PI * t), 6);
            samples[i] = (float)(sound * envelope);
        }
        var clip = AudioClip.Create(wolfCall ? "Вой" : "Взмахи крыльев", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void ShowVisitor(double elapsed)
    {
        if (!warning) return;
        if (shownVisitor != rules.Intruder || (rules.Intruder == FirstNightRules.Visitor.Elephant && shownRoom != rules.IntruderRoom))
        {
            shownVisitor = rules.Intruder;
            shownRoom = rules.IntruderRoom;
            bool showWolf = shownVisitor == FirstNightRules.Visitor.Wolf;
            falcon.gameObject.SetActive(shownVisitor == FirstNightRules.Visitor.Falcon);
            wolf.gameObject.SetActive(showWolf);
            bool showElephant = shownVisitor == FirstNightRules.Visitor.Elephant;
            if (elephant) elephant.gameObject.SetActive(showElephant);
            warning.Stop();
            if (shownVisitor != FirstNightRules.Visitor.None)
            {
                if (showElephant)
                {
                    bool onCamera = rules.ElephantChannel >= 0;
                    foreach (Transform part in elephant)
                        part.gameObject.layer = onCamera ? ParkMascots.SurveillanceLayer : 0;
                    if (onCamera)
                    {
                        player.PlaceMascot(elephant, rules.ElephantChannel, ParkMascot.Elephant);
                        elephant.position += Vector3.up * 1.38f;
                    }
                    else player.PlaceVisitor(elephant, rules.IntruderRoom, true);
                }
                else if (showWolf) player.PlaceVisitor(wolf, rules.IntruderRoom, true);
                else if (!player.PlaceFalcon(falcon, rules.IntruderRoom))
                {
                    rules.SkipBlockedFalcon(elapsed);
                    falcon.gameObject.SetActive(false);
                    return;
                }
                warning.transform.position = showElephant ? transform.position : (showWolf ? wolf : falcon).position;
                warning.clip = showElephant ? trumpet : showWolf ? howl : wingbeat;
                warning.Play();
            }
        }
        if (shownVisitor == FirstNightRules.Visitor.Falcon) warning.transform.position = falcon.position;
        if (shownVisitor == FirstNightRules.Visitor.None)
            falcon.gameObject.SetActive(elapsed < showLitFalconUntil);
        if (shownVisitor == FirstNightRules.Visitor.Elephant && rules.ElephantChannel < 0)
            elephant.gameObject.SetActive(player.SelectedChannel < 0);
        if (rules.WolfStunned) warning.Stop();
        warning.volume = shownVisitor == FirstNightRules.Visitor.Wolf ?
            Mathf.Lerp(.15f,.8f,1f - (float)(rules.IntruderDeadline - elapsed) / 9f) : .95f;
    }

    private void OnDestroy()
    {
        if (footsteps) Destroy(footsteps);
        if (scratching) Destroy(scratching);
        if (chirp) Destroy(chirp);
        if (clank) Destroy(clank);
        if (wingbeat) Destroy(wingbeat);
        if (howl) Destroy(howl);
        if (trumpet) Destroy(trumpet);
    }

    private void Update()
    {
        if (completed) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKey(KeyCode.C) && Input.GetKey(KeyCode.D) &&
            (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Plus) ||
             Input.GetKeyDown(KeyCode.Equals))) // Main keyboard +/= key, with or without Shift.
        {
            FinishNight(false);
            return;
        }
#endif
        double now = Time.unscaledTimeAsDouble;
        double elapsed = now - startTime;
        Hour = (int)(Math.Min(elapsed, Duration) / (Duration / 6));
        rules.Tick(Math.Min(elapsed, Duration), player.CurrentPost, player.IsRunning, player.CanRepairHere, player.IsHidden, player.ViewedChannel);
        if (terryPost != rules.Terry) player.PlaceMascot(terry, terryPost = rules.Terry, ParkMascot.Terry);
        if (larryPost != rules.Larry) player.PlaceMascot(larry, larryPost = rules.Larry, ParkMascot.Larry);
        if (umi && umiPost != rules.Umi)
        {
            umiPost = rules.Umi;
            umi.gameObject.SetActive(umiPost >= 0);
            if (umiPost >= 0) player.PlaceMascot(umi, umiPost, ParkMascot.Umi);
        }
        if (kesha && keshaPost != rules.Kesha)
        {
            keshaPost = rules.Kesha;
            kesha.gameObject.SetActive(keshaPost >= 0);
            if (keshaPost >= 0) player.PlaceMascot(kesha, keshaPost, ParkMascot.Kesha);
        }
        ShowVisitor(elapsed);
        UpdateEncounterAudio();
        double deadline = rules.Intruder != FirstNightRules.Visitor.None ? rules.IntruderDeadline : rules.CaughtByKesha ? rules.KeshaDeadline : rules.CaughtByUmi ? rules.UmiAttackAt : rules.AttackAt;
        bool caughtBeforeDawn = rules.Caught && deadline < Duration;
        if (elapsed >= Duration || caughtBeforeDawn)
        {
            FinishNight(caughtBeforeDawn);
            return;
        }

        if (player.UsingCover)
        {
            flashEnds = 0;
            beam.enabled = false;
            return;
        }
        if (charges > 0 && Input.GetMouseButtonDown(1))
        {
            charges--;
            flashEnds = now + FlashDuration;
        }
        beam.enabled = now < flashEnds;
        if (beam.enabled && rules.Intruder != FirstNightRules.Visitor.None && player.CanRepairHere)
        {
            Transform target = shownVisitor == FirstNightRules.Visitor.Elephant ? elephant : shownVisitor == FirstNightRules.Visitor.Wolf ? wolf : falcon;
            Vector3 offset = target.position - beam.transform.position;
            if (offset.sqrMagnitude <= beam.range * beam.range &&
                Vector3.Angle(beam.transform.forward, offset) <= beam.spotAngle / 2f &&
                !Physics.Linecast(beam.transform.position, target.position, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (shownVisitor == FirstNightRules.Visitor.Falcon)
                    showLitFalconUntil = flashEnds - startTime;
                rules.Flash(elapsed);
            }
        }
    }

    private void FinishNight(bool caught)
    {
        if (warning) warning.Stop();
        encounterAudio.Stop();
        completed = true;
        lost = caught;
        if (!caught) Hour = 6;
        beam.enabled = false;
        player.enabled = false;
        if (!caught) ParkProgress.RecordVictory(Number);
        if (!caught && Number == 1) ParkPostNight.MarkPending();
    }

    public void BeginPostNight()
    {
        if (IsPostNight || Number != 1 || !ParkPostNight.HasUnfinishedSegment) return;
        completed = true;
        lost = selectingNight = false;
        Hour = 6;
        beam.enabled = false;
        if (warning) warning.Stop();
        if (encounterAudio) encounterAudio.Stop();
        foreach (Transform mascot in new[] { terry, larry, umi, kesha })
            if (mascot) mascot.gameObject.SetActive(false);
        player.PreparePostNight();
        postNight = gameObject.AddComponent<ParkPostNight>();
        postNight.Begin(player, this);
    }

    public void StartPendingWalk()
    {
        if (loading) return;
        loading = true;
        ResumeWalk(ParkProgress.PendingWalk);
    }

    public static void ResumeWalk(int day)
    {
        Time.timeScale = 1;
        Terry.MiniGames.MiniGameModule.LoadAfterDay(day, () =>
        {
            ParkProgress.CompleteWalk(day);
            StartFromMenu(day + 1);
        });
    }

    private void LoadNight(int number)
    {
        if (loading) return;
        loading = true;
        nextNight = number;
        SceneManager.LoadSceneAsync(gameObject.scene.path);
    }

    private void OnDisable()
    {
        if (warning) warning.Stop();
        if (encounterAudio) encounterAudio.Stop();
        if (beam) beam.enabled = false;
    }

    private void OnGUI()
    {
        if (IsPostNight)
        {
            if (!player.enabled) postNight.DrawOverlay();
            return;
        }
        int previousDepth = GUI.depth;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.depth = -20;
        float scale = Mathf.Min(Screen.width / 1150f, Screen.height / 650f);
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        if (completed)
        {
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            Color previousColor = GUI.color;
            GUI.color = new Color32(12, 22, 30, 255);
            GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
            GUI.color = previousColor;
            if (GUI.Button(new Rect(width / 2 - 110, height - 58, 220, 38), "Главное меню")) ReturnToMenu();
            if (victoryTitle == null)
            {
                victoryTime = new GUIStyle(GUI.skin.label) { fontSize = 72, alignment = TextAnchor.MiddleCenter };
                victoryTitle = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
            }
            if (selectingNight)
            {
                GUI.Label(new Rect(0, height / 2 - 235, width, 60), "Выбор ночи", victoryTitle);
                bool previousEnabled = GUI.enabled;
                for (int i = 1; i <= LastNight; i++)
                {
                    GUI.enabled = previousEnabled && (i == 1 || ParkPostNight.SegmentCompleted);
                    if (GUI.Button(new Rect(width / 2 - 110, height / 2 - 173 + (i - 1) * 52, 220, 44), $"Ночь {i}")) LoadNight(i);
                }
                GUI.enabled = previousEnabled;
            }
            else
            {
                GUI.Label(new Rect(0, height / 2 - 85, width, 95), lost ? (rules.Intruder == FirstNightRules.Visitor.Elephant ? "Год нашёл тебя" : rules.Intruder == FirstNightRules.Visitor.Wolf ? "Клык догнал тебя" : rules.Intruder == FirstNightRules.Visitor.Falcon ? "Савелий настиг тебя" : rules.CaughtByKesha ? "Кеша поймал тебя" : rules.CaughtByUmi ? "Уми нашла тебя" : "Терри поймал тебя") : "6 утра", victoryTime);
                GUI.Label(new Rect(0, height / 2 + 10, width, 60), lost ? $"Ночь {Number} не пройдена" : Number == LastNight ? "Ты пережил все семь ночей!" : $"Победа! Ночь {Number} пройдена", victoryTitle);
                if (lost)
                {
                    if (GUI.Button(new Rect(width / 2 - 230, height / 2 + 88, 220, 48), "Дальше"))
                        selectingNight = true;
                    if (GUI.Button(new Rect(width / 2 + 10, height / 2 + 88, 220, 48), "Начать сначала"))
                        LoadNight(Number);
                }
                else if (GUI.Button(new Rect(width / 2 - 110, height / 2 + 88, 220, 48),
                    Number == 1 ? "К камере у хоздвора" : ParkProgress.PendingWalk == Number ? "Продолжить" : Number < LastNight ? $"Начать ночь {Number + 1}" : "Выбор ночи"))
                {
                    if (Number == 1) BeginPostNight();
                    else if (ParkProgress.PendingWalk == Number) StartPendingWalk();
                    else if (Number < LastNight) LoadNight(Number + 1);
                    else selectingNight = true;
                }
            }
        }
        else
        {
            float centre = Screen.width / scale / 2;
            if (rules != null && player.CanRepairHere && rules.CanRepair(player.CurrentPost))
            {
                Rect repair = new Rect(centre - 110, Screen.height / scale - 100, 220, 36);
                if (rules.Repairing) GUI.Box(repair, $"Ремонт · {rules.RepairRemaining(NightTime):0.0} с");
                else if (GUI.Button(repair, "Починить камеру · 2 с"))
                    rules.BeginRepair(NightTime, player.CurrentPost);
            }
            GUI.Box(new Rect(10, 8, 145, 32), $"Фонарик  {charges}/{MaxCharges}");
        }
        GUI.matrix = previousMatrix;
        GUI.depth = previousDepth;
    }
}
