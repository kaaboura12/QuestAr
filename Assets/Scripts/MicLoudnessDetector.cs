using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace ZombieAR
{
    public class MicLoudnessDetector : MonoBehaviour
    {
        public static MicLoudnessDetector Instance { get; private set; }

        [Header("Microphone Settings")]
        [Tooltip("Nom du micro (laisser vide pour le micro par défaut)")]
        public string deviceName = null;

        [Tooltip("Taille de la fenêtre d'échantillons audio")]
        public int sampleWindow = 256;

        [Tooltip("Amplification du signal micro (très utile sur smartphone)")]
        [Range(1f, 10f)]
        public float micSensitivityGain = 3.0f;

        [Header("Detection Threshold")]
        [Tooltip("Seuil de déclenchement en décibels (-70 dB = très sensible / voix basse, -30 dB = voix très forte)")]
        [Range(-80f, 0f)]
        public float thresholdDb = -60f;

        [Tooltip("Délai de rechargement minimal avant une nouvelle détection (en secondes)")]
        public float triggerCooldown = 6.0f;

        [Header("Sound Effect on Alert")]
        public AudioSource audioSource;
        public AudioClip alertScreamClip;

        [Header("Live Metrics (Read Only)")]
        [SerializeField] private float currentRms = 0f;
        [SerializeField] private float currentDb = -80f;
        [SerializeField] private bool isRecording = false;
        [SerializeField] private bool permissionGranted = false;

        public float CurrentDb => currentDb;
        public float CurrentRms => currentRms;
        public bool IsRecording => isRecording;
        public bool PermissionGranted => permissionGranted;

        public event Action<float> OnLoudSound;

        private AudioClip micClip;
        private float[] audioSamples;
        private float lastTriggerTime = -999f;
        private Coroutine initCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            if (alertScreamClip == null)
            {
                alertScreamClip = GenerateZombieRoarClip();
            }
        }

        private void Start()
        {
            audioSamples = new float[sampleWindow];
            initCoroutine = StartCoroutine(RequestPermissionAndInitRoutine());
        }

        private IEnumerator RequestPermissionAndInitRoutine()
        {
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Debug.Log("[MicLoudnessDetector] Demande d'autorisation Microphone sur Android...");
                Permission.RequestUserPermission(Permission.Microphone);

                float waitTimeout = 12f;
                while (!Permission.HasUserAuthorizedPermission(Permission.Microphone) && waitTimeout > 0f)
                {
                    waitTimeout -= Time.unscaledDeltaTime;
                    yield return new WaitForSecondsRealtime(0.4f);
                }
            }
            permissionGranted = Permission.HasUserAuthorizedPermission(Permission.Microphone);
#elif UNITY_IOS
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            permissionGranted = Application.HasUserAuthorization(UserAuthorization.Microphone);
#else
            permissionGranted = true;
            yield return null;
#endif

            yield return new WaitForSecondsRealtime(0.2f);
            StartMicrophoneRecording();
        }

        public void StartMicrophoneRecording()
        {
            try
            {
                if (Microphone.devices.Length == 0)
                {
                    Debug.LogWarning("[MicLoudnessDetector] Aucun microphone disponible pour le moment.");
                    return;
                }

                if (string.IsNullOrEmpty(deviceName))
                {
                    deviceName = Microphone.devices[0];
                }

                int sampleRate = AudioSettings.outputSampleRate;
                if (sampleRate <= 0) sampleRate = 44100;

                // Si déjà en train d'enregistrer, arrêter d'abord
                if (Microphone.IsRecording(deviceName))
                {
                    Microphone.End(deviceName);
                }

                micClip = Microphone.Start(deviceName, true, 10, sampleRate);
                isRecording = Microphone.IsRecording(deviceName);
                Debug.Log($"[MicLoudnessDetector] Micro opérationnel sur '{deviceName}', rate: {sampleRate}Hz");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MicLoudnessDetector] Exception initialisation micro: {ex.Message}");
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && (!isRecording || micClip == null))
            {
                if (initCoroutine != null) StopCoroutine(initCoroutine);
                initCoroutine = StartCoroutine(RequestPermissionAndInitRoutine());
            }
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TriggerManually();
                return;
            }
#endif

            if (!isRecording || micClip == null)
            {
                currentDb = -80f;
                currentRms = 0f;
                return;
            }

            try
            {
                int micPosition = Microphone.GetPosition(deviceName);
                if (micPosition < sampleWindow) return;

                int startPos = micPosition - sampleWindow;
                if (startPos < 0) startPos = 0;

                if (!micClip.GetData(audioSamples, startPos)) return;

                float sum = 0f;
                for (int i = 0; i < sampleWindow; i++)
                {
                    float val = audioSamples[i] * micSensitivityGain;
                    sum += val * val;
                }

                currentRms = Mathf.Sqrt(sum / sampleWindow);
                // Calcul des décibels avec seuil minimum réaliste
                currentDb = (currentRms > 0.000001f) ? 20f * Mathf.Log10(currentRms) : -80f;

                if (currentDb >= thresholdDb && (Time.time - lastTriggerTime) >= triggerCooldown)
                {
                    TriggerLoudSound(currentDb);
                }
            }
            catch
            {
                // Gestion silencieuse des décalages de buffer transitoires
            }
        }

        public void TriggerManually()
        {
            if ((Time.time - lastTriggerTime) >= 1.0f)
            {
                Debug.Log("[MicLoudnessDetector] Déclenchement manuel !");
                TriggerLoudSound(0f);
            }
        }

        private void TriggerLoudSound(float db)
        {
            lastTriggerTime = Time.time;
            Debug.Log($"<color=orange>[MicLoudnessDetector] CRI / BRUIT DÉTECTÉ ! dB: {db:F1}</color>");

            if (audioSource != null && alertScreamClip != null)
            {
                audioSource.PlayOneShot(alertScreamClip, 1.0f);
            }

            OnLoudSound?.Invoke(db);
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(deviceName) && Microphone.IsRecording(deviceName))
            {
                Microphone.End(deviceName);
                isRecording = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private AudioClip GenerateZombieRoarClip()
        {
            int sampleRate = 44100;
            float duration = 3.0f;
            int totalSamples = (int)(sampleRate * duration);
            float[] data = new float[totalSamples];
            var rand = new System.Random(42);

            float phase1 = 0f;
            float phase2 = 0f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin((t / duration) * Mathf.PI);

                float freq1 = 70f + 25f * Mathf.Sin(t * 14f) + (float)(rand.NextDouble() * 15f);
                float freq2 = 135f + 40f * Mathf.Sin(t * 8f);

                phase1 += 2f * Mathf.PI * freq1 / sampleRate;
                phase2 += 2f * Mathf.PI * freq2 / sampleRate;

                float wave = Mathf.Sin(phase1) * 0.55f + Mathf.Sin(phase2) * 0.3f;
                float noise = ((float)rand.NextDouble() * 2f - 1f) * 0.2f;

                data[i] = Mathf.Clamp((wave + noise) * env * 0.9f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ZombieRoar_Synthesized", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
