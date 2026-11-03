using UnityEngine;
using TMPro;

// Carte affichée quand l'énigme est résolue : indice + XP + bouton "Mission suivante".
public class CarteIndice : MonoBehaviour
{
    [Header("Références (déjà réglées par l'outil)")]
    public PuzzleData enigme;            // indice et XP viennent d'ici
    public GameObject canvasCarte;       // Canvas de la carte (désactivé au départ)
    public TextMeshProUGUI texteIndice;
    public TextMeshProUGUI texteXP;

    public bool EstOuverte { get { return canvasCarte.activeSelf; } }

    // Appelée par OnPuzzleSolved (et quand on retouche le panneau résolu)
    public void Afficher()
    {
        texteIndice.text = TextesEnigme.CarteIndice;
        // Les XP ne sont donnés qu'une seule fois (même après un redémarrage)
        texteXP.text = ProgressManager.ResolueAuLancement ? TextesEnigme.XPDejaObtenus : "+" + enigme.recompenseXP + " XP";

        // Titre de la carte et texte du bouton (retrouvés par leur nom)
        foreach (TextMeshProUGUI t in canvasCarte.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (t.name == "Titre") t.text = TextesEnigme.CarteTitre;
            else if (t.transform.parent.name == "BoutonMission") t.text = TextesEnigme.CarteBouton;
        }
        canvasCarte.SetActive(true);
    }

    // Bouton "Mission suivante"
    public void Fermer()
    {
        canvasCarte.SetActive(false);
    }
}
