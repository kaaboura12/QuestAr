using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using UnityEngine.UI;

// Gère le clavier à code : saisie, vérification, réussite / échec.
public class CodePuzzle : MonoBehaviour
{
    [Header("Références (déjà réglées par l'outil)")]
    public PuzzleData enigme;          // contient le bon code (Enigme_Musee)
    public GameObject canvasClavier;   // le Canvas du clavier (désactivé au départ)
    public Image[] fondsCases;         // les 4 cases (fond)
    public TextMeshProUGUI[] textesCases; // les 4 chiffres affichés

    [Header("Couleurs")]
    public Color couleurNormale = new Color(0.690f, 0f, 0.125f, 0.35f); // rouge sang
    public Color couleurErreur = new Color(0.3f, 0f, 0.05f, 0.9f);   // rouge sombre

    [Header("Événement (pour la récompense plus tard)")]
    public UnityEvent OnPuzzleSolved;
    public event System.Action<int> OnErreur;   // numéro de l'erreur (1 à 3), pour le sceau

    public TextMeshProUGUI texteEssais;   // "Essais : X/3" (en bas du clavier)

    const int EssaisMax = 3;
    const float DureeBlocage = 10f;    // secondes de blocage après 3 échecs
    string saisie = "";
    bool verrouille = false;           // true pendant l'affichage rouge
    EnigmeLogic logic;                 // essais, blocage, reliques (C# pur)
    [System.NonSerialized] public PuzzleStateMachine Etat = new PuzzleStateMachine();

    // Logique partagée (créée au premier usage, horloge = Time.time)
    public EnigmeLogic Logic
    {
        get
        {
            if (logic == null) logic = new EnigmeLogic(enigme.codeSecret, EssaisMax, DureeBlocage, () => Time.time);
            return logic;
        }
    }

    public bool EstOuvert { get { return canvasClavier.activeSelf; } }

    public void Ouvrir()
    {
        Reinitialiser();
        canvasClavier.SetActive(true);
        Etat.Aller(EtatEnigme.SaisieCode);
        // Si on referme puis rouvre le clavier pendant le blocage, il reste bloqué
        if (Logic.EstBloque) StartCoroutine(Blocage());
    }

    public void Fermer()
    {
        StopAllCoroutines();
        canvasClavier.SetActive(false);
        Etat.Aller(EtatEnigme.Exploration);
    }

    // Appelé par les boutons 0-9
    public void AjouterChiffre(int chiffre)
    {
        if (verrouille || saisie.Length >= 4) return;
        saisie += chiffre.ToString();
        MettreAJourCases();
    }

    // Bouton Effacer : retire le dernier chiffre
    public void Effacer()
    {
        if (verrouille || saisie.Length == 0) return;
        saisie = saisie.Substring(0, saisie.Length - 1);
        MettreAJourCases();
    }

    // Bouton Valider
    public void Valider()
    {
        if (verrouille || saisie.Length < 4) return;

        switch (Logic.Valider(saisie))
        {
            case ResultatCode.Correct:
                Fermer();
                Etat.Aller(EtatEnigme.Resolue);
                if (Logic.AttribuerRecompense()) OnPuzzleSolved.Invoke();   // une seule fois
                break;
            case ResultatCode.Verrouillage:
                if (OnErreur != null) OnErreur(Logic.Echecs);
                BandeauHistoire.Afficher(TextesEnigme.Echec3);
                StartCoroutine(Blocage());
                break;
            case ResultatCode.Echec:
                if (OnErreur != null) OnErreur(Logic.Echecs);
                if (Logic.Echecs == 1) BandeauHistoire.Afficher(TextesEnigme.Echec1);
                else BandeauHistoire.Afficher(TextesEnigme.Echec2);
                StartCoroutine(MauvaisCode());
                break;
        }
    }

    // 3 échecs : cases rouges, compte à rebours de 10 s, puis 3 nouveaux essais
    IEnumerator Blocage()
    {
        verrouille = true;
        Etat.Aller(EtatEnigme.Verrouille);
        foreach (Image f in fondsCases) f.color = couleurErreur;
        while (Logic.EstBloque)
        {
            if (texteEssais != null)
                texteEssais.text = TextesEnigme.Blocage(Mathf.CeilToInt(Logic.SecondesRestantes));
            yield return null;
        }
        Etat.Aller(EtatEnigme.SaisieCode);
        Reinitialiser();
    }

    // Cases rouges 1 seconde, puis on efface
    IEnumerator MauvaisCode()
    {
        verrouille = true;
        foreach (Image f in fondsCases) f.color = couleurErreur;
        yield return new WaitForSeconds(1f);
        Reinitialiser();
    }

    void Reinitialiser()
    {
        saisie = "";
        verrouille = false;
        MettreAJourCases();
        if (texteEssais != null) texteEssais.text = TextesEnigme.Essais(Logic.EssaisRestants);   // essais restants
    }

    void MettreAJourCases()
    {
        for (int i = 0; i < fondsCases.Length; i++)
        {
            fondsCases[i].color = couleurNormale;
            textesCases[i].text = i < saisie.Length ? saisie[i].ToString() : "";
        }
    }
}
