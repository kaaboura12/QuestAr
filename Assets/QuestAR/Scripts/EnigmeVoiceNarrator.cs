using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Narrateur vocal (Text-To-Speech) pour la scène d'énigme de Souhaila (Mission5_Enigme) :
/// Lit à haute voix les textes de l'énigme, les chroniques des reliques et la récompense
/// sur Android (via android.speech.tts.TextToSpeech) et sur PC Windows (via SpeechSynthesizer).
/// </summary>
public class EnigmeVoiceNarrator : MonoBehaviour
{
    private static EnigmeVoiceNarrator instance;
    private string lastSpokenText = "";

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject ttsObject;
    private bool ttsReady = false;

    private class TtsInitListener : AndroidJavaProxy
    {
        private readonly Action<int> onInitCallback;
        public TtsInitListener(Action<int> callback) : base("android.speech.tts.TextToSpeech$OnInitListener")
        {
            onInitCallback = callback;
        }

        public void onInit(int status)
        {
            onInitCallback?.Invoke(status);
        }
    }
#endif

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private System.Diagnostics.Process activeSpeechProcess;
#endif

    public static void LireTexte(string texte, bool prioritaire = false)
    {
        if (string.IsNullOrWhiteSpace(texte))
            return;

        if (instance == null)
        {
            instance = FindAnyObjectByType<EnigmeVoiceNarrator>();
            if (instance == null)
            {
                var go = new GameObject("EnigmeVoiceNarrator");
                instance = go.AddComponent<EnigmeVoiceNarrator>();
            }
        }

        instance.SpeakInternal(texte, prioritaire);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        InitTtsEngine();
        BuildRepeatSpeechButton();
    }

    private void OnDestroy()
    {
        StopSpeaking();
#if UNITY_ANDROID && !UNITY_EDITOR
        if (ttsObject != null)
        {
            try
            {
                ttsObject.Call("stop");
                ttsObject.Call("shutdown");
                ttsObject.Dispose();
            }
            catch { }
            ttsObject = null;
        }
#endif
        if (instance == this)
            instance = null;
    }

    private void InitTtsEngine()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                ttsObject = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, new TtsInitListener(status =>
                {
                    if (status == 0 && ttsObject != null) // TextToSpeech.SUCCESS == 0
                    {
                        try
                        {
                            using (AndroidJavaClass localeClass = new AndroidJavaClass("java.util.Locale"))
                            {
                                AndroidJavaObject frenchLocale = localeClass.GetStatic<AndroidJavaObject>("FRENCH");
                                ttsObject.Call<int>("setLanguage", frenchLocale);
                            }
                            ttsObject.Call<int>("setPitch", 0.88f);
                            ttsObject.Call<int>("setSpeechRate", 0.96f);
                            ttsReady = true;

                            if (!string.IsNullOrEmpty(lastSpokenText))
                            {
                                SpeakAndroid(lastSpokenText, true);
                            }
                        }
                        catch { }
                    }
                }));
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[EnigmeVoiceNarrator] Initialisation TTS Android ignorée : " + ex.Message);
        }
#endif
    }

    private void SpeakInternal(string rawText, bool interrupt)
    {
        if (ProgressManager.Data != null && !ProgressManager.Data.son)
            return;

        // Nettoyer les balises RichText éventuelles (<color=...>, etc.)
        string clean = System.Text.RegularExpressions.Regex.Replace(rawText, "<.*?>", string.Empty).Trim();
        if (string.IsNullOrEmpty(clean))
            return;

        lastSpokenText = clean;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (ttsReady && ttsObject != null)
        {
            SpeakAndroid(clean, interrupt);
        }
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        SpeakWindows(clean, interrupt);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void SpeakAndroid(string cleanText, bool interrupt)
    {
        try
        {
            int queueMode = interrupt ? 0 : 1; // QUEUE_FLUSH = 0, QUEUE_ADD = 1
            ttsObject.Call<int>("speak", cleanText, queueMode, null, "questar_utt_" + Time.frameCount);
        }
        catch { }
    }
#endif

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private void SpeakWindows(string cleanText, bool interrupt)
    {
        try
        {
            if (interrupt)
            {
                StopSpeaking();
            }

            string escaped = cleanText.Replace("'", " ").Replace("\"", " ").Replace("\r", " ").Replace("\n", ". ");
            string psCommand =
                $"Add-Type -AssemblyName System.Speech; " +
                $"$s = New-Object System.Speech.Synthesis.SpeechSynthesizer; " +
                $"$s.Rate = 0; " +
                $"$s.Speak('{escaped}');";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{psCommand}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            activeSpeechProcess = System.Diagnostics.Process.Start(psi);
        }
        catch { }
    }
#endif

    public void StopSpeaking()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (ttsObject != null && ttsReady)
        {
            try { ttsObject.Call<int>("stop"); } catch { }
        }
#endif
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        try
        {
            if (activeSpeechProcess != null && !activeSpeechProcess.HasExited)
            {
                activeSpeechProcess.Kill();
                activeSpeechProcess.Dispose();
            }
        }
        catch { }
        activeSpeechProcess = null;
#endif
    }

    private void BuildRepeatSpeechButton()
    {
        Canvas c = UIOutils.NouveauCanvas("CanvasNarrateurVocal", 25);
        c.transform.SetParent(transform, false);
        RectTransform zone = UIOutils.ZoneSure(c.transform);

        Button btn = UIOutils.Bouton(zone, "VOIX : LIRE", Vector2.zero, new Vector2(260f, 110f), () =>
        {
            string txt = !string.IsNullOrEmpty(lastSpokenText) ? lastSpokenText : TextesEnigme.EnigmeCourante();
            SpeakInternal(txt, true);
        }, 30f);

        RectTransform rt = (RectTransform)btn.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(24f, -150f);
    }
}
