
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIEmberEffect : MonoBehaviour
{
    [Header("Ember Settings")]
    public int emberCount = 35;
    public float riseSpeed = 35f;
    public float swayAmount = 18f;
    public float minSize = 3f;
    public float maxSize = 8f;

    private RectTransform area;
    private readonly List<RectTransform> embers = new();
    private readonly List<float> speeds = new();
    private readonly List<float> phases = new();

    private void Start()
    {
        area = GetComponent<RectTransform>();

        for (int i = 0; i < emberCount; i++)
        {
            GameObject ember = new GameObject("Ember");
            ember.transform.SetParent(transform, false);

            RectTransform rect = ember.AddComponent<RectTransform>();
            rect.sizeDelta = Vector2.one * Random.Range(minSize, maxSize);

            Image image = ember.AddComponent<Image>();
            image.color = new Color(1f, Random.Range(0.2f, 0.55f), 0.03f,
                Random.Range(0.35f, 0.9f));
            image.raycastTarget = false;

            SetRandomPosition(rect);

            embers.Add(rect);
            speeds.Add(Random.Range(riseSpeed * 0.5f, riseSpeed * 1.5f));
            phases.Add(Random.Range(0f, 10f));
        }
    }

    private void Update()
    {
        if (area == null) return;

        for (int i = 0; i < embers.Count; i++)
        {
            RectTransform ember = embers[i];
            Vector2 pos = ember.anchoredPosition;

            pos.y += speeds[i] * Time.deltaTime;
            pos.x += Mathf.Sin(Time.time * 1.2f + phases[i])
                     * swayAmount * Time.deltaTime;

            ember.anchoredPosition = pos;

            if (pos.y > area.rect.height / 2f + 20f)
                SetRandomPosition(ember);
        }
    }

    private void SetRandomPosition(RectTransform ember)
    {
        float width = area.rect.width;
        float height = area.rect.height;

        ember.anchoredPosition = new Vector2(
            Random.Range(-width / 2f, width / 2f),
            Random.Range(-height / 2f, height / 2f)
        );
    }
}
