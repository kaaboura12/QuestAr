using System.Collections;
using UnityEngine;
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

    [Header("Player Health")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public RectTransform healthBarFill;

    private bool isFlashActive = false;
    private float remainingStunTime = 0f;
    private bool pausedArSessionForTorch = false;
    private Coroutine flashRoutine;

    public bool IsFlashActive => isFlashActive;
    public float RemainingStunTime => remainingStunTime;

    private void Awake()
    {
        Instance = this;
        currentHealth = maxHealth;

        if (arCameraManager == null)
        {
            arCameraManager = Object.FindAnyObjectByType<ARCameraManager>();
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
                cameraFlashSpotLight.range = 25f;
                cameraFlashSpotLight.spotAngle = 65f;
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
        }
        else
        {
            screenFlashOverlay.color = new Color(1f, 1f, 1f, 0f);
        }
    }

    public void TriggerFlashStun()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }
        flashRoutine = StartCoroutine(FlashStunCoroutine());
    }

    private IEnumerator FlashStunCoroutine()
    {
        isFlashActive = true;

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
                cameraFlashSpotLight.intensity = 4.0f;
                cameraFlashSpotLight.color = new Color(1f, 0.95f, 0.85f);
            }
        }
        if (cameraFlashSpotLight != null)
        {
            cameraFlashSpotLight.enabled = true;
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

            // After 0.25s, verify if ARCore Torch turned on; if not, force Android CameraManager Torch
            if (!checkedHardwareFallback && elapsed >= 0.25f)
            {
                checkedHardwareFallback = true;
                if (!IsARFoundationTorchActive())
                {
                    StartCoroutine(EnsureAndroidHardwareTorchCoroutine());
                }
            }

            // Initial bright flash burst in the first 0.35s, then subtle torch glow
            if (screenFlashOverlay != null)
            {
                float alpha = elapsed < 0.35f ? Mathf.Lerp(0.65f, 0.08f, elapsed / 0.35f) : 0.08f;
                screenFlashOverlay.color = new Color(1f, 0.98f, 0.85f, alpha);
            }

            if (statusText != null)
            {
                statusText.text = "QuestAR - MOUHAMED (HAMA)\nFLASH ACTIF : ZOMBIES STOP (" + remaining.ToString("F1") + "s)";
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
        }
        else
        {
            screenFlashOverlay.color = new Color(1f, 1f, 1f, 0f);
        }

        zombies = Object.FindObjectsByType<HamaZombieAI>(FindObjectsInactive.Exclude);
        foreach (HamaZombieAI z in zombies)
        {
            if (z != null) z.SetStunned(false);
        }

        if (statusText != null)
        {
            statusText.text = "QuestAR - MOUHAMED (HAMA)\nMission 3 : Survivre aux Zombies AR";
            statusText.color = new Color(0.45f, 0.95f, 1f, 1f);
        }
        if (flashButtonText != null)
        {
            flashButtonText.text = "FLASH (5s)\nSTOP ZOMBIES";
        }

        isFlashActive = false;
        flashRoutine = null;
    }

    private void SetARFoundationTorch(bool enable)
    {
        if (arCameraManager == null)
        {
            arCameraManager = Object.FindAnyObjectByType<ARCameraManager>();
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
        // First attempt direct CameraManager.setTorchMode without pausing ARSession
        if (TrySetAndroidCameraManagerTorch(true))
        {
            yield break;
        }

        // If ARCore holds an exclusive camera lock and rejects setTorchMode, pause ARSession during the 5s freeze
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

        // Retry up to 6 times (max ~0.6s) while Android releases the camera lock
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
