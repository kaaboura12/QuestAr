using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZombieAR
{
    public class DarkVignetteController : MonoBehaviour
    {
        [Header("Lighting Settings (3D Objects Visibility)")]
        [Tooltip("Lumière directionnelle de la scène")]
        public Light directionalLight;

        [Tooltip("Intensité de la lumière (doit être suffisante pour que les zombies soient bien visibles !)")]
        [Range(0.5f, 2f)]
        public float sceneLightIntensity = 1.1f;

        [Tooltip("Couleur de la lumière directionnelle")]
        public Color sceneLightColor = new Color(0.95f, 0.95f, 1.0f);

        [Header("URP Volume & Vignette (Dark Ambience)")]
        [Tooltip("Volume URP de la scène")]
        public Volume globalVolume;

        [Header("Vignette Settings (Assombrit le fond AR sans noircir les zombies)")]
        [Tooltip("Intensité de la vignette pour assombrir le tour de l'écran")]
        [Range(0f, 1f)]
        public float baseVignetteIntensity = 0.42f;

        [Tooltip("Intensité max de la vignette pendant la pulsation d'alerte (5s)")]
        [Range(0f, 1f)]
        public float alertVignetteMaxIntensity = 0.70f;

        [Tooltip("Vitesse de battement/pulsation pendant l'alerte")]
        public float alertPulseSpeed = 5.0f;

        [Tooltip("Couleur d'alerte de la vignette (rouge sang)")]
        public Color alertVignetteColor = new Color(0.5f, 0.02f, 0.02f);

        private Vignette vignetteComponent;
        private bool isAlerting = false;
        private float alertTimer = 0f;

        private void Start()
        {
            ApplyCleanLighting();
            SetupVignette();

            if (MicLoudnessDetector.Instance != null)
            {
                MicLoudnessDetector.Instance.OnLoudSound += OnLoudSound;
            }
        }

        private void OnEnable()
        {
            if (MicLoudnessDetector.Instance != null)
            {
                MicLoudnessDetector.Instance.OnLoudSound -= OnLoudSound;
                MicLoudnessDetector.Instance.OnLoudSound += OnLoudSound;
            }
        }

        private void OnDisable()
        {
            if (MicLoudnessDetector.Instance != null)
            {
                MicLoudnessDetector.Instance.OnLoudSound -= OnLoudSound;
            }
        }

        /// <summary>
        /// Éclaire correctement la scène 3D pour que les zombies et objets restent parfaitement visibles.
        /// </summary>
        private void ApplyCleanLighting()
        {
            if (directionalLight == null)
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (var l in lights)
                {
                    if (l.type == LightType.Directional)
                    {
                        directionalLight = l;
                        break;
                    }
                }
            }

            if (directionalLight != null)
            {
                directionalLight.intensity = sceneLightIntensity;
                directionalLight.color = sceneLightColor;
            }

            // Éclairage d'ambiance doux et naturel pour ne pas avoir de silhouettes noires
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.48f);
        }

        private void SetupVignette()
        {
            if (globalVolume == null)
            {
                globalVolume = FindAnyObjectByType<Volume>();
            }

            if (globalVolume != null && globalVolume.profile != null)
            {
                if (!globalVolume.profile.TryGet(out vignetteComponent))
                {
                    vignetteComponent = globalVolume.profile.Add<Vignette>(true);
                }

                if (vignetteComponent != null)
                {
                    vignetteComponent.active = true;
                    vignetteComponent.color.overrideState = true;
                    vignetteComponent.color.value = Color.black;

                    vignetteComponent.intensity.overrideState = true;
                    vignetteComponent.intensity.value = baseVignetteIntensity;

                    vignetteComponent.smoothness.overrideState = true;
                    vignetteComponent.smoothness.value = 0.55f;
                }
            }
        }

        private void OnLoudSound(float db)
        {
            TriggerVignetteAlert(5.0f);
        }

        [ContextMenu("Trigger Vignette Alert (5s)")]
        public void TriggerVignetteAlert(float duration = 5.0f)
        {
            StopAllCoroutines();
            StartCoroutine(VignetteAlertRoutine(duration));
        }

        private IEnumerator VignetteAlertRoutine(float duration)
        {
            isAlerting = true;
            alertTimer = duration;

            while (alertTimer > 0f)
            {
                alertTimer -= Time.deltaTime;

                if (vignetteComponent != null)
                {
                    // Effet de battement de cœur angoissant
                    float t = Mathf.Sin(Time.time * alertPulseSpeed);
                    float normalizedT = (t + 1f) * 0.5f;

                    vignetteComponent.intensity.value = Mathf.Lerp(baseVignetteIntensity, alertVignetteMaxIntensity, normalizedT);
                    vignetteComponent.color.value = Color.Lerp(Color.black, alertVignetteColor, normalizedT);
                }

                yield return null;
            }

            // Transition douce de retour
            float recovery = 1.0f;
            while (recovery > 0f)
            {
                recovery -= Time.deltaTime * 2f;
                if (vignetteComponent != null)
                {
                    vignetteComponent.intensity.value = Mathf.Lerp(baseVignetteIntensity, alertVignetteMaxIntensity, Mathf.Max(0, recovery));
                    vignetteComponent.color.value = Color.Lerp(Color.black, alertVignetteColor, Mathf.Max(0, recovery));
                }
                yield return null;
            }

            if (vignetteComponent != null)
            {
                vignetteComponent.intensity.value = baseVignetteIntensity;
                vignetteComponent.color.value = Color.black;
            }

            isAlerting = false;
        }

        private void Update()
        {
            if (!isAlerting && vignetteComponent != null)
            {
                // Respiration subtile aux bords de l'écran
                float subtleBreath = Mathf.Sin(Time.time * 1.5f) * 0.025f;
                vignetteComponent.intensity.value = baseVignetteIntensity + subtleBreath;
            }
        }
    }
}
