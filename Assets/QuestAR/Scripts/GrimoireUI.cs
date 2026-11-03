using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Grimoire : bouton en haut à droite, livre plein écran à 3 pages (Chronique, Plan du château, Indices),
// bouton "Ajouter au grimoire" sur la carte finale, XP donnés une seule fois, reset caché (triple tap en haut à gauche).
public class GrimoireUI : MonoBehaviour
{
    static readonly Color Rouge = new Color(0.690f, 0f, 0.125f);
    static readonly Color Lueur = new Color(1f, 0.063f, 0.188f);

    CarteIndice carte;
    CodePuzzle puzzle;
    PuzzleData enigme;
    Canvas canvasBouton, canvasLivre;
    RectTransform boutonGrimoire;
    GameObject livre;
    RectTransform pageRacine;       // contenu qui "tourne"
    TextMeshProUGUI titrePage, textePage, indicateur, texteXP;
    GameObject planRacine;          // dessin du plan (créé au premier affichage)
    int page;                       // 0 à 2
    bool enTransition;
    Button boutonAjout;
    TextMeshProUGUI texteAjout;
    int tapsReset;
    float debutTaps;

    void Start()
    {
        carte = FindAnyObjectByType<CarteIndice>();
        puzzle = FindAnyObjectByType<CodePuzzle>();
        enigme = carte != null ? carte.enigme : null;

        // Bouton GRIMOIRE (haut droite) + zone de reset cachée (haut gauche)
        canvasBouton = UIOutils.NouveauCanvas("CanvasGrimoireBouton", -4);
        canvasBouton.transform.SetParent(transform, false);
        RectTransform zone = UIOutils.ZoneSure(canvasBouton.transform);
        Button b = UIOutils.Bouton(zone, "GRIMOIRE", Vector2.zero, new Vector2(240f, 150f), Ouvrir, 36f);
        boutonGrimoire = (RectTransform)b.transform;
        PlacerCoin(boutonGrimoire, 1f, 1f, new Vector2(-30f - 120f, -30f - 75f));

        Image reset = UIOutils.Rect("ResetCache", zone, Vector2.zero, new Vector2(150f, 150f), new Color(0f, 0f, 0f, 0f), true);
        PlacerCoin(reset.rectTransform, 0f, 1f, new Vector2(75f, -75f));
        reset.gameObject.AddComponent<Button>().onClick.AddListener(TapReset);

        ConstruireLivre();
        livre.SetActive(false);

        if (puzzle != null) puzzle.OnPuzzleSolved.AddListener(Resolu);
        if (carte != null) PreparerBoutonAjout();
    }

    void OnDestroy()
    {
        if (puzzle != null) puzzle.OnPuzzleSolved.RemoveListener(Resolu);
    }

    static void PlacerCoin(RectTransform rt, float ax, float ay, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
    }

    // ---------- Énigme résolue : XP une seule fois ----------

    void Resolu()
    {
        int xp = enigme != null ? enigme.recompenseXP : 0;
        ProgressManager.ValiderEnigme(xp);   // renvoie false si les XP ont déjà été donnés
        MettreAJourXP();
    }

    // ---------- Reset caché ----------

    void TapReset()
    {
        if (Time.unscaledTime - debutTaps > 1.5f) { tapsReset = 0; debutTaps = Time.unscaledTime; }
        tapsReset++;
        if (tapsReset < 3) return;
        tapsReset = 0;
        ProgressManager.Reinitialiser();
        BandeauHistoire.Afficher("Progression effacée.");
        StartCoroutine(Recharger());
    }

    IEnumerator Recharger()
    {
        yield return new WaitForSecondsRealtime(1f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ---------- Livre ----------

    void ConstruireLivre()
    {
        canvasLivre = UIOutils.NouveauCanvas("CanvasGrimoireLivre", 30);
        canvasLivre.transform.SetParent(transform, false);
        livre = canvasLivre.gameObject;   // on active / désactive tout le canvas

        Image fond = UIOutils.Rect("Fond", canvasLivre.transform, Vector2.zero, new Vector2(3000f, 3000f), new Color(0.03f, 0.03f, 0.03f, 0.97f), true);
        RectTransform zone = UIOutils.ZoneSure(canvasLivre.transform);

        UIOutils.Texte(zone, "GRIMOIRE", 64f, new Vector2(0f, 800f), new Vector2(800f, 100f)).fontStyle = FontStyles.Bold;
        texteXP = UIOutils.Texte(zone, "", 36f, new Vector2(0f, 730f), new Vector2(800f, 60f));
        texteXP.color = Lueur;

        // Page qui tourne : titre + texte
        pageRacine = (RectTransform)new GameObject("Page", typeof(RectTransform)).transform;
        pageRacine.SetParent(zone, false);
        pageRacine.anchorMin = pageRacine.anchorMax = new Vector2(0.5f, 0.5f);
        pageRacine.anchoredPosition = new Vector2(0f, 40f);
        pageRacine.sizeDelta = new Vector2(920f, 1300f);
        titrePage = UIOutils.Texte(pageRacine, "", 52f, new Vector2(0f, 590f), new Vector2(900f, 90f));
        titrePage.fontStyle = FontStyles.Bold;
        titrePage.color = Lueur;
        textePage = UIOutils.Texte(pageRacine, "", 36f, new Vector2(0f, -40f), new Vector2(880f, 1150f), TextAlignmentOptions.TopLeft);
        textePage.rectTransform.anchoredPosition = new Vector2(0f, -40f);
        textePage.richText = true;

        // Navigation (cibles >= 140 px)
        UIOutils.Bouton(zone, "<", new Vector2(-330f, -780f), new Vector2(200f, 150f), () => ChangerPage(-1), 64f);
        UIOutils.Bouton(zone, ">", new Vector2(330f, -780f), new Vector2(200f, 150f), () => ChangerPage(1), 64f);
        UIOutils.Bouton(zone, "FERMER", new Vector2(0f, -780f), new Vector2(340f, 150f), Fermer, 44f);
        indicateur = UIOutils.Texte(zone, "", 36f, new Vector2(0f, -690f), new Vector2(400f, 60f));
    }

    void Ouvrir()
    {
        if (puzzle != null && puzzle.Etat != null) puzzle.Etat.GrimoireOuvert = true;
        page = 0;
        livre.SetActive(true);
        pageRacine.localScale = Vector3.one;
        MettreAJourXP();
        AfficherPage();
        AudioManager.Jouer("page");
    }

    void Fermer()
    {
        if (puzzle != null && puzzle.Etat != null) puzzle.Etat.GrimoireOuvert = false;
        livre.SetActive(false);
    }

    void MettreAJourXP()
    {
        if (texteXP != null) texteXP.text = "XP : " + ProgressManager.Data.xp;
    }

    void ChangerPage(int delta)
    {
        if (enTransition) return;
        int n = Mathf.Clamp(page + delta, 0, 2);
        if (n == page) return;
        page = n;
        AudioManager.Jouer("page");
        if (ProgressManager.Data.effetsReduits) AfficherPage();
        else StartCoroutine(Tourner());
    }

    // Page qui se retourne : 0,15 s pour se fermer, 0,15 s pour s'ouvrir (0,3 s en tout, sans plugin)
    IEnumerator Tourner()
    {
        enTransition = true;
        AnimationCurve courbe = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        for (float t = 0f; t < 0.15f; t += Time.unscaledDeltaTime)
        {
            pageRacine.localScale = new Vector3(1f - courbe.Evaluate(t / 0.15f), 1f, 1f);
            yield return null;
        }
        AfficherPage();
        for (float t = 0f; t < 0.15f; t += Time.unscaledDeltaTime)
        {
            pageRacine.localScale = new Vector3(courbe.Evaluate(t / 0.15f), 1f, 1f);
            yield return null;
        }
        pageRacine.localScale = Vector3.one;
        enTransition = false;
    }

    void AfficherPage()
    {
        indicateur.text = (page + 1) + " / 3";
        if (planRacine != null) planRacine.SetActive(false);
        SauvegardeQuestAR s = ProgressManager.Data;
        switch (page)
        {
            case 0:
                titrePage.text = "CHRONIQUE";
                textePage.text = TexteChronique(s);
                break;
            case 1:
                titrePage.text = "PLAN DU CHÂTEAU";
                if (!s.planDebloque) textePage.text = "Page verrouillée.\nBrise le sceau pour la révéler.";
                else
                {
                    textePage.text = "";
                    AfficherPlan();
                }
                break;
            default:
                titrePage.text = "INDICES";
                textePage.text = s.indice6Debloque ? TextesEnigme.IndiceMission6 : "Page verrouillée.\nBrise le sceau pour la révéler.";
                break;
        }
    }

    // Page 1 : les chroniques authentiques lues dans l'ordre du temps ; leurres révélés après la résolution
    string TexteChronique(SauvegardeQuestAR s)
    {
        StringBuilder sb = new StringBuilder();
        if (enigme == null || enigme.reliques == null) return "Aucune chronique.";
        int nb = 0;
        for (int rang = 1; rang <= 4; rang++)
            foreach (RelicData r in enigme.reliques)
                if (r.authentique && r.rang == rang && ProgressManager.EstLue(r.id))
                {
                    sb.Append(rang).Append(". ").Append(r.fragment).Append("\n\n");
                    nb++;
                }
        if (nb == 0) sb.Append("Aucune chronique lue pour l'instant.\n\n");
        if (s.enigmeResolue)
            foreach (RelicData r in enigme.reliques)
                if (!r.authentique)
                    sb.Append("<color=#FF1030>[X] chronique falsifiée</color> : ").Append(r.fragment).Append("\n\n");
        return sb.ToString();
    }

    // Page 2 : schéma du château dessiné avec des rectangles et des lignes
    void AfficherPlan()
    {
        if (planRacine == null)
        {
            planRacine = new GameObject("Plan", typeof(RectTransform));
            planRacine.transform.SetParent(pageRacine, false);
            RectTransform rt = (RectTransform)planRacine.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -40f);
            rt.sizeDelta = new Vector2(880f, 1000f);
            Transform p = planRacine.transform;

            Color os = UIOutils.Os;
            // Murs du château
            Cadre(p, Vector2.zero, new Vector2(760f, 700f), 8f, os);
            // Tour nord (carré en haut à gauche)
            Cadre(p, new Vector2(-260f, 300f), new Vector2(150f, 150f), 8f, os);
            UIOutils.Texte(p, "TOUR NORD", 32f, new Vector2(-260f, 300f), new Vector2(150f, 100f));
            // Crypte sous la tour (rouge, avec une croix)
            UIOutils.Rect("Crypte", p, new Vector2(-260f, 110f), new Vector2(190f, 110f), new Color(Rouge.r, Rouge.g, Rouge.b, 0.8f));
            UIOutils.Texte(p, "CRYPTE", 36f, new Vector2(-260f, 110f), new Vector2(190f, 60f)).fontStyle = FontStyles.Bold;
            Image b1 = UIOutils.Rect("Croix1", p, new Vector2(-260f, 20f), new Vector2(60f, 10f), Lueur);
            Image b2 = UIOutils.Rect("Croix2", p, new Vector2(-260f, 20f), new Vector2(60f, 10f), Lueur);
            b1.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            b2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            // Chemin pointillé de l'entrée à la crypte
            Vector2 a = new Vector2(0f, -330f), c = new Vector2(-260f, 55f);
            for (int i = 0; i < 14; i++)
                UIOutils.Rect("Pas" + i, p, Vector2.Lerp(a, c, (i + 0.5f) / 14f), new Vector2(16f, 16f), Lueur);
            UIOutils.Texte(p, "ENTRÉE", 32f, new Vector2(0f, -420f), new Vector2(300f, 60f));
            UIOutils.Texte(p, "Le plan est caché derrière le sceau.", 32f, new Vector2(0f, 430f), new Vector2(860f, 60f));
        }
        planRacine.SetActive(true);
    }

    // Rectangle vide = 4 barres
    static void Cadre(Transform p, Vector2 centre, Vector2 taille, float ep, Color c)
    {
        UIOutils.Rect("H1", p, centre + new Vector2(0f, taille.y * 0.5f), new Vector2(taille.x, ep), c);
        UIOutils.Rect("H2", p, centre - new Vector2(0f, taille.y * 0.5f), new Vector2(taille.x, ep), c);
        UIOutils.Rect("V1", p, centre + new Vector2(taille.x * 0.5f, 0f), new Vector2(ep, taille.y), c);
        UIOutils.Rect("V2", p, centre - new Vector2(taille.x * 0.5f, 0f), new Vector2(ep, taille.y), c);
    }

    // ---------- Carte finale : "Ajouter au grimoire" ----------

    void PreparerBoutonAjout()
    {
        Transform ca = carte.canvasCarte.transform.Find("Carte");
        if (ca == null) return;
        boutonAjout = UIOutils.Bouton(ca, "AJOUTER AU GRIMOIRE", new Vector2(0f, -440f), new Vector2(620f, 120f), Ajouter, 38f);
        texteAjout = boutonAjout.GetComponentInChildren<TextMeshProUGUI>();
        if (ProgressManager.Data.grimoireAjoute) MarquerAjoute();
    }

    void MarquerAjoute()
    {
        boutonAjout.interactable = false;
        texteAjout.text = "AJOUTÉ AU GRIMOIRE";
    }

    void Ajouter()
    {
        ProgressManager.Data.grimoireAjoute = true;
        ProgressManager.Sauver();
        MarquerAjoute();
        AudioManager.Jouer("page");
        if (!ProgressManager.Data.effetsReduits) StartCoroutine(PageVolante());
    }

    // Une petite page "vole" de la carte vers le bouton GRIMOIRE (0,6 s)
    IEnumerator PageVolante()
    {
        RectTransform canvasCarte = (RectTransform)carte.canvasCarte.transform;
        Image page = UIOutils.Rect("PageVolante", canvasCarte, Vector2.zero, new Vector2(120f, 160f), UIOutils.Os);
        Vector2 depart = LocalDans(canvasCarte, (RectTransform)boutonAjout.transform);
        Vector2 arrivee = LocalDans(canvasCarte, boutonGrimoire);
        AnimationCurve courbe = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            float k = courbe.Evaluate(t / 0.6f);
            page.rectTransform.anchoredPosition = Vector2.Lerp(depart, arrivee, k);
            page.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, k);
            page.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 25f * k);
            yield return null;
        }
        Destroy(page.gameObject);
        // petit "rebond" du bouton GRIMOIRE
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            boutonGrimoire.localScale = Vector3.one * (1f + 0.2f * Mathf.Sin(t / 0.3f * Mathf.PI));
            yield return null;
        }
        boutonGrimoire.localScale = Vector3.one;
    }

    // Position d'un élément d'un autre canvas, exprimée dans le repère d'un canvas
    static Vector2 LocalDans(RectTransform canvas, RectTransform cible)
    {
        Vector2 ecran = RectTransformUtility.WorldToScreenPoint(null, cible.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, ecran, null, out Vector2 local);
        return local;
    }
}
