using System;
using UnityEngine;

// The shared post-attack presentation for every night and mascot.
internal sealed class ParkDeathScreen : IDisposable
{
    private const float FrameSeconds = .4f;
    private const int NoiseWidth = 384, NoiseHeight = 216;
    private readonly Texture2D[] frames = new Texture2D[4];
    private readonly Texture2D noise;
    private readonly Color32[] pixels = new Color32[NoiseWidth * NoiseHeight];
    private readonly System.Random random = new System.Random();
    private readonly AudioSource audio;
    private readonly AudioClip staticClip, music;
    private double started, nextNoise;
    private bool showing, carousel;
    private GUIStyle signalStyle, titleStyle;
    internal bool ShowingCarousel => showing && carousel;

    internal ParkDeathScreen(GameObject owner)
    {
        for (int i = 0; i < frames.Length; i++)
            frames[i] = Resources.Load<Texture2D>($"DeathScreen/Carousel-v1/carousel-death-{i + 1:00}");
        staticClip = Resources.Load<AudioClip>("DeathScreen/Static");
        music = Resources.Load<AudioClip>("DeathScreen/GameOver");
        noise = new Texture2D(NoiseWidth, NoiseHeight, TextureFormat.RGBA32, false)
        {
            name = "Death screen static", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat
        };
        audio = owner.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0;
        audio.priority = 32;
    }

    internal void BeginSignalLost()
    {
        showing = true;
        carousel = false;
        started = Time.unscaledTimeAsDouble;
        audio.clip = staticClip;
        audio.loop = false;
        audio.volume = .55f;
        audio.Play();
        RefreshNoise();
    }

    internal void Tick()
    {
        if (!showing) return;
        double now = Time.unscaledTimeAsDouble;
        if (!carousel && now - started >= (staticClip ? staticClip.length : 2f))
        {
            carousel = true;
            started = now;
            audio.Stop();
            audio.clip = music;
            audio.loop = true;
            audio.volume = .65f;
            audio.Play();
        }
        if (!carousel && now >= nextNoise) RefreshNoise();
    }

    private void RefreshNoise()
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            byte value = (byte)random.Next(25, 205);
            pixels[i] = new Color32(value, value, value, 255);
        }
        noise.SetPixels32(pixels);
        noise.Apply(false);
        nextNoise = Time.unscaledTimeAsDouble + 1.0 / 24;
    }

    internal void Draw()
    {
        if (!showing) return;
        float width = Screen.width, height = Screen.height;
        if (signalStyle == null)
        {
            signalStyle = new GUIStyle(GUI.skin.label) { fontSize = 46, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            signalStyle.normal.textColor = Color.white;
            titleStyle = new GUIStyle(signalStyle) { fontSize = 40, alignment = TextAnchor.MiddleRight };
            titleStyle.normal.textColor = new Color32(239, 219, 177, 255);
        }
        if (!carousel)
        {
            GUI.DrawTexture(new Rect(0, 0, width, height), noise, ScaleMode.StretchToFill, false);
            GUI.color = new Color(0, 0, 0, .14f);
            for (float y = 0; y < height; y += 4)
                GUI.DrawTexture(new Rect(0, y, width, 2), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        else
        {
            int index = (int)((Time.unscaledTimeAsDouble - started) / FrameSeconds) % frames.Length;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (frames[index]) GUI.DrawTexture(new Rect(0, 0, width, height), frames[index], ScaleMode.ScaleAndCrop, false);
        }

        float scale = Mathf.Min(width / 1150f, height / 650f);
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        width /= scale;
        height /= scale;
        if (!carousel)
        {
            GUI.color = new Color(0, 0, 0, .82f);
            GUI.DrawTexture(new Rect(width / 2 - 235, height / 2 - 47, 470, 94), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, height / 2 - 40, width, 80), "Signal Lost", signalStyle);
            return;
        }

        var title = new Rect(width - 354, 28, 320, 60);
        GUI.color = new Color(0, 0, 0, .9f);
        GUI.Label(new Rect(title.x + 2, title.y + 2, title.width, title.height), "GAME OVER", titleStyle);
        GUI.color = Color.white;
        GUI.Label(title, "GAME OVER", titleStyle);
    }

    public void Dispose()
    {
        audio.Stop();
        UnityEngine.Object.Destroy(audio);
        UnityEngine.Object.Destroy(noise);
    }
}
