using UnityEngine;

/// <summary>
/// Options the game actually reads. The menu writes them; gameplay applies them.
/// </summary>
public static class GameSettings
{
    public const string VolumeKey = "MasterVolume";
    public const string LookKey = "LookSensitivity";
    public const string InvertKey = "InvertLook";

    const string RevisionKey = "QuestAR.SettingsRev";
    const int Revision = 1;

    public static float MasterVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));

    public static float LookScale => Mathf.Clamp(PlayerPrefs.GetFloat(LookKey, 1f), 0.5f, 1.5f);

    public static bool InvertLook => PlayerPrefs.GetInt(InvertKey, 0) == 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        EnsureCurrent();
        ApplyAudio();
    }

    public static void EnsureCurrent()
    {
        if (PlayerPrefs.GetInt(RevisionKey, 0) >= Revision)
            return;

        PlayerPrefs.SetFloat(VolumeKey, 1f);
        PlayerPrefs.SetFloat(LookKey, 1f);
        PlayerPrefs.SetInt(InvertKey, 0);
        PlayerPrefs.SetInt(RevisionKey, Revision);
        PlayerPrefs.Save();
    }

    public static void ApplyAudio()
    {
        AudioListener.volume = MasterVolume;
    }
}
