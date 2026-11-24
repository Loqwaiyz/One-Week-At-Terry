using UnityEngine;

public static class ParkProgress
{
    public const string MenuScene = "Assets/NightPark/MainMenu.unity";
    public const string GameScene = "Assets/NightPark/NightPark.unity";
    private const string CompletedKey = "Terry.Progress.CompletedNight.v1";
    private const string ContinueKey = "Terry.Progress.ContinueNight.v1";

    // Recognise saves created before the main menu was introduced.
    public static int CompletedNight => Mathf.Max(PlayerPrefs.GetInt(CompletedKey, 0),
        ParkPostNight.HasUnfinishedSegment || ParkPostNight.SegmentCompleted ? 1 : 0);
    public static bool CanContinue => CompletedNight >= 1;
    public static int ContinueNight => ParkPostNight.HasUnfinishedSegment ? 1 :
        Mathf.Clamp(PlayerPrefs.GetInt(ContinueKey, ParkPostNight.SegmentCompleted ? 2 : CompletedNight + 1), 1, 7);

    public static void RecordStarted(int number)
    {
        if (!CanContinue) return;
        PlayerPrefs.SetInt(ContinueKey, number);
        PlayerPrefs.Save();
    }

    public static void RecordVictory(int number)
    {
        PlayerPrefs.SetInt(CompletedKey, Mathf.Max(CompletedNight, number));
        PlayerPrefs.SetInt(ContinueKey, number == 1 ? 1 : Mathf.Min(7, number + 1));
        PlayerPrefs.Save();
    }

    public static void CompleteFirstPostNight()
    {
        PlayerPrefs.SetInt(ContinueKey, 2);
        PlayerPrefs.Save();
    }

    public static void NewGame()
    {
        PlayerPrefs.DeleteKey(CompletedKey);
        PlayerPrefs.DeleteKey(ContinueKey);
        ParkPostNight.ResetProgress();
        PlayerPrefs.Save();
    }
}

public static class ParkOptions
{
    private const string Prefix = "Terry.Options.";
    public static float Volume => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Volume", .8f));
    public static bool Atmosphere => PlayerPrefs.GetInt(Prefix + "Atmosphere", 1) != 0;
    public static bool Fullscreen => PlayerPrefs.HasKey(Prefix + "Fullscreen") ?
        PlayerPrefs.GetInt(Prefix + "Fullscreen") != 0 : Screen.fullScreen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Apply()
    {
        AudioListener.volume = Volume;
        if (PlayerPrefs.HasKey(Prefix + "Quality"))
            QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Quality"), 0, QualitySettings.names.Length - 1));
        if (PlayerPrefs.HasKey(Prefix + "Fullscreen"))
            Screen.fullScreen = PlayerPrefs.GetInt(Prefix + "Fullscreen") != 0;
    }

    public static void SetVolume(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(Prefix + "Volume", AudioListener.volume);
    }

    public static void SetQuality(int index)
    {
        index = Mathf.Clamp(index, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(index);
        PlayerPrefs.SetInt(Prefix + "Quality", index);
    }

    public static void SetFullscreen(bool enabled)
    {
        Screen.fullScreen = enabled;
        PlayerPrefs.SetInt(Prefix + "Fullscreen", enabled ? 1 : 0);
    }

    public static void SetAtmosphere(bool enabled)
    {
        PlayerPrefs.SetInt(Prefix + "Atmosphere", enabled ? 1 : 0);
    }
}
