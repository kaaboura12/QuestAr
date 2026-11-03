using UnityEngine;

/// <summary>
/// Gameplay one-shots for the AR scene. The hurt voice plays with a health loss.
/// </summary>
[DefaultExecutionOrder(180)]
public class ARGameplayAudio : MonoBehaviour
{
    const string HurtVoicePath =
        "Assets/_Project/Audio/SFX/hurt voice/freesound_community-male_hurt7-48124.wav";

    [SerializeField] AudioClip hurtVoice;

    AudioSource voiceSource;

    void Awake()
    {
        if (hurtVoice == null)
            hurtVoice = LoadClip(HurtVoicePath);

        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;
        voiceSource.dopplerLevel = 0f;
        voiceSource.priority = 32;
        voiceSource.volume = 1f;

        if (hurtVoice != null && hurtVoice.loadState != AudioDataLoadState.Loaded)
            hurtVoice.LoadAudioData();

        EnsureListener();
        GameSettings.ApplyAudio();
    }

    public void PlayHurt()
    {
        if (hurtVoice == null || voiceSource == null)
            return;

        if (hurtVoice.loadState != AudioDataLoadState.Loaded)
            hurtVoice.LoadAudioData();

        voiceSource.pitch = 1f;
        voiceSource.PlayOneShot(hurtVoice, 1f);
    }

    static void EnsureListener()
    {
        AudioListener[] listeners = FindObjectsByType<AudioListener>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < listeners.Length; i++)
        {
            if (listeners[i] != null && listeners[i].enabled)
                return;
        }

        Camera camera = Camera.main;
        if (camera == null)
            return;

        AudioListener listener = camera.GetComponent<AudioListener>();
        if (listener == null)
            camera.gameObject.AddComponent<AudioListener>();
        else
            listener.enabled = true;
    }

    static AudioClip LoadClip(string path)
    {
        var registry = ARCombatAssetRegistry.Instance;
        if (registry != null && registry.hurtVoice != null)
            return registry.hurtVoice;

#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
#else
        _ = path;
        return null;
#endif
    }
}
