using UnityEngine;
using TMPro;

// Le sceau holographique réagit à tout (table de la section 5) :
// posé, relique lue, leurre lu, Vision, erreur de code (secousse + fissure), 3e erreur (chaînes + compte à rebours), victoire.
// Ajouté par HoloPuzzlePanel. Aucun son ici (phase 4). Toutes les animations durent <= 1,5 s (victoire <= 3 s).
public class SceauVisuel : MonoBehaviour
{
    static readonly Color Sang = new Color(0.690f, 0f, 0.125f);
    static readonly Color Lueur = new Color(1f, 0.063f, 0.188f);
    static readonly Color Os = new Color(0.929f, 0.902f, 0.855f);
    static readonly Color RougeSombre = new Color(0.35f, 0f, 0.06f);

    // Géométrie (repère local du panneau "Visuel" ; le cadenas est en (0, -0.2))
    float centreX, centreY = -0.2f;   // centre du cadenas (recalculé dans Init)
    const float RayonAnneau = 0.08f, RayonSegments = 0.10f, RayonRunes = 0.116f;   // 80 % de la taille d'origine
    const int NbSegments = 4, NbRunes = 8, NbFissures = 6, LiensParRangee = 7;
    const float DureeVictoire = 3f;

    Transform visuel;
    IndicesFlottants reliques;
    CodePuzzle puzzle;
    Material matLigne, matPlein;
    Transform racine, anneauPivot;
    LineRenderer anneau;
    LineRenderer[] segments = new LineRenderer[NbSegments];
    float[] segFlash = new float[NbSegments];   // 1 -> 0 quand un segment vient de s'allumer
    Transform[] runes = new Transform[NbRunes];
    LineRenderer[] fissures = new LineRenderer[NbFissures];   // 0-2 : erreurs de code, 3-5 : leurres lus
    float[] fissureVis = new float[NbFissures];
    bool[] fissureOn = new bool[NbFissures];
    Transform[] liens = new Transform[2 * LiensParRangee];
    TextMeshPro compteARebours;
    int dernierCompte = -1;

    int allumes;              // segments allumés (0 à 4)
    int nbLeurresLus;
    float intensiteVision;    // 0 à 1
    float scintille;          // 1 -> 0 (leurre lu)
    float secousse;           // 1 -> 0 (erreur)
    float baseX;
    float fermeture;          // 0 = chaînes ouvertes, 1 = fermées
    bool verrouille;
    bool termine;
    float tVictoire = -1f;
    float elargir;            // 0 -> 1 : fissures qui s'élargissent à la victoire
    Material matFlash;
    Transform flash;
    ParticleSystem eclat;

    // Courbes d'animation (sans plugin)
    readonly AnimationCurve douce = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public void Init(Transform panneauVisuel, IndicesFlottants source, CodePuzzle clavier)
    {
        visuel = panneauVisuel; reliques = source; puzzle = clavier;
        baseX = visuel.localPosition.x;
        matLigne = new Material(Shader.Find("Sprites/Default"));
        matPlein = new Material(Shader.Find("Sprites/Default"));

        racine = new GameObject("SceauVisuel").transform;
        racine.SetParent(visuel, false);
        CentrerSurCadenas();
        racine.localPosition = new Vector3(centreX, centreY, -0.03f);   // devant le panneau (côté joueur)
        anneauPivot = new GameObject("AnneauPivot").transform;
        anneauPivot.SetParent(racine, false);

        // Anneau central + 4 segments éteints
        anneau = NouvelleLigne("Anneau", anneauPivot, 0.012f, true);
        TracerArc(anneau, RayonAnneau, 0f, 360f, 48);
        for (int s = 0; s < NbSegments; s++)
        {
            segments[s] = NouvelleLigne("Segment" + (s + 1), anneauPivot, 0.02f, false);
            TracerArc(segments[s], RayonSegments, s * 90f + 10f, s * 90f + 80f, 14);
        }
        CreerRunes();
        CreerFissures();
        CreerChaines();
        CreerCompteARebours();
        CreerEffetsVictoire();

        AudioManager.DemarrerScellage();   // bourdonnement grave du sceau posé
        if (reliques != null) reliques.ReliqueLue += OnReliqueLue;
        if (puzzle != null)
        {
            puzzle.OnErreur += OnErreur;
            puzzle.OnPuzzleSolved.AddListener(Victoire);
        }
    }

    void OnDestroy()
    {
        if (reliques != null) reliques.ReliqueLue -= OnReliqueLue;
        if (puzzle != null)
        {
            puzzle.OnErreur -= OnErreur;
            puzzle.OnPuzzleSolved.RemoveListener(Victoire);
        }
    }

    // Centre = milieu réel des pièces du cadenas (corps + anse), dans le repère du panneau
    void CentrerSurCadenas()
    {
        Transform cadenas = visuel.Find("Cadenas");
        if (cadenas == null) return;
        Renderer[] rends = cadenas.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;
        Bounds b = rends[0].bounds;
        foreach (Renderer rd in rends) b.Encapsulate(rd.bounds);
        Vector3 c = visuel.InverseTransformPoint(b.center);
        centreX = c.x; centreY = c.y;
    }

    // ---------- Création des pièces ----------

    LineRenderer NouvelleLigne(string nom, Transform parent, float largeur, bool boucle)
    {
        GameObject go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = boucle;
        lr.widthMultiplier = largeur;
        lr.sharedMaterial = matLigne;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    static void TracerArc(LineRenderer lr, float rayon, float debutDeg, float finDeg, int points)
    {
        lr.positionCount = points;
        bool cercle = Mathf.Approximately(finDeg - debutDeg, 360f);
        for (int i = 0; i < points; i++)
        {
            float f = cercle ? (float)i / points : (float)i / (points - 1);
            float a = Mathf.Deg2Rad * Mathf.Lerp(debutDeg, finDeg, f);
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * rayon, Mathf.Sin(a) * rayon, 0f));
        }
    }

    // 8 runes gravées autour de l'anneau (visibles avec la Vision)
    void CreerRunes()
    {
        for (int i = 0; i < NbRunes; i++)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "RuneSceau" + i;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(anneauPivot, false);
            float a = i * Mathf.PI * 2f / NbRunes;
            go.transform.localPosition = new Vector3(Mathf.Cos(a) * RayonRunes, Mathf.Sin(a) * RayonRunes, 0f);
            go.transform.localScale = Vector3.one * 0.04f;
            Material m = new Material(Shader.Find("Sprites/Default"));
            m.mainTexture = VisionVampirique.RuneTexture(101 + i);
            go.GetComponent<Renderer>().material = m;
            go.SetActive(false);
            runes[i] = go.transform;
        }
    }

    // Fissures en zigzag qui partent de l'anneau (graine fixe : toujours le même dessin)
    void CreerFissures()
    {
        System.Random h = new System.Random(4729);
        float[] angles = { 30f, 150f, 270f, 90f, 210f, 330f };
        for (int f = 0; f < NbFissures; f++)
        {
            bool fine = f >= 3;   // les fissures des leurres sont plus fines
            LineRenderer lr = NouvelleLigne("Fissure" + f, racine, fine ? 0.005f : 0.01f, false);
            lr.positionCount = 6;
            float a = angles[f] * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 perp = new Vector2(-dir.y, dir.x);
            for (int p = 0; p < 6; p++)
            {
                float d = RayonAnneau + 0.02f + p * 0.035f;
                float zig = p == 0 ? 0f : ((float)h.NextDouble() - 0.5f) * 0.05f;
                Vector2 pos = dir * d + perp * zig;
                lr.SetPosition(p, new Vector3(pos.x, pos.y, 0f));
            }
            lr.enabled = false;
            fissures[f] = lr;
        }
    }

    // Deux rangées de maillons de chaîne (quads) qui se croisent sur le panneau
    void CreerChaines()
    {
        for (int i = 0; i < liens.Length; i++)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Maillon" + i;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(racine, false);
            bool large = i % 2 == 0;
            go.transform.localScale = large ? new Vector3(0.075f, 0.032f, 1f) : new Vector3(0.04f, 0.032f, 1f);
            go.GetComponent<Renderer>().sharedMaterial = matPlein;
            go.SetActive(false);
            liens[i] = go.transform;
        }
        matPlein.color = new Color(0.75f, 0.72f, 0.68f, 1f);
    }

    void CreerCompteARebours()
    {
        GameObject go = new GameObject("CompteARebours");
        go.transform.SetParent(racine, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        compteARebours = go.AddComponent<TextMeshPro>();
        compteARebours.fontSize = 3f;
        compteARebours.fontStyle = FontStyles.Bold;
        compteARebours.alignment = TextAlignmentOptions.Center;
        compteARebours.color = Os;
        compteARebours.rectTransform.sizeDelta = new Vector2(0.3f, 0.15f);
        go.SetActive(false);
    }

    // Éclatement (<= 100 particules) et flash de lumière pour la victoire
    void CreerEffetsVictoire()
    {
        GameObject pgo = new GameObject("EclatSceau");
        pgo.transform.SetParent(racine, false);
        eclat = pgo.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = eclat.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startColor = Lueur;
        main.startLifetime = 1.2f;
        main.startSpeed = 0.5f;
        main.startSize = 0.02f;
        main.maxParticles = 100;
        ParticleSystem.EmissionModule em = eclat.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 100) });
        ParticleSystem.ShapeModule sh = eclat.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = RayonAnneau;
        pgo.GetComponent<ParticleSystemRenderer>().sharedMaterial = matPlein;
        eclat.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // ne part qu'à la victoire

        GameObject f = GameObject.CreatePrimitive(PrimitiveType.Quad);
        f.name = "FlashSceau";
        Destroy(f.GetComponent<Collider>());
        f.transform.SetParent(racine, false);
        f.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        f.transform.localScale = Vector3.one * 0.9f;
        matFlash = new Material(Shader.Find("Sprites/Default"));
        matFlash.mainTexture = VisionVampirique.CercleSprite(64, 0f, 1f).texture;
        matFlash.color = new Color(Lueur.r, Lueur.g, Lueur.b, 0f);
        f.GetComponent<Renderer>().sharedMaterial = matFlash;
        f.SetActive(false);
        flash = f.transform;
    }

    // ---------- Événements ----------

    // Relique lue : un segment s'allume (progression). Leurre : scintillement bref + fissure fine.
    void OnReliqueLue(int i)
    {
        if (termine) return;
        int lues = puzzle != null ? puzzle.Logic.NbLues : 0;
        int cible = Mathf.Min(NbSegments, (lues * NbSegments + 6) / 7);
        for (int s = allumes; s < cible; s++) segFlash[s] = 1f;
        allumes = cible;

        if (!reliques.Data(i).authentique)
        {
            if (!ProgressManager.Data.effetsReduits) scintille = 1f;
            if (nbLeurresLus < 3) fissureOn[3 + nbLeurresLus] = true;
            nbLeurresLus++;
        }
    }

    // Erreur de code : secousse 0,3 s + une fissure de plus ; à la 3e, les chaînes se ferment (voir Update)
    void OnErreur(int numero)
    {
        if (termine) return;
        AudioManager.Jouer("fissure");
        AudioManager.Vibrer(60);
        if (!ProgressManager.Data.effetsReduits) secousse = 1f;
        if (numero >= 1 && numero <= 3) fissureOn[numero - 1] = true;
    }

    void Victoire()
    {
        if (termine) return;
        termine = true;
        tVictoire = 0f;
        AudioManager.ArreterScellage();
        AudioManager.Jouer("eclat");
        AudioManager.Vibrer(600);
        verrouille = false;
        compteARebours.gameObject.SetActive(false);
        for (int f = 0; f < 3; f++) fissureOn[f] = true;   // les 3 grandes fissures s'ouvrent
        flash.gameObject.SetActive(true);
    }

    // ---------- Animation ----------

    void Update()
    {
        float dt = Time.deltaTime;

        // Verrouillage (3 erreurs) : suit la logique, donc robuste si le clavier est fermé pendant le blocage
        if (!termine && puzzle != null)
        {
            bool bloque = puzzle.Logic.EstBloque;
            if (bloque != verrouille)
            {
                verrouille = bloque;
                if (bloque) AudioManager.Jouer("chaines");
                compteARebours.gameObject.SetActive(bloque);
                dernierCompte = -1;
                if (!bloque) { for (int f = 0; f < 3; f++) fissureOn[f] = false; }   // nouvelle série d'essais : fissures d'erreur effacées
            }
            if (bloque)
            {
                int sec = Mathf.CeilToInt(puzzle.Logic.SecondesRestantes);
                if (sec != dernierCompte) { dernierCompte = sec; compteARebours.text = sec.ToString(); }
            }
        }
        fermeture = Mathf.MoveTowards(fermeture, verrouille ? 1f : 0f, dt / (verrouille ? 1.2f : 0.6f));

        // Vision active : runes du sceau visibles + rotation lente de l'anneau
        bool vision = puzzle != null && puzzle.Etat.VisionActive && !termine;
        intensiteVision = Mathf.MoveTowards(intensiteVision, vision ? 1f : 0f, dt * 4f);
        if (intensiteVision > 0.01f) anneauPivot.Rotate(0f, 0f, 20f * intensiteVision * dt, Space.Self);
        MettreAJourRunes();

        MettreAJourAnneau(dt);
        MettreAJourFissures(dt);
        MettreAJourChaines();
        if (tVictoire >= 0f) AnimerVictoire(dt);
    }

    void MettreAJourAnneau(float dt)
    {
        scintille = Mathf.MoveTowards(scintille, 0f, dt / 0.5f);
        float vitesse = 1.5f + allumes * 0.9f;   // pulsation plus rapide avec la progression
        bool reduit = ProgressManager.Data.effetsReduits;
        float pulse = verrouille ? 0f : (reduit ? 0.5f : 0.5f + 0.5f * Mathf.Sin(Time.time * vitesse));

        Color c;
        if (verrouille) c = RougeSombre;                                   // anneau verrouillé : sombre, immobile
        else c = Color.Lerp(new Color(Sang.r, Sang.g, Sang.b, 0.55f), new Color(Lueur.r, Lueur.g, Lueur.b, 0.95f), 0.25f + 0.5f * pulse);
        c = Color.Lerp(c, Lueur, scintille);
        anneau.startColor = anneau.endColor = c;

        for (int s = 0; s < NbSegments; s++)
        {
            segFlash[s] = Mathf.MoveTowards(segFlash[s], 0f, dt / 0.6f);
            bool allume = s < allumes;
            Color sc = allume ? Color.Lerp(Lueur, Os, segFlash[s]) : new Color(Os.r, Os.g, Os.b, 0.15f);
            if (verrouille) sc = allume ? RougeSombre : new Color(Os.r, Os.g, Os.b, 0.08f);
            segments[s].startColor = segments[s].endColor = sc;
            segments[s].widthMultiplier = 0.02f * (1f + 0.8f * segFlash[s]);
        }
    }

    void MettreAJourRunes()
    {
        bool voir = intensiteVision > 0.01f;
        for (int i = 0; i < NbRunes; i++)
            if (runes[i].gameObject.activeSelf != voir) runes[i].gameObject.SetActive(voir);
        if (voir)
        {
            Color c = new Color(Lueur.r, Lueur.g, Lueur.b, intensiteVision);
            for (int i = 0; i < NbRunes; i++) runes[i].GetComponent<Renderer>().material.color = c;
        }
    }

    void MettreAJourFissures(float dt)
    {
        for (int f = 0; f < NbFissures; f++)
        {
            fissureVis[f] = Mathf.MoveTowards(fissureVis[f], fissureOn[f] ? 1f : 0f, dt / 0.4f);
            bool visible = fissureVis[f] > 0.01f;
            if (fissures[f].enabled != visible) fissures[f].enabled = visible;
            if (!visible) continue;
            Color c = new Color(Lueur.r, Lueur.g, Lueur.b, fissureVis[f]);
            fissures[f].startColor = c;
            fissures[f].endColor = new Color(c.r, c.g, c.b, c.a * 0.3f);
            float base_ = f < 3 ? 0.01f : 0.005f;
            fissures[f].widthMultiplier = base_ * (1f + 2.5f * douce.Evaluate(elargir));
        }
    }

    // Les chaînes glissent depuis les côtés et se croisent sur le panneau
    void MettreAJourChaines()
    {
        bool voir = fermeture > 0.001f && !termine;
        float k = douce.Evaluate(fermeture);
        for (int r = 0; r < 2; r++)
        {
            Vector2 debut = r == 0 ? new Vector2(-0.25f, 0.35f) - new Vector2(0f, centreY) : new Vector2(0.25f, 0.35f) - new Vector2(0f, centreY);
            Vector2 fin = r == 0 ? new Vector2(0.25f, -0.35f) - new Vector2(0f, centreY) : new Vector2(-0.25f, -0.35f) - new Vector2(0f, centreY);
            float angle = Mathf.Atan2(fin.y - debut.y, fin.x - debut.x) * Mathf.Rad2Deg;
            float decalage = (r == 0 ? -0.7f : 0.7f) * (1f - k);   // vient de l'extérieur
            for (int i = 0; i < LiensParRangee; i++)
            {
                Transform m = liens[r * LiensParRangee + i];
                if (m.gameObject.activeSelf != voir) m.gameObject.SetActive(voir);
                if (!voir) continue;
                Vector2 p = Vector2.Lerp(debut, fin, (i + 0.5f) / LiensParRangee);
                m.localPosition = new Vector3(p.x + decalage, p.y, -0.01f - 0.005f * r);
                m.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
    }

    // Victoire (<= 3 s) : fissures qui s'élargissent, flash, éclatement, anneau qui se dissout
    void AnimerVictoire(float dt)
    {
        tVictoire += dt;
        elargir = Mathf.Clamp01(tVictoire / 1.5f);

        if (tVictoire > 1.2f && !eclat.isPlaying && eclat.particleCount == 0 && tVictoire < 1.4f) eclat.Play();

        float tf = Mathf.Clamp01((tVictoire - 1f) / 1f);            // flash entre 1 s et 2 s
        float a = ProgressManager.Data.effetsReduits ? 0f : Mathf.Sin(tf * Mathf.PI) * 0.6f;   // pas de flash en effets réduits
        matFlash.color = new Color(Lueur.r, Lueur.g, Lueur.b, a);

        racine.localScale = Vector3.one * (1f + 0.25f * douce.Evaluate(Mathf.Clamp01((tVictoire - 1.5f) / 1.5f)));
        float fondu = 1f - Mathf.Clamp01((tVictoire - 1.8f) / 1.2f);
        Color ca = new Color(Lueur.r, Lueur.g, Lueur.b, fondu);
        anneau.startColor = anneau.endColor = ca;
        for (int s = 0; s < NbSegments; s++) segments[s].startColor = segments[s].endColor = ca;
        for (int f = 0; f < NbFissures; f++)
            if (fissures[f].enabled) { fissures[f].startColor = ca; fissures[f].endColor = new Color(ca.r, ca.g, ca.b, ca.a * 0.3f); }

        if (tVictoire >= DureeVictoire)
        {
            racine.gameObject.SetActive(false);
            tVictoire = -1f;
            enabled = false;
        }
    }

    // Secousse de 0,3 s : on déplace le panneau en X (le flottement de HoloPuzzlePanel ne touche que Y)
    void LateUpdate()
    {
        Vector3 p = visuel.localPosition;
        if (secousse > 0f)
        {
            secousse = Mathf.MoveTowards(secousse, 0f, Time.deltaTime / 0.3f);
            p.x = baseX + (Mathf.PerlinNoise(Time.time * 40f, 0.5f) - 0.5f) * 0.05f * secousse;
        }
        else p.x = baseX;
        visuel.localPosition = p;
    }
}
