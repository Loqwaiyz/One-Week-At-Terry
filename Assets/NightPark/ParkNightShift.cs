using System;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[RequireComponent(typeof(ParkRunController))]
public sealed class ParkNightShift : MonoBehaviour
{
    private const int LastNight = 7;
    private Transform falcon, wolf, elephant;
    private const float ThreatVolume = .56f;
    private AudioSource effects, background;
    private AudioClip nightHum, metalCreak, distantImpact, cableMoan;
    private readonly System.Random ambienceRandom = new System.Random();
    private double nextDistantSound;
    private int lastDistantSound = -1;
    private AudioClip leftStep, rightStep, unfold, tabletOn, channelChange, flashOn, flashOff, dawn, defeat;
    private AudioClip tabletOff, jumpscare;
    private bool nextStepRight;
    private AudioClip trumpet;
    private AudioSource warning, encounterAudio;
    private AudioClip footsteps, scratching, chirp, clank;
    private ParkScareView scare;
    private ParkMascot killer;
    private int routePost = -1;
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
    private bool briefing;
    private GUIStyle briefingText;
    private static readonly string[] Briefings = {
        "Терри виден на камерах. Перед перебежкой проверь, свободна ли локация. Услышишь тяжёлые шаги рядом — открой карту и беги в соседнюю локацию.\n\nМеталлический лязг означает, что Ларри сломал камеру. Дождись его ухода, доберись до этой локации и почини камеру.",
        "Уми выдаёт прерывистый шорох. Когда он станет громче, закрой планшет и нажми «Спрятаться». Оставайся в укрытии, пока шорох не стихнет.",
        "Кешу выдаёт свист. Переключай камеры и прислушивайся: на его канале свист громче. Смотри на Кешу, пока он не уйдёт. Если камера потеряла сигнал, сначала почини её.",
        null,
        "Следи за Савелием по камерам. Уходи с его пути и выбирай свободную локацию. Если он добрался до тебя, прислушайся к крыльям. Закрой планшет, выйди из укрытия и повернись на звук. Направь фонарик на птицу и нажми ПКМ.",
        null,
        "Клык. Его видно на камерах по пути к тебе. Смени локацию заранее. При нарастающем вое посвети волку в глаза. Беги дальше соседней локации, чтобы оторваться от него.\n\nГод. После трубного звука проверь камеры. Видишь слона без воя — прячься до тишины. Слышишь вой без слона на камерах — закрой планшет и посвети на него.\n\nЕсли видишь слона и слышишь вой, беги. Год пойдёт следом. На новом месте снова проверь сигналы. Если прячешься, дождись тишины, прежде чем выглянуть."
    };
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

    public bool TryDepart(int next)
    {
        if (IsPostNight) return postNight.CanTravel;
        if (briefing || completed || !rules.CanDepart(NightTime)) return false;
        if (CheckEntry(next)) return false;
        rules.Depart();
        return true;
    }

    public bool CheckEntry(int room)
    {
        if (IsPostNight || briefing || completed) return false;
        var occupant = rules.Occupant(room);
        if (!occupant.HasValue) return false;
        FinishNight(NightTime < Duration, occupant);
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
        effects = gameObject.AddComponent<AudioSource>();
        effects.playOnAwake = false;
        background = gameObject.AddComponent<AudioSource>();
        background.playOnAwake = false;
        background.loop = true;
        background.priority = 200;
        nightHum = CreateNightHum();
        background.clip = nightHum;
        metalCreak = CreateEffect(Effect.MetalCreak, 2.6);
        distantImpact = CreateEffect(Effect.DistantImpact, 2.1);
        cableMoan = CreateEffect(Effect.CableMoan, 3.2);
        leftStep = CreateEffect(Effect.LeftStep, .21);
        rightStep = CreateEffect(Effect.RightStep, .23);
        channelChange = CreateEffect(Effect.ChannelChange, .12);
        flashOn = CreateEffect(Effect.FlashOn, .08);
        flashOff = CreateEffect(Effect.FlashOff, .07);
        unfold = CreateEffect(Effect.Map, .29);
        tabletOn = CreateEffect(Effect.Tablet, .22);
        dawn = CreateEffect(Effect.Dawn, 1.8);
        tabletOff = CreateEffect(Effect.TabletOff, .18);
        jumpscare = Resources.Load<AudioClip>("ParkJumpscare");
        defeat = CreateEffect(Effect.Defeat, 1.1);
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
        if (Number == 1 && ParkPostNight.HasUnfinishedSegment) BeginPostNight();
        else if (Briefings[Number - 1] != null)
        {
            briefing = true;
            player.enabled = false;
        }
        else StartNightClock();
    }

    private enum Effect { LeftStep, RightStep, Map, Tablet, TabletOff, ChannelChange, FlashOn, FlashOff, Dawn, Defeat, MetalCreak, DistantImpact, CableMoan }

    public void PlayRunStep()
    {
        effects.PlayOneShot(nextStepRight ? rightStep : leftStep, .21f);
        nextStepRight = !nextStepRight;
    }

    public void PlayChannelChange() => effects.PlayOneShot(channelChange, .33f);

    private void SetFlashlight(bool lit)
    {
        if (beam.enabled == lit) return;
        beam.enabled = lit;
        effects.PlayOneShot(lit ? flashOn : flashOff, .43f);
    }
    public void PlayMapOpen() => effects.PlayOneShot(unfold, .34f);
    public void PlayTabletClose() => effects.PlayOneShot(tabletOff, .30f);
    public void PlayTabletOpen() => effects.PlayOneShot(tabletOn, .32f);

    private void StartNightClock()
    {
        startTime = Time.unscaledTimeAsDouble;
        nextDistantSound = ambienceRandom.Next(80, 131);
        background.volume = 0f;
        background.Play();
    }

    private static AudioClip CreateNightHum()
    {
        const int rate = 11025, seconds = 16;
        var samples = new float[rate * seconds];
        var noise = new System.Random(73);
        double wind = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double t = (double)i / rate;
            double cycle = 2 * Math.PI * t / seconds;
            double swell = .65 + .2 * Math.Cos(cycle) + .15 * Math.Cos(3 * cycle);
            wind = .974 * wind + .026 * (2 * noise.NextDouble() - 1);
            // Periodic tones meet exactly at the seam; taper only the wind there.
            double windWindow = Math.Min(1, Math.Min(t, seconds - t) * 2);
            samples[i] = (float)(swell * (.19 * Math.Sin(2 * Math.PI * 63 * t) +
                .12 * Math.Sin(2 * Math.PI * 66 * t) + .06 * Math.Sin(2 * Math.PI * 127 * t))
                + .55 * wind * windWindow);
        }
        var clip = AudioClip.Create("Ночной парк", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void UpdateBackground(double elapsed)
    {
        bool threat = rules.Intruder != FirstNightRules.Visitor.None || rules.AttackAt >= 0 ||
            rules.Umi >= 0 || rules.Kesha >= 0;
        background.volume = Mathf.MoveTowards(background.volume, threat ? .055f : .14f,
            Time.unscaledDeltaTime * .15f);
        if (elapsed < nextDistantSound) return;
        int next = lastDistantSound < 0 ? ambienceRandom.Next(3) :
            (lastDistantSound + 1 + ambienceRandom.Next(2)) % 3;
        AudioClip clip = next == 0 ? metalCreak : next == 1 ? distantImpact : cableMoan;
        background.PlayOneShot(clip, .85f);
        lastDistantSound = next;
        nextDistantSound = elapsed + ambienceRandom.Next(80, 131);
    }

    private static double Bell(double t, double hz)
    {
        if (t <= 0) return 0;
        double wave = 2 * Math.PI * hz * t;
        return Math.Min(1, t * 100) * Math.Exp(-3.4 * t) *
            (Math.Sin(wave) + .21 * Math.Sin(wave * 2.76));
    }

    private static AudioClip CreateEffect(Effect effect, double duration)
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * duration)];
        var random = new System.Random(19);
        double grain = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double t = (double)i / rate;
            double noise = 2 * random.NextDouble() - 1;
            grain = .76 * grain + .24 * noise;
            double sound;
            switch (effect)
            {
                case Effect.LeftStep:
                case Effect.RightStep:
                    bool right = effect == Effect.RightStep;
                    double toeTime = t - (right ? .069 : .056);
                    double heel = Math.Exp(-38*t) * (.34*Math.Sin(2*Math.PI*(right ? 78 : 90)*t) + 1.2*grain);
                    double scuff = toeTime < 0 ? 0 : Math.Min(1, toeTime*180) *
                        Math.Exp(-24*toeTime) * (.75*grain + .19*noise);
                    sound = heel + scuff;
                    break;
                case Effect.FlashOn:
                case Effect.FlashOff:
                    bool on = effect == Effect.FlashOn;
                    double latch = t - (on ? .019 : .012);
                    sound = Math.Exp(-135*t) * (.5*noise + .38*Math.Sin(2*Math.PI*(on ? 1600 : 950)*t));
                    if (latch > 0) sound += .4*Math.Exp(-170*latch)*Math.Sin(2*Math.PI*(on ? 1050 : 670)*latch);
                    break;
                case Effect.ChannelChange:
                    sound = .52*(noise-grain)*Math.Exp(-33*t) +
                        .21*Math.Sin(2*Math.PI*(980*t - 2100*t*t))*Math.Exp(-35*t);
                    break;
                case Effect.Map:
                    double folds = .25 + .75 * Math.Pow(Math.Sin(27*t), 2);
                    sound = 1.3 * grain * folds * Math.Sin(Math.PI*t/duration);
                    break;
                case Effect.Tablet:
                    sound = .48 * grain * Math.Exp(-65*t) + .33 * Bell(t-.045, 850) + .21 * Bell(t-.10, 1190);
                    break;
                case Effect.TabletOff:
                    double latchTime = Math.Max(0, t - .085);
                    sound = .32 * Math.Sin(2*Math.PI*(740*t - 1500*t*t))*Math.Exp(-29*t) +
                        (t >= .085 ? .54*grain*Math.Exp(-78*latchTime) : 0);
                    break;
                case Effect.MetalCreak:
                    double bend = 2*Math.PI*(218*t + 18*t*t) + 3*Math.Sin(19*t);
                    sound = Math.Pow(Math.Sin(Math.PI*t/duration), 2) *
                        (.28*Math.Sin(bend) + .14*Math.Sin(2.43*bend) + .23*grain);
                    break;
                case Effect.DistantImpact:
                    sound = .68*Bell(t, 83) + .35*Bell(t-.34, 111) +
                        .2*Bell(t-.73, 83) + .5*grain*Math.Exp(-9*t);
                    break;
                case Effect.CableMoan:
                    double tension = 2*Math.PI*(154*t - 5*t*t) + .65*Math.Sin(7*t);
                    sound = Math.Pow(Math.Sin(Math.PI*t/duration), 2) *
                        (.37*Math.Sin(tension) + .15*Math.Sin(1.49*tension));
                    break;
                case Effect.Dawn:
                    sound = .45 * Bell(t, 523.25) + .4 * Bell(t-.28, 659.25) + .42 * Bell(t-.56, 783.99);
                    break;
                default:
                    sound = Math.Exp(-4*t) * (.64*Math.Sin(2*Math.PI*(136*t - 40*t*t)) + .48*grain);
                    break;
            }
            samples[i] = (float)(sound * Math.Min(1, t*180) * Math.Min(1, (duration-t)*65));
        }
        var clip = AudioClip.Create(effect.ToString(), samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    internal static AudioClip CreateCue(ParkMascot kind)
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
        encounterAudio.volume = ThreatVolume * (cue == scratching && rules.UmiAttackAt < 0 ? .36f :
            cue == chirp && player.SelectedChannel != rules.Kesha ? .46f : .9f);
        if (rules.Failures != heardFailures)
        {
            encounterAudio.PlayOneShot(clank, .8f);
        }
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
        if (rules.ApproachRoom >= 0)
        {
            if (routePost != rules.ApproachRoom)
            {
                bool approachingWolf = rules.Approaching == FirstNightRules.Visitor.Wolf;
                var actor = approachingWolf ? wolf : falcon;
                falcon.gameObject.SetActive(!approachingWolf);
                wolf.gameObject.SetActive(approachingWolf);
                routePost = rules.ApproachRoom;
                player.PlaceMascot(actor, routePost, approachingWolf ? ParkMascot.Wolf : ParkMascot.Falcon);
                actor.position += Vector3.up * (approachingWolf ? 1.25f : 2.2f);
            }
            return;
        }
        if (routePost >= 0)
        {
            routePost = -1;
            falcon.gameObject.SetActive(false);
            wolf.gameObject.SetActive(false);
        }
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
        warning.volume = ThreatVolume * (shownVisitor == FirstNightRules.Visitor.Wolf ?
            Mathf.Lerp(.15f,.8f,1f - (float)(rules.IntruderDeadline - elapsed) / 9f) : .95f);
    }

    private void OnDestroy()
    {
        foreach (var clip in new[] { leftStep, rightStep, unfold, tabletOn, tabletOff, channelChange, flashOn, flashOff, dawn, defeat, nightHum, metalCreak, distantImpact, cableMoan })
            if (clip) Destroy(clip);
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
        if (scare != null)
        {
            if (scare.Active) scare.Animate();
            else ClearScare();
        }
        if (briefing || completed) return;
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

        UpdateBackground(elapsed);
        if (player.UsingCover)
        {
            flashEnds = 0;
            SetFlashlight(false);
            return;
        }
        if (charges > 0 && Input.GetMouseButtonDown(1))
        {
            charges--;
            flashEnds = now + FlashDuration;
        }
        SetFlashlight(now < flashEnds);
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

    private void ClearScare()
    {
        scare?.Dispose();
        scare = null;
    }

    private ParkMascot Attacker()
    {
        switch (rules.Intruder)
        {
            case FirstNightRules.Visitor.Wolf: return ParkMascot.Wolf;
            case FirstNightRules.Visitor.Elephant: return ParkMascot.Elephant;
            case FirstNightRules.Visitor.Falcon: return ParkMascot.Falcon;
        }
        if (rules.CaughtByKesha) return ParkMascot.Kesha;
        return rules.CaughtByUmi ? ParkMascot.Umi : ParkMascot.Terry;
    }

    private void FinishNight(bool caught, ParkMascot? collision = null)
    {
        background.Stop();
        if (warning) warning.Stop();
        encounterAudio.Stop();
        effects.Stop();
        ClearScare();
        if (caught)
        {
            killer = collision ?? Attacker();
            scare = new ParkScareView(player, killer);
        }
        if (caught && jumpscare) effects.PlayOneShot(jumpscare, .48f);
        else effects.PlayOneShot(caught ? defeat : dawn, .52f);
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

    public void StartSecondNight()
    {
        if (IsPostNight && postNight.Finished) LoadNight(2);
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
        ClearScare();
        if (background) background.Stop();
        if (effects) effects.Stop();
        if (warning) warning.Stop();
        if (encounterAudio) encounterAudio.Stop();
        if (beam) beam.enabled = false;
    }

    private void DrawBriefing(float width, float height)
    {
        Color tint = GUI.color;
        GUI.color = new Color32(9, 16, 21, 255);
        GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
        GUI.color = tint;
        if (briefingText == null)
            briefingText = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true, richText = false };
        if (victoryTitle == null)
        {
            victoryTime = new GUIStyle(GUI.skin.label) { fontSize = 72, alignment = TextAnchor.MiddleCenter };
            victoryTitle = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
        }
        const float lineWidth = 770;
        var message = new GUIContent(Briefings[Number - 1]);
        float textHeight = briefingText.CalcHeight(message, lineWidth);
        float top = (height - textHeight - 150) / 2;
        GUI.Label(new Rect(0, top, width, 52), $"Ночь {Number}", victoryTitle);
        GUI.Label(new Rect((width - lineWidth) / 2, top + 72, lineWidth, textHeight), message, briefingText);
        if (GUI.Button(new Rect(width / 2 - 115, top + 100 + textHeight, 230, 50), "Начать ночь"))
        {
            StartNightClock();
            briefing = false;
            player.enabled = true;
        }
    }

    private void OnGUI()
    {
        if (IsPostNight)
        {
            if (!player.enabled) postNight.DrawOverlay();
            return;
        }
        if (lost && scare != null && scare.Active)
        {
            scare.Draw(new Rect(0, 0, Screen.width, Screen.height));
            return;
        }
        int previousDepth = GUI.depth;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.depth = -20;
        float scale = Mathf.Min(Screen.width / 1150f, Screen.height / 650f);
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        if (briefing)
            DrawBriefing(Screen.width / scale, Screen.height / scale);
        else if (completed)
        {
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            Color previousColor = GUI.color;
            GUI.color = lost && !selectingNight ? new Color32(37, 13, 18, 255) : new Color32(12, 22, 30, 255);
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
                GUI.Label(new Rect(0, height / 2 - 85, width, 95), lost ? (killer switch { ParkMascot.Elephant => "Год нашёл тебя", ParkMascot.Wolf => "Клык догнал тебя", ParkMascot.Falcon => "Савелий настиг тебя", ParkMascot.Kesha => "Кеша поймал тебя", ParkMascot.Umi => "Уми нашла тебя", _ => "Терри поймал тебя" }) : "6 утра", victoryTime);
                GUI.Label(new Rect(0, height / 2 + 10, width, 60), lost ? $"Ночь {Number} не пройдена" : Number == LastNight ? "Ты пережил все семь ночей!" : $"Победа! Ночь {Number} пройдена", victoryTitle);
                if (lost)
                {
                    if (GUI.Button(new Rect(width / 2 - 230, height / 2 + 88, 220, 48), "Дальше"))
                        selectingNight = true;
                    if (GUI.Button(new Rect(width / 2 + 10, height / 2 + 88, 220, 48), "Начать сначала"))
                        LoadNight(Number);
                }
                else if (GUI.Button(new Rect(width / 2 - 110, height / 2 + 88, 220, 48),
                    Number == 1 ? "К камере у хоздвора" : Number < LastNight ? $"Начать ночь {Number + 1}" : "Выбор ночи"))
                {
                    if (Number == 1) BeginPostNight();
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
