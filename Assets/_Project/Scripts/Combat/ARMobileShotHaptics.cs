using System;
using UnityEngine;

/// <summary>
/// A short hit on the phone vibrator for each real shot. Dry fire stays silent.
/// The Android library in Plugins/Android grants the vibrate permission.
/// </summary>
public static class ARMobileShotHaptics
{
    const long ShotDurationMs = 40L;
    const int ShotAmplitude = 255;

    static bool ready;
    static bool unavailable;
    static AndroidJavaObject vibrator;
    static AndroidJavaClass vibrationEffect;

    public static void PulseShot()
    {
        if (unavailable || Application.platform != RuntimePlatform.Android)
            return;

        try
        {
            if (!ready)
                Prepare();

            if (unavailable || vibrator == null || vibrationEffect == null)
                return;

            using (var effect = vibrationEffect.CallStatic<AndroidJavaObject>(
                       "createOneShot",
                       ShotDurationMs,
                       ShotAmplitude))
            {
                vibrator.Call("cancel");
                vibrator.Call("vibrate", effect);
            }
        }
        catch (Exception exception)
        {
            unavailable = true;
            Debug.LogWarning("Shot vibration stopped. " + exception.Message);
        }
    }

    static void Prepare()
    {
        ready = true;

        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            if (activity == null)
            {
                unavailable = true;
                return;
            }

            int sdk;
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                sdk = version.GetStatic<int>("SDK_INT");

            if (sdk >= 31)
            {
                using (var manager = activity.Call<AndroidJavaObject>("getSystemService", "vibrator_manager"))
                {
                    if (manager == null)
                    {
                        unavailable = true;
                        return;
                    }

                    vibrator = manager.Call<AndroidJavaObject>("getDefaultVibrator");
                }
            }
            else
            {
                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
        }

        if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
        {
            unavailable = true;
            vibrator = null;
            return;
        }

        vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect");
    }
}
