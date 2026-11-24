using System;
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
    private bool completed;

    private void Start()
    {
        player = GetComponent<ParkRunController>();
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
        double now = Time.unscaledTimeAsDouble;
        double elapsed = now - startTime;
        Hour = (int)(Math.Min(elapsed, Duration) / (Duration / 6));
        if (elapsed >= Duration)
        {
            completed = true;
            beam.enabled = false;
            player.enabled = false;
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
            GUI.Label(new Rect(0, height / 2 - 85, width, 95), "6 утра", victoryTime);
            GUI.Label(new Rect(0, height / 2 + 10, width, 60), "Победа! Ночь пройдена", victoryTitle);
        }
        else
        {
            GUI.Box(new Rect(10, 8, 145, 32), $"Фонарик  {charges}/{MaxCharges}");
        }
        GUI.matrix = previousMatrix;
        GUI.depth = previousDepth;
    }
}
