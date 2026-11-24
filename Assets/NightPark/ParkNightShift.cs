using System;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[RequireComponent(typeof(ParkRunController))]
public sealed class ParkNightShift : MonoBehaviour
{
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
    private int keshaPost = -1;
    private int umiPost = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSelection() => nextNight = 1;
    private int terryPost = -1, larryPost = -1;
    private double NightTime => Time.unscaledTimeAsDouble - startTime;
    public int BrokenChannel => rules == null ? -1 : rules.Broken;

    public bool TryDepart()
    {
        if (completed || !rules.CanDepart(NightTime)) return false;
        rules.Depart();
        return true;
    }

    private void Start()
    {
        player = GetComponent<ParkRunController>();
        Number = nextNight;
        nextNight = 1;
        rules = player.CreateNight(Number);
        terry = player.CreateMascot(ParkMascot.Terry);
        larry = player.CreateMascot(ParkMascot.Larry);
        if (Number >= 2)
        {
            umi = player.CreateMascot(ParkMascot.Umi);
            umi.gameObject.SetActive(false);
        }
        if (Number == 3)
        {
            kesha = player.CreateMascot(ParkMascot.Kesha);
            kesha.gameObject.SetActive(false);
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
    }

    private void Update()
    {
        if (completed) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKey(KeyCode.C) && Input.GetKey(KeyCode.D) &&
            (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Plus) ||
             (Input.GetKeyDown(KeyCode.Equals) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))))
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
        double deadline = rules.CaughtByKesha ? rules.KeshaDeadline : rules.CaughtByUmi ? rules.UmiAttackAt : rules.AttackAt;
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
    }

    private void FinishNight(bool caught)
    {
        completed = true;
        lost = caught;
        if (!caught) Hour = 6;
        beam.enabled = false;
        player.enabled = false;
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
        if (beam) beam.enabled = false;
    }

    private void OnGUI()
    {
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
            if (victoryTitle == null)
            {
                victoryTime = new GUIStyle(GUI.skin.label) { fontSize = 72, alignment = TextAnchor.MiddleCenter };
                victoryTitle = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
            }
            if (selectingNight)
            {
                GUI.Label(new Rect(0, height / 2 - 70, width, 60), "Выбор ночи", victoryTitle);
                if (GUI.Button(new Rect(width / 2 - 110, height / 2 + 8, 220, 48), "Ночь 1"))
                    LoadNight(1);
                if (GUI.Button(new Rect(width / 2 - 110, height / 2 + 64, 220, 48), "Ночь 2"))
                    LoadNight(2);
                if (GUI.Button(new Rect(width / 2 - 110, height / 2 + 120, 220, 48), "Ночь 3"))
                    LoadNight(3);
            }
            else
            {
                GUI.Label(new Rect(0, height / 2 - 85, width, 95), lost ? (rules.CaughtByKesha ? "Кеша поймал тебя" : rules.CaughtByUmi ? "Уми нашла тебя" : "Терри поймал тебя") : "6 утра", victoryTime);
                GUI.Label(new Rect(0, height / 2 + 10, width, 60), lost ? $"Ночь {Number} не пройдена" : $"Победа! Ночь {Number} пройдена", victoryTitle);
                if (lost)
                {
                    if (GUI.Button(new Rect(width / 2 - 230, height / 2 + 88, 220, 48), "Дальше"))
                        selectingNight = true;
                    if (GUI.Button(new Rect(width / 2 + 10, height / 2 + 88, 220, 48), "Начать сначала"))
                        LoadNight(Number);
                }
                else if (GUI.Button(new Rect(width / 2 - 110, height / 2 + 88, 220, 48),
                    Number < 3 ? $"Начать ночь {Number + 1}" : "Выбор ночи"))
                {
                    if (Number < 3) LoadNight(Number + 1);
                    else selectingNight = true;
                }
            }
        }
        else
        {
            float centre = Screen.width / scale / 2;
            if (rules != null && rules.AttackAt >= 0)
                GUI.Box(new Rect(centre - 160, 58, 320, 32), $"Тяжёлые шаги рядом! Беги · {Math.Ceiling(Math.Max(0, rules.AttackAt - NightTime))} с");
            if (rules != null && rules.Kesha >= 0)
                GUI.Box(new Rect(centre - 260, 58, 520, 32), $"Кеша! Найди за {FirstNightRules.KeshaSearchSeconds} с и смотри {FirstNightRules.KeshaWatchSeconds} с подряд");
            if (rules != null && rules.UmiAttackAt >= 0)
                GUI.Box(new Rect(centre - 220, 58, 440, 32), "Шорох у укрытия! Спрячься и не выглядывай");
            if (rules != null && player.CanRepairHere && player.CurrentPost == rules.Broken)
            {
                Rect repair = new Rect(centre - 110, Screen.height / scale - 100, 220, 36);
                if (rules.LarryAtFault) GUI.Box(repair, "Ларри ещё у камеры");
                else if (rules.Repairing) GUI.Box(repair, $"Ремонт · {rules.RepairRemaining(NightTime):0.0} с");
                else if (GUI.Button(repair, "Починить камеру · 2 с"))
                    rules.BeginRepair(NightTime, player.CurrentPost);
            }
            GUI.Box(new Rect(10, 8, 145, 32), $"Фонарик  {charges}/{MaxCharges}");
        }
        GUI.matrix = previousMatrix;
        GUI.depth = previousDepth;
    }
}
