using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Réglages sauvegardés : Effets réduits, Son, Vibrations, Taille du texte.
// Bouton RÉGLAGES sous le bouton GRIMOIRE (en haut à droite).
public class ReglagesUI : MonoBehaviour
{
    GameObject panneau;
    TextMeshProUGUI txtEffets, txtSon, txtVibrations, txtTaille;

    void Start()
    {
        Canvas cb = UIOutils.NouveauCanvas("CanvasReglagesBouton", -4);
        cb.transform.SetParent(transform, false);
        RectTransform zone = UIOutils.ZoneSure(cb.transform);
        Button b = UIOutils.Bouton(zone, "RÉGLAGES", Vector2.zero, new Vector2(240f, 150f), Ouvrir, 34f);
        RectTransform rt = (RectTransform)b.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-150f, -280f);

        Canvas cp = UIOutils.NouveauCanvas("CanvasReglages", 40);
        cp.transform.SetParent(transform, false);
        panneau = cp.gameObject;
        UIOutils.Rect("Fond", cp.transform, Vector2.zero, new Vector2(3000f, 3000f), new Color(0.03f, 0.03f, 0.03f, 0.97f), true);
        RectTransform z = UIOutils.ZoneSure(cp.transform);
        UIOutils.Texte(z, "RÉGLAGES", 64f, new Vector2(0f, 700f), new Vector2(800f, 100f)).fontStyle = FontStyles.Bold;

        txtEffets = Ligne(z, 450f, () => { Data.effetsReduits = !Data.effetsReduits; Appliquer(); });
        txtSon = Ligne(z, 250f, () => { Data.son = !Data.son; Appliquer(); });
        txtVibrations = Ligne(z, 50f, () => { Data.vibrations = !Data.vibrations; Appliquer(); });
        txtTaille = Ligne(z, -150f, () => { Data.texteGrand = !Data.texteGrand; Appliquer(); UIOutils.AppliquerTailleTexte(); });
        UIOutils.Texte(z, "Effets réduits : supprime secousses, scintillements et pulsations.", 34f, new Vector2(0f, -330f), new Vector2(880f, 120f));
        UIOutils.Bouton(z, "FERMER", new Vector2(0f, -700f), new Vector2(400f, 150f), () => panneau.SetActive(false), 44f);

        panneau.SetActive(false);
    }

    static SauvegardeQuestAR Data { get { return ProgressManager.Data; } }

    // Un gros bouton par réglage ; le libellé indique l'état en toutes lettres (pas seulement une couleur)
    TextMeshProUGUI Ligne(Transform parent, float y, UnityEngine.Events.UnityAction action)
    {
        Button b = UIOutils.Bouton(parent, "", new Vector2(0f, y), new Vector2(860f, 150f), action, 44f);
        return b.GetComponentInChildren<TextMeshProUGUI>();
    }

    void Ouvrir()
    {
        Rafraichir();
        panneau.SetActive(true);
    }

    void Appliquer()
    {
        ProgressManager.Sauver();
        Rafraichir();
    }

    void Rafraichir()
    {
        txtEffets.text = "Effets réduits : " + (Data.effetsReduits ? "OUI" : "NON");
        txtSon.text = "Son : " + (Data.son ? "OUI" : "NON");
        txtVibrations.text = "Vibrations : " + (Data.vibrations ? "OUI" : "NON");
        txtTaille.text = "Taille du texte : " + (Data.texteGrand ? "GRANDE" : "NORMALE");
    }
}
