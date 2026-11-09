using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
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
    bool layoutConfigure = false;
    int lastInputFrame = -1;
    int frameOuverture = -1;
    EnigmeLogic logic;                 // essais, blocage, reliques (C# pur)
    [System.NonSerialized] public PuzzleStateMachine Etat = new PuzzleStateMachine();

    struct ZoneTouche
    {
        public RectTransform rect;
        public System.Action action;
    }
    readonly List<ZoneTouche> zonesTouches = new List<ZoneTouche>();

    // Nombre de chiffres requis pour le niveau actuel : 2 (Niveau 1) -> 3 (Niveau 2) -> 4 (Niveau 3)
    public int NbChiffresRequis => Mathf.Clamp(QuestARLevelManager.GetPuzzleDigitsForCurrentLevel(), 2, 4);

    void Awake()
    {
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;

        ConfigurerLayoutEtBoutons();
    }

    void ConfigurerLayoutEtBoutons()
    {
        if (canvasClavier == null) return;
        UIOutils.AssurerEventSystemValide();
        UIOutils.ConfigurerCanvasOverlay(canvasClavier, 125);

        Transform cv = canvasClavier.transform;

        // Fond bien sombre et opaque pour que le clavier soit parfaitement lisible
        Transform fondTr = cv.Find("Fond");
        if (fondTr != null)
        {
            Image fondImg = fondTr.GetComponent<Image>();
            if (fondImg != null)
            {
                fondImg.color = new Color(0.02f, 0.02f, 0.03f, 0.96f);
                fondImg.raycastTarget = true;
            }
        }

        // Titre en haut
        Transform titreTr = cv.Find("Titre");
        if (titreTr is RectTransform titreRt)
        {
            titreRt.anchoredPosition = new Vector2(0f, 760f);
            titreRt.sizeDelta = new Vector2(960f, 95f);
            TextMeshProUGUI tt = titreRt.GetComponent<TextMeshProUGUI>();
            if (tt != null)
            {
                tt.fontSize = 54f;
                tt.text = $"ENTRER LE CODE ({NbChiffresRequis} CHIFFRES)";
            }
        }

        // Bouton Fermer (X) en haut à droite, bien visible dans l'écran Paysage
        Transform fermerTr = cv.Find("Fermer");
        if (fermerTr is RectTransform fermerRt)
        {
            bool paysage = Screen.width >= Screen.height;
            fermerRt.anchoredPosition = paysage ? new Vector2(560f, 760f) : new Vector2(450f, 780f);
            fermerRt.sizeDelta = new Vector2(140f, 140f);
        }

        // Cases des chiffres centrées horizontalement selon NbChiffresRequis (2, 3 ou 4)
        int req = NbChiffresRequis;
        float espacementCase = 225f;
        float startX = -(req - 1) * espacementCase * 0.5f;
        if (fondsCases != null)
        {
            for (int i = 0; i < fondsCases.Length; i++)
            {
                if (fondsCases[i] == null) continue;
                RectTransform rtCase = fondsCases[i].rectTransform;
                rtCase.anchoredPosition = new Vector2(startX + i * espacementCase, 540f);
                rtCase.sizeDelta = new Vector2(195f, 210f);
                if (i < textesCases.Length && textesCases[i] != null)
                {
                    textesCases[i].fontSize = 130f;
                    textesCases[i].raycastTarget = false;
                }
            }
        }

        // Grille 1..9 bien centrée et espacée pour rentrer à 100% avec les boutons du bas
        Vector2 tailleBouton = new Vector2(275f, 185f);
        for (int n = 1; n <= 9; n++)
        {
            Transform tTr = cv.Find("Touche" + n);
            if (tTr is RectTransform tRt)
            {
                int col = (n - 1) % 3;
                int ligne = (n - 1) / 3;
                tRt.anchoredPosition = new Vector2((col - 1) * 305f, 220f - ligne * 210f);
                tRt.sizeDelta = tailleBouton;
            }
        }

        // Ligne du bas : EFFACER, 0, VALIDER
        Transform effTr = cv.Find("Effacer");
        if (effTr is RectTransform effRt)
        {
            effRt.anchoredPosition = new Vector2(-305f, -415f);
            effRt.sizeDelta = tailleBouton;
        }

        Transform t0Tr = cv.Find("Touche0");
        if (t0Tr is RectTransform t0Rt)
        {
            t0Rt.anchoredPosition = new Vector2(0f, -415f);
            t0Rt.sizeDelta = tailleBouton;
        }

        Transform valTr = cv.Find("Valider");
        if (valTr is RectTransform valRt)
        {
            valRt.anchoredPosition = new Vector2(305f, -415f);
            valRt.sizeDelta = tailleBouton;
        }

        // Texte Essais en bas
        if (texteEssais != null)
        {
            RectTransform essRt = texteEssais.rectTransform;
            essRt.anchoredPosition = new Vector2(0f, -610f);
            essRt.sizeDelta = new Vector2(850f, 85f);
            texteEssais.fontSize = 46f;
            texteEssais.raycastTarget = false;
        }

        if (layoutConfigure) return;
        layoutConfigure = true;

        // Rebranchement propre des boutons par code + secours tactile direct
        zonesTouches.Clear();
        for (int n = 0; n <= 9; n++)
        {
            int chiffre = n;
            BrancherBouton(cv.Find("Touche" + n), () => AjouterChiffre(chiffre));
        }
        BrancherBouton(effTr, Effacer);
        BrancherBouton(valTr, Valider);
        BrancherBouton(fermerTr, Fermer);
    }

    void BrancherBouton(Transform tr, System.Action action)
    {
        if (tr == null) return;
        Image img = tr.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;

        Button btn = tr.GetComponent<Button>();
        if (btn == null) btn = tr.gameObject.AddComponent<Button>();
        if (img != null) btn.targetGraphic = img;

        // On vide les listeners (persistants cassés) : le toucher est géré UNE seule fois dans Update()
        // (le Button reste pour l'effet visuel). Ainsi aucun double chiffre (appui + relâchement).
        btn.onClick = new Button.ButtonClickedEvent();

        foreach (TMP_Text tmp in tr.GetComponentsInChildren<TMP_Text>(true))
            tmp.raycastTarget = false;

        if (tr is RectTransform rt)
            zonesTouches.Add(new ZoneTouche { rect = rt, action = action });
    }

    void ExecuterActionTouche(System.Action action)
    {
        if (lastInputFrame == Time.frameCount) return;
        lastInputFrame = Time.frameCount;
        action?.Invoke();
    }

    void Update()
    {
        if (!EstOuvert) return;

        // Secours tactile / souris direct si l'EventSystem ne déclenche pas le Button.onClick
        Vector2 pos;
        bool pressed = false;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            pos = Touchscreen.current.primaryTouch.position.ReadValue();
            pressed = true;
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            pos = Mouse.current.position.ReadValue();
            pressed = true;
        }
        else
        {
            return;
        }

        // Le toucher qui a OUVERT le clavier ne doit pas taper un chiffre
        if (!pressed || lastInputFrame == Time.frameCount || Time.frameCount <= frameOuverture) return;

        for (int i = 0; i < zonesTouches.Count; i++)
        {
            ZoneTouche z = zonesTouches[i];
            if (z.rect != null && z.rect.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(z.rect, pos, null))
            {
                ExecuterActionTouche(z.action);
                break;
            }
        }
    }

    // Logique partagée (créée au premier usage, horloge = Time.time)
    public EnigmeLogic Logic
    {
        get
        {
            if (logic == null)
            {
                string fullCode = (enigme != null && !string.IsNullOrEmpty(enigme.codeSecret)) ? enigme.codeSecret : "4729";
                int len = Mathf.Min(NbChiffresRequis, fullCode.Length);
                string levelCode = fullCode.Substring(0, len);
                logic = new EnigmeLogic(levelCode, EssaisMax, DureeBlocage, () => Time.time);
            }
            return logic;
        }
    }

    public bool EstOuvert { get { return canvasClavier.activeSelf; } }

    // Vrai pendant la frame où le clavier vient d'être fermé (évite qu'un même toucher le rouvre)
    int frameFermeture = -1;
    public bool VientDeFermer { get { return Time.frameCount <= frameFermeture + 1; } }

    public void Ouvrir()
    {
        ConfigurerLayoutEtBoutons();
        frameOuverture = Time.frameCount;
        Reinitialiser();
        canvasClavier.SetActive(true);
        Etat.Aller(EtatEnigme.SaisieCode);
        // Si on referme puis rouvre le clavier pendant le blocage, il reste bloqué
        if (Logic.EstBloque) StartCoroutine(Blocage());
    }

    public void Fermer()
    {
        StopAllCoroutines();
        frameFermeture = Time.frameCount;
        canvasClavier.SetActive(false);
        Etat.Aller(EtatEnigme.Exploration);
    }

    // Appelé par les boutons 0-9
    public void AjouterChiffre(int chiffre)
    {
        if (verrouille || saisie.Length >= NbChiffresRequis) return;
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
        if (verrouille || saisie.Length < NbChiffresRequis) return;

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
        int req = NbChiffresRequis;
        for (int i = 0; i < fondsCases.Length; i++)
        {
            bool active = i < req;
            if (fondsCases[i] != null && fondsCases[i].gameObject.activeSelf != active)
                fondsCases[i].gameObject.SetActive(active);

            if (active)
            {
                fondsCases[i].color = couleurNormale;
                textesCases[i].text = i < saisie.Length ? saisie[i].ToString() : "";
            }
        }
    }
}
