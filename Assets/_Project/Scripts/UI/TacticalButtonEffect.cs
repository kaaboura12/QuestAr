
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class TacticalButtonEffect : MonoBehaviour
{
    public Color normalColor = new Color(0.07f, 0.07f, 0.07f, 0.94f);
    public Color highlightColor = new Color(0.24f, 0.055f, 0.045f, 1f);
    public Color accentColor = new Color(0.9f, 0.12f, 0.08f, 1f);

    private Image background;
    private Button button;
    private RectTransform rect;
    private Vector3 originalScale;
    private bool selected;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        background = GetComponent<Image>();
        button = GetComponent<Button>();

        originalScale = rect.localScale;

        if (background != null)
            background.color = normalColor;

        if (button != null)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.12f;
            button.colors = colors;
        }
    }

    private void Update()
    {
        if (rect == null || button == null)
            return;

        bool hovered = button.IsHighlighted();
        bool pressed = button.IsPressed();

        Color targetColor = hovered ? highlightColor : normalColor;
        if (background != null)
            background.color = Color.Lerp(
                background.color, targetColor, Time.unscaledDeltaTime * 9f);

        float targetScale = pressed ? 0.96f : hovered ? 1.025f : 1f;
        rect.localScale = Vector3.Lerp(
            rect.localScale,
            originalScale * targetScale,
            Time.unscaledDeltaTime * 12f);
    }

    public void PlayPressEffect()
    {
        StopAllCoroutines();
        StartCoroutine(PressAnimation());
    }

    private IEnumerator PressAnimation()
    {
        rect.localScale = originalScale * 0.94f;
        yield return new WaitForSecondsRealtime(0.07f);
        rect.localScale = originalScale;
    }
}
