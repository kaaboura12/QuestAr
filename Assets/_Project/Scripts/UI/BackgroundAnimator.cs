
using UnityEngine;

public class BackgroundAnimator : MonoBehaviour
{
    [Header("Zoom Animation")]
    public float zoomAmount = 0.035f;
    public float zoomSpeed = 0.25f;

    [Header("Movement Animation")]
    public float movementAmount = 8f;
    public float movementSpeed = 0.15f;

    [Header("Screen Coverage")]
    public float extraScale = 1.12f;

    private RectTransform rectTransform;
    private Vector3 originalScale;
    private Vector2 originalPosition;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        // Keep the background larger than the screen.
        originalScale = rectTransform.localScale * extraScale;
        originalPosition = rectTransform.anchoredPosition;

        rectTransform.localScale = originalScale;
    }

    private void Update()
    {
        float time = Time.time;

        // Smooth cinematic zoom.
        float zoom = 1f + Mathf.Sin(time * zoomSpeed) * zoomAmount;
        rectTransform.localScale = originalScale * zoom;

        // Gentle movement.
        float x = Mathf.Sin(time * movementSpeed) * movementAmount;
        float y = Mathf.Cos(time * movementSpeed * 0.7f) * movementAmount;

        rectTransform.anchoredPosition =
            originalPosition + new Vector2(x, y);
    }
}
