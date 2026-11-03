using System;
using UnityEngine;

/// <summary>
/// Triggers weapon-specific haptic vibration on mobile devices.
/// Assault rifle shots trigger a heavy, sustained vibration pulse; pistol shots trigger a crisp, distinct tap.
/// Uses AudioAttributes with USAGE_GAME (14) so vibrations fire even if system touch haptics are disabled.
/// Full multi-tier fallback: VibrationEffect with USAGE_GAME -> Pattern with USAGE_GAME -> Handheld.Vibrate.
/// </summary>
public static class ARMobileShotHaptics
{
    // Assault Rifle (heavy): powerful pulse sustaining a heavy rumble across automatic bursts.
    const long HeavyShotDurationMs = 140L;
    const int HeavyShotAmplitude = 255;

    // Pistol (light): crisp, snappy tap with enough duration for any mobile motor to spin up.
    const long LightShotDurationMs = 60L;
    const int LightShotAmplitude = 125;

    const int DefaultAmplitude = -1; // android.os.VibrationEffect.DEFAULT_AMPLITUDE
    const int UsageGame = 14;         // android.media.AudioAttributes.USAGE_GAME
    const int ContentTypeSonification = 4; // android.media.AudioAttributes.CONTENT_TYPE_SONIFICATION

    static bool initialized;
    static AndroidJavaObject vibrator;
    static AndroidJavaClass vibrationEffectClass;
    static AndroidJavaObject gameAudioAttributes;
    static bool supportsAmplitudeControl;
    static int androidSdkInt;

    public static void PulseShot(bool heavy = true)
    {
#if UNITY_EDITOR
        Debug.Log($"[ARMobileShotHaptics] Shot vibration: {(heavy ? "HEAVY (Assault Rifle)" : "LIGHT (Pistol)")}");
        return;
#endif

        if (!Application.isMobilePlatform)
            return;

#if UNITY_ANDROID
        EnsureInitialized();

        long durationMs = heavy ? HeavyShotDurationMs : LightShotDurationMs;
        int amplitude = heavy ? HeavyShotAmplitude : LightShotAmplitude;
        bool vibrationTriggered = false;

        // Tier 1: Modern Android VibrationEffect with USAGE_GAME (API 26+)
        if (vibrator != null && vibrationEffectClass != null && androidSdkInt >= 26)
        {
            try
            {
                int targetAmp = supportsAmplitudeControl ? amplitude : DefaultAmplitude;
                using (var effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                           "createOneShot",
                           durationMs,
                           targetAmp))
                {
                    if (effect != null)
                    {
                        var audioAttr = GetGameAudioAttributes();
                        if (audioAttr != null)
                        {
                            vibrator.Call("vibrate", effect, audioAttr);
                            vibrationTriggered = true;
                        }
                        else
                        {
                            vibrator.Call("vibrate", effect);
                            vibrationTriggered = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ARMobileShotHaptics] VibrationEffect Tier 1 failed: " + ex.Message);
            }
        }

        // Tier 2: Pattern vibration with USAGE_GAME (works across all Android versions and OEMs)
        if (!vibrationTriggered && vibrator != null)
        {
            try
            {
                long[] pattern = new long[] { 0, durationMs };
                var audioAttr = GetGameAudioAttributes();
                if (audioAttr != null && androidSdkInt >= 21)
                {
                    vibrator.Call("vibrate", pattern, -1, audioAttr);
                    vibrationTriggered = true;
                }
                else
                {
                    vibrator.Call("vibrate", pattern, -1);
                    vibrationTriggered = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ARMobileShotHaptics] Pattern Tier 2 failed: " + ex.Message);
            }
        }

        // Tier 3: Classic simple vibrator.vibrate(milliseconds)
        if (!vibrationTriggered && vibrator != null)
        {
            try
            {
                vibrator.Call("vibrate", durationMs);
                vibrationTriggered = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ARMobileShotHaptics] Classic Tier 3 failed: " + ex.Message);
            }
        }

        // Tier 4: Unity native engine vibration fallback
        if (!vibrationTriggered)
        {
            try
            {
                Handheld.Vibrate();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ARMobileShotHaptics] Handheld.Vibrate Tier 4 failed: " + ex.Message);
            }
        }
#elif UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    static AndroidJavaObject GetGameAudioAttributes()
    {
        if (gameAudioAttributes != null)
            return gameAudioAttributes;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (androidSdkInt < 21)
            return null;

        try
        {
            using (var builder = new AndroidJavaObject("android.media.AudioAttributes$Builder"))
            {
                builder.Call<AndroidJavaObject>("setUsage", UsageGame);
                builder.Call<AndroidJavaObject>("setContentType", ContentTypeSonification);
                gameAudioAttributes = builder.Call<AndroidJavaObject>("build");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ARMobileShotHaptics] Failed to build AudioAttributes: " + ex.Message);
        }
#endif
        return gameAudioAttributes;
    }

    static void EnsureInitialized()
    {
        if (initialized)
            return;

        initialized = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                androidSdkInt = version.GetStatic<int>("SDK_INT");

            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                if (activity != null)
                {
                    try
                    {
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[ARMobileShotHaptics] getSystemService('vibrator') exception: " + ex.Message);
                    }

                    if (vibrator == null && androidSdkInt >= 31)
                    {
                        try
                        {
                            using (var manager = activity.Call<AndroidJavaObject>("getSystemService", "vibrator_manager"))
                            {
                                if (manager != null)
                                    vibrator = manager.Call<AndroidJavaObject>("getDefaultVibrator");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("[ARMobileShotHaptics] VibratorManager exception: " + ex.Message);
                        }
                    }
                }
            }

            if (vibrator != null)
            {
                try
                {
                    if (!vibrator.Call<bool>("hasVibrator"))
                        Debug.LogWarning("[ARMobileShotHaptics] Device reports hasVibrator = false");
                }
                catch { }

                if (androidSdkInt >= 26)
                {
                    try
                    {
                        vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    }
                    catch { }

                    try
                    {
                        supportsAmplitudeControl = vibrator.Call<bool>("hasAmplitudeControl");
                    }
                    catch
                    {
                        supportsAmplitudeControl = false;
                    }
                }
            }

            GetGameAudioAttributes();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ARMobileShotHaptics] Initialization failed: " + ex.Message);
        }
#endif
    }
}
