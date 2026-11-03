using System.Collections;
using UnityEngine;
using TMPro;

// Bandeau d'histoire en bas de l'écran : apparaît en fondu, reste, puis disparaît (4 secondes en tout).
// Utilisation depuis n'importe quel script : BandeauHistoire.Afficher("texte");
public class BandeauHistoire : MonoBehaviour
{
    const float Duree = 4f;
    static readonly Color Os = new Color(0.929f, 0.902f, 0.855f);          // blanc os
    static readonly Color OmbreRouge = new Color(0.35f, 0f, 0.06f, 1f);    // ombre rouge sombre

    static BandeauHistoire instance;
    TextMeshProUGUI texte;
    Coroutine encours;

    public static void Afficher(string message)
    {
        if (instance == null)
        {
            instance = FindAnyObjectByType<BandeauHistoire>();
            if (instance == null) instance = new GameObject("BandeauHistoire").AddComponent<BandeauHistoire>();
        }
        instance.Montrer(message);
    }

    void Awake()
    {
        instance = this;
        Construire();
    }

    void Start()
    {
        Montrer(TextesEnigme.Lancement);   // message du début de la scène
    }

    // Crée le Canvas et le texte (au-dessus des autres canvas pour rester visible)
    void Construire()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        GameObject go = new GameObject("TexteBandeau");
        go.transform.SetParent(transform, false);
        texte = go.AddComponent<TextMeshProUGUI>();
        texte.fontSize = 44;
        texte.fontStyle = FontStyles.Italic;
        texte.alignment = TextAlignmentOptions.Center;
        texte.color = new Color(Os.r, Os.g, Os.b, 0f);
        texte.raycastTarget = false;

        // Ombre rouge sombre (effet "Underlay" de TextMeshPro)
        Material m = texte.fontMaterial;
        m.EnableKeyword("UNDERLAY_ON");
        m.SetColor("_UnderlayColor", OmbreRouge);
        m.SetFloat("_UnderlayOffsetX", 1f);
        m.SetFloat("_UnderlayOffsetY", -1f);
        m.SetFloat("_UnderlaySoftness", 0.3f);

        RectTransform rt = texte.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 60f);
        rt.sizeDelta = new Vector2(-120f, 200f);
    }

    void Montrer(string message)
    {
        if (texte == null) return;
        if (encours != null) StopCoroutine(encours);
        texte.text = message;
        texte.fontSize = 44f * ProgressManager.FacteurTexte;   // réglage "Taille du texte"
        encours = StartCoroutine(Fondu());
    }

    // 0,5 s d'apparition + 3 s affiché + 0,5 s de disparition = 4 s
    IEnumerator Fondu()
    {
        float t = 0f;
        while (t < Duree)
        {
            t += Time.deltaTime;
            float a = Mathf.Min(t / 0.5f, (Duree - t) / 0.5f);
            texte.color = new Color(Os.r, Os.g, Os.b, Mathf.Clamp01(a));
            yield return null;
        }
        texte.color = new Color(Os.r, Os.g, Os.b, 0f);
        encours = null;
    }
}
