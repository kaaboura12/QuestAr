using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Covers the menu, briefs the collapse, and reveals gameplay only after the scene is ready.
/// </summary>
public sealed class GameplayTransition : MonoBehaviour
{
    const float FadeInSeconds = 0.45f;
    const float LineSeconds = 1.15f;
    const float BreachSeconds = 0.7f;
    const float FadeOutSeconds = 0.4f;
    const float ReadyTimeoutSeconds = 12f;

    static readonly string[] Lines =
    {
        "The last cities are going dark.",
        "Zombies are walking through the smoke.",
        "The sky is splitting open.",
        "The world is collapsing.",
        "The streets no longer belong to the living.",
        "Hold your nerve.",
    };

    static bool running;

    static float Step()
    {
        return Mathf.Min(Time.unscaledDeltaTime, 0.05f);
    }

    string sceneName;
    LoadingScreenView view;

    public static void Begin(string targetScene)
    {
        if (running || string.IsNullOrEmpty(targetScene))
            return;

        running = true;
        GameplayReadiness.Reset();

        var host = new GameObject("Gameplay Transition");
        DontDestroyOnLoad(host);
        var transition = host.AddComponent<GameplayTransition>();
        transition.sceneName = targetScene;
    }

    void Start()
    {
        view = gameObject.AddComponent<LoadingScreenView>();
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return Fade(0f, 1f, FadeInSeconds);
        view.SetPhase("LISTENING");
        view.ShowLine(Lines[0]);

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (load == null)
        {
            Debug.LogError("GameplayTransition: Could not load " + sceneName);
            running = false;
            Destroy(gameObject);
            yield break;
        }

        load.allowSceneActivation = false;

        int line = 0;
        float lineTime = 0f;
        float shown = 0f;
        bool briefingDone = false;

        while (!briefingDone || load.progress < 0.9f)
        {
            float step = Step();
            if (!briefingDone)
            {
                lineTime += step;
                if (lineTime >= LineSeconds)
                {
                    lineTime = 0f;
                    line++;
                    if (line >= Lines.Length)
                    {
                        briefingDone = true;
                        view.SetPhase("SCENE STAGED");
                    }
                    else
                    {
                        view.ShowLine(Lines[line]);
                    }
                }
            }

            float loaded = Mathf.Clamp01(load.progress / 0.9f) * 0.9f;
            float spoken = briefingDone
                ? 1f
                : (line + Mathf.Clamp01(lineTime / LineSeconds)) / Lines.Length;
            float target = Mathf.Min(loaded, spoken * 0.9f);
            if (briefingDone && load.progress >= 0.9f)
                target = 0.92f;

            shown = Mathf.MoveTowards(shown, target, step * 1.6f);
            view.SetProgress(shown);
            yield return null;
        }

        view.SetPhase("BREACHING");
        view.ShowLine("They're already here.");
        float breach = 0f;
        while (breach < BreachSeconds)
        {
            float step = Step();
            breach += step;
            shown = Mathf.MoveTowards(shown, 1f, step * 1.1f);
            view.SetProgress(shown);
            yield return null;
        }

        load.allowSceneActivation = true;
        while (!load.isDone)
            yield return null;

        view.SetPhase("DEPLOYING");
        float waited = 0f;
        while (!GameplayReadiness.IsReady && waited < ReadyTimeoutSeconds)
        {
            waited += Step();
            yield return null;
        }

        view.SetProgress(1f);
        yield return Fade(view.RootGroup.alpha, 0f, FadeOutSeconds);

        running = false;
        Destroy(gameObject);
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Step();
            float u = Mathf.Clamp01(elapsed / duration);
            view.RootGroup.alpha = Mathf.Lerp(from, to, u);
            yield return null;
        }

        view.RootGroup.alpha = to;
    }
}
