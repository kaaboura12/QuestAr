using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// Carte affichée quand l'énigme est résolue : récompense débloquée (Arme 2 / Flash / Micro) + bouton "Passer au Niveau suivant".
public class CarteIndice : MonoBehaviour
{
    [Header("Références (déjà réglées par l'outil)")]
    public PuzzleData enigme;            // indice et XP viennent d'ici
    public GameObject canvasCarte;       // Canvas de la carte (désactivé au départ)
    public TextMeshProUGUI texteIndice;
    public TextMeshProUGUI texteXP;

    RectTransform boutonMission;         // bouton "PASSER AU NIVEAU SUIVANT"
    int frameOuverture = -1;
    int frameFermeture = -1;
    bool enTransition = false;

    public bool EstOuverte { get { return canvasCarte != null && canvasCarte.activeSelf; } }
    public bool VientDeFermer { get { return Time.frameCount <= frameFermeture + 1; } }

    void Awake()
    {
        Configurer();
    }

    // Canvas adapté au mode Paysage (tout visible) + bouton fiable au toucher
    void Configurer()
    {
        if (canvasCarte == null) return;
        UIOutils.AssurerEventSystemValide();
        UIOutils.ConfigurerCanvasOverlay(canvasCarte, 130);

        if (boutonMission != null) return;
        foreach (RectTransform rt in canvasCarte.GetComponentsInChildren<RectTransform>(true))
        {
            if (rt.name != "BoutonMission") continue;
            boutonMission = rt;
            Image img = rt.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
            Button b = rt.GetComponent<Button>();
            if (b == null) b = rt.gameObject.AddComponent<Button>();
            if (img != null) b.targetGraphic = img;
            // Le toucher est géré une seule fois dans Update() (pas de double chargement de niveau)
            b.onClick = new Button.ButtonClickedEvent();
            break;
        }
    }

    // Appelée par OnPuzzleSolved (et quand on retouche le panneau résolu)
    public void Afficher()
    {
        Configurer();
        texteIndice.text = TextesEnigme.CarteIndice;
        texteXP.text = ProgressManager.ResolueAuLancement ? TextesEnigme.XPDejaObtenus : "+" + enigme.recompenseXP + " XP";

        // Titre de la carte et texte du bouton (retrouvés par leur nom)
        foreach (TextMeshProUGUI t in canvasCarte.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (t.name == "Titre") t.text = TextesEnigme.CarteTitre;
            else if (t.transform.parent != null && t.transform.parent.name == "BoutonMission") t.text = TextesEnigme.CarteBouton;
            t.raycastTarget = false;
        }
        frameOuverture = Time.frameCount;
        canvasCarte.SetActive(true);

        // Lecture vocale de la récompense débloquée
        EnigmeVoiceNarrator.LireTexte(TextesEnigme.CarteTitre + ". " + TextesEnigme.CarteIndice, true);
    }

    void Update()
    {
        if (!EstOuverte || boutonMission == null || enTransition) return;
        if (Time.frameCount <= frameOuverture) return;   // le toucher qui a ouvert la carte ne compte pas

        Vector2 pos;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            pos = Touchscreen.current.primaryTouch.position.ReadValue();
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            pos = Mouse.current.position.ReadValue();
        else return;

        if (RectTransformUtility.RectangleContainsScreenPoint(boutonMission, pos, null))
            Fermer();
    }

    // Bouton "PASSER AU NIVEAU SUIVANT" (anciennement "Mission suivante")
    public void Fermer()
    {
        if (enTransition) return;
        enTransition = true;
        frameFermeture = Time.frameCount;
        canvasCarte.SetActive(false);
        QuestARLevelManager.CompleteEnigmeAndLoadNextLevel();
    }
}
