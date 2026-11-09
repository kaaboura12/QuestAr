using System;
using UnityEngine;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Détecte la voix forte ou un cri via le microphone en calculant la puissance RMS nette
/// (avec calibration du bruit ambiant et anti-auto-déclenchement lors des tirs) et en la convertissant en dB.
/// Actif au Niveau 4 (QuestARLevelManager.IsMicUnlocked).
/// </summary>
[DefaultExecutionOrder(-50)]
public class ARMicDetectionManager : MonoBehaviour
{
    public static ARMicDetectionManager Instance { get; private set; }

    /// <summary>
    /// Événement C# statique auquel les zombies et les effets visuels s'abonnent.
    /// </summary>
    public static event Action OnLoudSound;

    public static float AlertTimeRemaining { get; private set; }
    public static bool IsAlertActive => AlertTimeRemaining > 0f;

    [Header("Microphone Settings")]
    [Tooltip("Seuil de détection en décibels (-24 dB = voix forte / cri réel, ignore le silence et le bruit de fond).")]
    [SerializeField] private float thresholdDb = -24f;

    [Tooltip("Gain d'amplification appliqué au signal audio.")]
    [SerializeField] private float amplificationGain = 1.5f;

    [Tooltip("Délai anti-spam entre deux détections (en secondes).")]
    [SerializeField] private float cooldownSeconds = 6.0f;

    [Tooltip("Fréquence d'échantillonnage pour le microphone (44100 Hz).")]
    [SerializeField] private int sampleRate = 44100;

    [Header("Debug Live Metrics")]
    [SerializeField] private float currentRms = 0f;
    [SerializeField] private float noiseFloorRms = 0.004f;
    [SerializeField] private float currentDecibels = -90f;
    [SerializeField] private float timeSinceLastTrigger = 999f;
    [SerializeField] private bool isMicrophoneRecording = false;

    private AudioClip micClip;
    private string selectedDevice = null;
    private int sampleWindow = 1024;
    private float[] sampleBuffer;
    private float lastTriggerTime = -999f;
    private float sustainedLoudTimer = 0f;
    private float calibrationTimer = 0f;
    private static float ignoreUntilTime = 0f;

    public float CurrentDecibels => currentDecibels;
    public float ThresholdDb => thresholdDb;
    public float AmplificationGain => amplificationGain;
    public float CooldownRemaining => Mathf.Max(0f, cooldownSeconds - (Time.time - lastTriggerTime));

    /// <summary>
    /// Appelé quand le joueur tire pour éviter que le haut-parleur du téléphone ne déclenche le micro.
    /// </summary>
    public static void NotifyWeaponShot()
    {
        ignoreUntilTime = Time.time + 0.42f;
    }

    public void SetThreshold(float value)
    {
        thresholdDb = Mathf.Clamp(value, -55f, -8f);
        Debug.Log($"[ARMicDetectionManager] Nouveau seuil : {thresholdDb:F1} dB");
    }

    public void SetGain(float value)
    {
        amplificationGain = Mathf.Clamp(value, 0.5f, 10f);
        Debug.Log($"[ARMicDetectionManager] Nouveau gain : x{amplificationGain:F1}");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoEnsureManager()
    {
        if (Instance == null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "ARGameplay")
        {
            var go = new GameObject("ARMicDetectionManager");
            go.AddComponent<ARMicDetectionManager>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        sampleBuffer = new float[sampleWindow];
    }

    private void Start()
    {
        if (QuestARLevelManager.IsMicUnlocked)
        {
            RequestMicrophonePermissionAndStart();
        }
    }

    private void RequestMicrophonePermissionAndStart()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
            StartCoroutine(WaitForPermissionCoroutine());
            return;
        }
#endif
        StartMicrophoneRecording();
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private System.Collections.IEnumerator WaitForPermissionCoroutine()
    {
        float timer = 0f;
        while (!Permission.HasUserAuthorizedPermission(Permission.Microphone) && timer < 4f)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            StartMicrophoneRecording();
        }
        else
        {
            Debug.LogWarning("[ARMicDetectionManager] Permission microphone refusée par l'utilisateur.");
        }
    }
#endif

    public void StartMicrophoneRecording()
    {
        if (isMicrophoneRecording)
            return;

        selectedDevice = null;
        try
        {
            micClip = Microphone.Start(selectedDevice, true, 2, sampleRate);
            isMicrophoneRecording = true;
            calibrationTimer = 1.0f;
            Debug.Log($"[ARMicDetectionManager] Enregistrement micro démarré ({sampleRate} Hz, seuil={thresholdDb} dB).");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ARMicDetectionManager] Erreur démarrage microphone : {ex.Message}");
        }
    }

    public void StopMicrophoneRecording()
    {
        if (!isMicrophoneRecording)
            return;

        try
        {
            Microphone.End(selectedDevice);
        }
        catch { }

        isMicrophoneRecording = false;
    }

    private void OnDisable()
    {
        StopMicrophoneRecording();
    }

    private void OnDestroy()
    {
        StopMicrophoneRecording();
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        // Le système micro n'est actif qu'au Niveau 4 (une fois débloqué par la 3ème énigme)
        if (!QuestARLevelManager.IsMicUnlocked)
        {
            AlertTimeRemaining = 0f;
            currentDecibels = -90f;
            return;
        }

        if (!isMicrophoneRecording)
        {
            RequestMicrophonePermissionAndStart();
        }

        timeSinceLastTrigger = Time.time - lastTriggerTime;

        if (AlertTimeRemaining > 0f)
        {
            AlertTimeRemaining -= Time.deltaTime;
            if (AlertTimeRemaining < 0f)
                AlertTimeRemaining = 0f;
        }

        // 1. Simulation PC via touche Espace
        CheckKeyboardSimulation();

        // 2. Traitement Audio du microphone
        ProcessMicrophoneAudio();
    }

    private void CheckKeyboardSimulation()
    {
        bool spaceTriggered = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            spaceTriggered = true;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space))
        {
            spaceTriggered = true;
        }
#endif

        if (spaceTriggered)
        {
            Debug.Log("[ARMicDetectionManager] Simulation touche Espace détectée !");
            TryTriggerLoudSound(true);
        }
    }

    private void ProcessMicrophoneAudio()
    {
        if (!isMicrophoneRecording || micClip == null)
            return;

        // Ignorer pendant que le joueur tire (pour ne pas capter le son du tir)
        if (Time.time < ignoreUntilTime)
        {
            sustainedLoudTimer = 0f;
            return;
        }

        int micPosition = Microphone.GetPosition(selectedDevice);
        if (micPosition < 0)
            return;

        int totalSamples = micClip.samples;
        if (totalSamples <= 0)
            return;

        int samplesToRead = Mathf.Min(sampleWindow, totalSamples);
        int startPosition = (micPosition - samplesToRead + totalSamples) % totalSamples;

        if (!micClip.GetData(sampleBuffer, startPosition))
            return;

        // Calcul de la puissance RMS pure
        float sum = 0f;
        for (int i = 0; i < samplesToRead; i++)
        {
            float s = sampleBuffer[i];
            sum += s * s;
        }

        currentRms = Mathf.Sqrt(sum / samplesToRead);

        // Pendant la 1ère seconde, calibrer le bruit de fond ambiant (silence)
        if (calibrationTimer > 0f)
        {
            calibrationTimer -= Time.deltaTime;
            noiseFloorRms = Mathf.Lerp(noiseFloorRms, Mathf.Clamp(currentRms, 0.001f, 0.03f), 8f * Time.deltaTime);
            currentDecibels = -80f;
            return;
        }

        // Adapter doucement le bruit de fond quand la pièce est calme
        if (currentRms < noiseFloorRms * 1.8f)
        {
            noiseFloorRms = Mathf.Lerp(noiseFloorRms, Mathf.Clamp(currentRms, 0.0015f, 0.025f), 2f * Time.deltaTime);
        }

        // Soustraire le bruit de fond ambiant pour que le silence donne toujours ~ -80 dB
        float netRms = Mathf.Max(0f, currentRms - noiseFloorRms * 1.35f);
        float amplified = netRms * Mathf.Max(0.1f, amplificationGain);
        currentDecibels = 20f * Mathf.Log10(Mathf.Max(amplified, 0.00001f));

        // Il faut que la voix dépasse le seuil ET soit maintenue au moins 0.14s (ignore les clics isolés)
        bool isClearlyAboveAmbient = currentRms > noiseFloorRms * 3.0f;
        if (currentDecibels >= thresholdDb && isClearlyAboveAmbient)
        {
            sustainedLoudTimer += Time.deltaTime;
            if (sustainedLoudTimer >= 0.14f)
            {
                sustainedLoudTimer = 0f;
                TryTriggerLoudSound(false);
            }
        }
        else
        {
            sustainedLoudTimer = Mathf.Max(0f, sustainedLoudTimer - Time.deltaTime * 2f);
        }
    }

    public bool TryTriggerLoudSound(bool forceBypassThreshold = false)
    {
        if (!QuestARLevelManager.IsMicUnlocked)
            return false;

        if (!forceBypassThreshold && Time.time - lastTriggerTime < cooldownSeconds)
            return false;

        lastTriggerTime = Time.time;
        AlertTimeRemaining = 5.0f;
        Debug.Log($"<color=#FF2222><b>[ARMicDetectionManager] VOIX / SON FORT DÉTECTÉ !</b> (Niveau : {currentDecibels:F1} dB / Seuil : {thresholdDb:F1} dB)</color>");
        OnLoudSound?.Invoke();
        return true;
    }

    public static void TriggerSoundManually()
    {
        if (Instance != null)
        {
            Instance.TryTriggerLoudSound(true);
        }
        else if (QuestARLevelManager.IsMicUnlocked)
        {
            AlertTimeRemaining = 5.0f;
            OnLoudSound?.Invoke();
        }
    }
}
