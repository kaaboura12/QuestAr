using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#if UNITY_ANDROID
using UnityEngine.XR.ARCore;
#endif

public class HamaFlashStunManager : MonoBehaviour
{
    public static HamaFlashStunManager Instance { get; private set; }

    [Header("AR Torch References")]
    public ARCameraManager arCameraManager;
    public ARSession arSession;

    [Header("Flash Stun Settings")]
    public float stunDuration = 5.0f;
    public Light cameraFlashSpotLight;
    public Image screenFlashOverlay;
    public Text statusText;
    public Text flashButtonText;

    [Header("Light Sensor (Capteur de Luminosité)")]
    [Tooltip("Seuil en Lux séparant une pièce sombre (Flash fort) d'une pièce éclairée (Flash doux).")]
    public float brightEnvironmentThresholdLux = 85f;
    [SerializeField] private float currentAmbientLux = 45f;
    [SerializeField] private bool isDarkEnvironment = true;
    private float lastCameraLux = 45f;
    private bool hasValidPhysicalReading = false;

    [Header("Cooldown Settings (Pause de 10s)")]
    public float cooldownDuration = 10.0f;
    [SerializeField] private float remainingCooldownTime = 0f;

    [Header("Player Health")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public RectTransform healthBarFill;

    private bool isFlashActive = false;
    private float remainingStunTime = 0f;
    private bool pausedArSessionForTorch = false;
    private Coroutine flashRoutine;
    private TextMeshProUGUI sensorBannerLabel;
    private GameObject sensorBannerObj;

    public bool IsFlashActive => isFlashActive;
    public float RemainingStunTime => remainingStunTime;
    public bool IsInCooldown => remainingCooldownTime > 0f;
    public float RemainingCooldownTime => remainingCooldownTime;
    public bool CanTriggerFlash => !isFlashActive && remainingCooldownTime <= 0f && QuestARLevelManager.IsFlashUnlocked;
    public float CurrentAmbientLux => currentAmbientLux;
    public bool IsDarkEnvironment => isDarkEnvironment;

    private void Awake()
    {
        Instance = this;
        currentHealth = maxHealth;

        if (arCameraManager == null)
        {
            arCameraManager = Object.FindAnyObjectByType<ARCameraManager>();
        }
        if (arCameraManager != null)
        {
            arCameraManager.requestedLightEstimation = LightEstimation.AmbientIntensity | LightEstimation.AmbientColor | LightEstimation.AmbientSphericalHarmonics;
        }
        if (arSession == null)
        {
            arSession = Object.FindAnyObjectByType<ARSession>();
        }

        if (cameraFlashSpotLight == null)
        {
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                GameObject spotObj = new GameObject("AR_Flashlight_SpotLight");
                spotObj.transform.SetParent(cam.transform, false);
                spotObj.transform.localPosition = Vector3.zero;
                spotObj.transform.localRotation = Quaternion.identity;
                cameraFlashSpotLight = spotObj.AddComponent<Light>();
                cameraFlashSpotLight.type = LightType.Spot;
                cameraFlashSpotLight.range = 28f;
                cameraFlashSpotLight.spotAngle = 68f;
                cameraFlashSpotLight.intensity = 3.5f;
                cameraFlashSpotLight.color = new Color(1f, 0.95f, 0.85f);
                cameraFlashSpotLight.enabled = false;
            }
        }

        if (cameraFlashSpotLight != null)
        {
            cameraFlashSpotLight.enabled = false;
        }

        if (screenFlashOverlay == null)
        {
            GameObject overlayCanvas = new GameObject("AR_ScreenFlash_Canvas");
            Canvas canvas = overlayCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            CanvasScaler scaler = overlayCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            GameObject imgObj = new GameObject("OverlayImage");
            imgObj.transform.SetParent(overlayCanvas.transform, false);
            RectTransform rt = imgObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            screenFlashOverlay = imgObj.AddComponent<Image>();
            screenFlashOverlay.color = new Color(1f, 1f, 1f, 0f);
            screenFlashOverlay.raycastTarget = false;

            BuildSensorBanner(overlayCanvas.transform);
        }
        else
        {
            screenFlashOverlay.color = new Color(1f, 1f, 1f, 0f);
        }
    }

    private void OnEnable()
    {
        if (arCameraManager == null)
            arCameraManager = Object.FindAnyObjectByType<ARCameraManager>();

        if (arCameraManager != null)
        {
            arCameraManager.requestedLightEstimation = LightEstimation.AmbientIntensity | LightEstimation.AmbientColor | LightEstimation.AmbientSphericalHarmonics;
            arCameraManager.frameReceived += OnCameraFrameReceived;
        }

        if (LightSensor.current != null && !LightSensor.current.enabled)
        {
            InputSystem.EnableDevice(LightSensor.current);
        }
    }

    private void Update()
    {
        // 1. Décompte du Cooldown de 10 secondes (Pause)
        if (remainingCooldownTime > 0f)
        {
            remainingCooldownTime -= Time.deltaTime;
            if (remainingCooldownTime <= 0f)
            {
                remainingCooldownTime = 0f;
            }
        }

        // 2. Lecture du capteur matériel de luminosité
        if (LightSensor.current != null)
        {
            if (!LightSensor.current.enabled)
                InputSystem.EnableDevice(LightSensor.current);

            float sensorLux = LightSensor.current.lightLevel.ReadValue();
            // Si le capteur matériel renvoie une vraie valeur positive non nulle
            if (sensorLux > 0.05f)
            {
                hasValidPhysicalReading = true;
                currentAmbientLux = Mathf.Lerp(currentAmbientLux, sensorLux, Time.deltaTime * 6f);
            }
            else
            {
                hasValidPhysicalReading = false;
            }
        }
        else
        {
            hasValidPhysicalReading = false;
        }

        // Fallback sécurisé : si le capteur physique n'a pas fourni de valeur (> 0), utiliser ARCore
        if (!hasValidPhysicalReading)
        {
            if (lastCameraLux > 0f)
            {
                currentAmbientLux = Mathf.Lerp(currentAmbientLux, lastCameraLux, Time.deltaTime * 4f);
            }
            else if (currentAmbientLux <= 1f)
            {
                currentAmbientLux = 45f;
            }
        }

        // Ne jamais laisser la valeur à 0 (valeur plancher de 10 Lux pour la pénombre)
        if (currentAmbientLux < 10f)
            currentAmbientLux = 10f;

        isDarkEnvironment = currentAmbientLux <= brightEnvironmentThresholdLux;
    }

    private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        float estimatedLux = -1f;
        if (args.lightEstimation.averageIntensityInLumens.HasValue && args.lightEstimation.averageIntensityInLumens.Value > 0f)
        {
            estimatedLux = args.lightEstimation.averageIntensityInLumens.Value;
        }
        else if (args.lightEstimation.averageBrightness.HasValue)
        {
            // averageBrightness varie de 0.0f (obscurité) à 1.0f (très éclairé)
            // ex: 0.1 -> ~25 Lux, 0.4 -> ~100 Lux, 0.8+ -> ~200+ Lux
            float b = args.lightEstimation.averageBrightness.Value;
            estimatedLux = Mathf.Clamp(b * 260f, 15f, 400f);
        }

        if (estimatedLux > 0f)
        {
            lastCameraLux = estimatedLux;
            if (!hasValidPhysicalReading)
            {
                currentAmbientLux = Mathf.Lerp(currentAmbientLux, estimatedLux, 0.35f);
            }
        }
    }

    private void BuildSensorBanner(Transform canvasTransform)
    {
        sensorBannerObj = new GameObject("LightSensor_Banner", typeof(RectTransform));
        sensorBannerObj.transform.SetParent(canvasTransform, false);
        RectTransform rt = sensorBannerObj.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -115f);
        rt.sizeDelta = new Vector2(780f, 54f);

        Image bg = sensorBannerObj.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.08f, 0.88f);
        bg.raycastTarget = false;

        GameObject lblObj = new GameObject("Text", typeof(RectTransform));
        lblObj.transform.SetParent(sensorBannerObj.transform, false);
        RectTransform lrt = lblObj.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        sensorBannerLabel = lblObj.AddComponent<TextMeshProUGUI>();
        sensorBannerLabel.fontSize = 22f;
        sensorBannerLabel.fontStyle = FontStyles.Bold;
        sensorBannerLabel.alignment = TextAlignmentOptions.Center;
        sensorBannerLabel.color = new Color(1f, 0.92f, 0.35f, 1f);
        sensorBannerLabel.raycastTarget = false;

        sensorBannerObj.SetActive(false);
    }

    public void TriggerFlashStun()
    {
        if (!CanTriggerFlash)
            return;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }
        flashRoutine = StartCoroutine(FlashStunCoroutine());
    }

    private IEnumerator FlashStunCoroutine()
    {
        isFlashActive = true;

        // Lecture du capteur de lumière du téléphone au moment du Flash
        bool darkRoom = currentAmbientLux <= brightEnvironmentThresholdLux;

        // Si pièce sombre (Dinya Dilma) -> Flash très puissant à l'écran et en 3D
        // Si pièce éclairée (Dinya Dawiya) -> Flash doux à l'écran (moch 9wi)
        float peakScreenAlpha = darkRoom ? 0.90f : 0.22f;
        float steadyScreenAlpha = darkRoom ? 0.28f : 0.04f;
        float targetSpotIntensity = darkRoom ? 6.5f : 1.6f;

        // 1. Request ARCore / AR Foundation hardware Torch ON + 3D Camera SpotLight
        SetARFoundationTorch(true);
        if (cameraFlashSpotLight == null)
        {
            Camera cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                GameObject spotObj = new GameObject("AR_Flashlight_SpotLight");
                spotObj.transform.SetParent(cam.transform, false);
                spotObj.transform.localPosition = Vector3.zero;
                spotObj.transform.localRotation = Quaternion.identity;
                cameraFlashSpotLight = spotObj.AddComponent<Light>();
                cameraFlashSpotLight.type = LightType.Spot;
                cameraFlashSpotLight.range = 30f;
                cameraFlashSpotLight.spotAngle = 70f;
                cameraFlashSpotLight.color = new Color(1f, 0.95f, 0.85f);
            }
        }
        if (cameraFlashSpotLight != null)
        {
            cameraFlashSpotLight.intensity = targetSpotIntensity;
            cameraFlashSpotLight.enabled = true;
        }

        if (sensorBannerObj != null && sensorBannerLabel != null)
        {
            sensorBannerObj.SetActive(true);
            string modeTxt = darkRoom
                ? $"CAPTEUR LUMIÈRE : {currentAmbientLux:F0} Lux (Obscurité -> FLASH PUISSANT)"
                : $"CAPTEUR LUMIÈRE : {currentAmbientLux:F0} Lux (Zone Éclairée -> FLASH DOUX)";
            sensorBannerLabel.text = modeTxt;
        }

        // 2. Freeze all zombies in the scene for 5 seconds
        HamaZombieAI[] zombies = Object.FindObjectsByType<HamaZombieAI>(FindObjectsInactive.Exclude);
        foreach (HamaZombieAI z in zombies)
        {
            if (z != null) z.SetStunned(true);
        }

        float remaining = stunDuration;
        bool checkedHardwareFallback = false;

        while (remaining > 0f)
        {
            float elapsed = stunDuration - remaining;

            // Recalculer dynamiquement selon le capteur de lumière en temps réel pendant les 5s
            darkRoom = currentAmbientLux <= brightEnvironmentThresholdLux;
            peakScreenAlpha = darkRoom ? 0.90f : 0.22f;
            steadyScreenAlpha = darkRoom ? 0.28f : 0.04f;
            if (cameraFlashSpotLight != null)
                cameraFlashSpotLight.intensity = darkRoom ? 6.5f : 1.6f;

            if (sensorBannerLabel != null)
            {
                sensorBannerLabel.text = darkRoom
                    ? $"CAPTEUR LUMIÈRE : {currentAmbientLux:F0} Lux (Obscurité -> FLASH FORT) [{remaining:F1}s]"
                    : $"CAPTEUR LUMIÈRE : {currentAmbientLux:F0} Lux (Éclairé -> FLASH DOUX) [{remaining:F1}s]";
            }

            if (!checkedHardwareFallback && elapsed >= 0.25f)
            {
                checkedHardwareFallback = true;
                if (!IsARFoundationTorchActive())
                {
                    StartCoroutine(EnsureAndroidHardwareTorchCoroutine());
                }
            }

            if (screenFlashOverlay != null)
            {
                float alpha = elapsed < 0.35f
                    ? Mathf.Lerp(peakScreenAlpha, steadyScreenAlpha, elapsed / 0.35f)
                    : steadyScreenAlpha;
                screenFlashOverlay.color = new Color(1f, 0.98f, 0.85f, alpha);
            }

            if (statusText != null)
            {
                statusText.text = "QuestAR - FLASH ACTIF : ZOMBIES STOP (" + remaining.ToString("F1") + "s)";
                statusText.color = new Color(1f, 0.95f, 0.3f, 1f);
            }
            if (flashButtonText != null)
            {
                flashButtonText.text = "FLASH ON\n(" + remaining.ToString("F1") + "s)";
            }
            remainingStunTime = remaining;
            remaining -= Time.deltaTime;
            yield return null;
        }

        // 3. Turn OFF hardware flashlight & unfreeze zombies after 5 seconds
        yield return StartCoroutine(DisableAllTorchModesCoroutine());

        if (cameraFlashSpotLight != null)
        {
            cameraFlashSpotLight.enabled = false;
        }
        if (screenFlashOverlay != null)
        {
            screenFlashOverlay.color = new Color(1f, 1f, 1f, 0f);
        }
        if (sensorBannerObj != null)
        {
            sensorBannerObj.SetActive(false);
        }

        zombies = Object.FindObjectsByType<HamaZombieAI>(FindObjectsInactive.Exclude);
        foreach (HamaZombieAI z in zombies)
        {
            if (z != null) z.SetStunned(false);
        }

        if (statusText != null)
        {
            statusText.text = "QuestAR - Survivre aux Zombies AR";
            statusText.color = new Color(0.45f, 0.95f, 1f, 1f);
        }
        if (flashButtonText != null)
        {
            flashButtonText.text = "FLASH (5s)\nSTOP ZOMBIES";
        }

        isFlashActive = false;
        remainingStunTime = 0f;
        flashRoutine = null;

        // Début du temps de pause / cooldown de 10 secondes
        remainingCooldownTime = cooldownDuration;
    }

    private void SetARFoundationTorch(bool enable)
    {
        if (arCameraManager == null)
        {
            arCameraManager = Object.FindAnyObjectByType<ARCameraManager>();
        }
        if (arCameraManager != null)
        {
            arCameraManager.requestedLightEstimation = LightEstimation.AmbientIntensity | LightEstimation.AmbientColor | LightEstimation.AmbientSphericalHarmonics;
        }
        if (arSession == null)
        {
            arSession = Object.FindAnyObjectByType<ARSession>();
        }

        XRCameraTorchMode targetMode = enable ? XRCameraTorchMode.On : XRCameraTorchMode.Off;

        if (arCameraManager != null)
        {
            arCameraManager.requestedCameraTorchMode = targetMode;
            if (arCameraManager.subsystem != null)
            {
                arCameraManager.subsystem.requestedCameraTorchMode = targetMode;
            }
        }

#if UNITY_ANDROID
        if (arSession != null && arSession.subsystem is ARCoreSessionSubsystem arCoreSubsystem)
        {
            arCoreSubsystem.SetConfigurationDirty();
        }
#endif
    }

    private bool IsARFoundationTorchActive()
    {
        if (arCameraManager != null && arCameraManager.currentCameraTorchMode == XRCameraTorchMode.On)
        {
            return true;
        }
        if (arCameraManager != null && arCameraManager.subsystem != null &&
            arCameraManager.subsystem.currentCameraTorchMode == XRCameraTorchMode.On)
        {
            return true;
        }
        return false;
    }

    private IEnumerator EnsureAndroidHardwareTorchCoroutine()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (TrySetAndroidCameraManagerTorch(true))
        {
            yield break;
        }

        SetARFoundationTorch(false);
        if (arCameraManager != null && arCameraManager.enabled)
        {
            arCameraManager.enabled = false;
        }
        if (arSession != null && arSession.enabled)
        {
            arSession.enabled = false;
            pausedArSessionForTorch = true;
        }

        for (int attempt = 0; attempt < 6; attempt++)
        {
            yield return new WaitForSeconds(0.12f);
            if (!isFlashActive) yield break;
            if (TrySetAndroidCameraManagerTorch(true))
            {
                yield break;
            }
        }
#else
        yield break;
#endif
    }

    private IEnumerator DisableAllTorchModesCoroutine()
    {
        SetARFoundationTorch(false);
        TrySetAndroidCameraManagerTorch(false);

        if (pausedArSessionForTorch)
        {
            pausedArSessionForTorch = false;
            yield return new WaitForSeconds(0.08f);
            if (arSession != null)
            {
                arSession.enabled = true;
            }
            if (arCameraManager != null)
            {
                arCameraManager.enabled = true;
            }
        }
    }

    private bool TrySetAndroidCameraManagerTorch(bool enable)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        bool anySuccess = false;
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext"))
            using (AndroidJavaObject cameraManager = context.Call<AndroidJavaObject>("getSystemService", "camera"))
            {
                string[] cameraIds = cameraManager.Call<string[]>("getCameraIdList");
                if (cameraIds != null)
                {
                    foreach (string id in cameraIds)
                    {
                        try
                        {
                            cameraManager.Call("setTorchMode", id, enable);
                            anySuccess = true;
                            break;
                        }
                        catch (System.Exception) { }
                    }
                }
            }
        }
        catch (System.Exception) { }
        return anySuccess;
#else
        return false;
#endif
    }

    public void DamagePlayer(float amount)
    {
        if (isFlashActive) return;
        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        if (healthBarFill != null)
        {
            float ratio = currentHealth / maxHealth;
            healthBarFill.anchorMax = new Vector2(0.04f + 0.84f * ratio, 0.82f);
        }

        var vitals = Object.FindAnyObjectByType<ARZombieVitals>();
        if (vitals != null)
        {
            vitals.ApplyDamage(amount);
        }
    }

    private void OnDisable()
    {
        if (arCameraManager != null)
        {
            arCameraManager.frameReceived -= OnCameraFrameReceived;
        }

        if (isFlashActive)
        {
            SetARFoundationTorch(false);
            TrySetAndroidCameraManagerTorch(false);
            if (pausedArSessionForTorch)
            {
                pausedArSessionForTorch = false;
                if (arSession != null) arSession.enabled = true;
                if (arCameraManager != null) arCameraManager.enabled = true;
            }
        }
    }
}
