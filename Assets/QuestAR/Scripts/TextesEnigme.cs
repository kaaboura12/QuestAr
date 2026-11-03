// Tous les textes de l'énigme au même endroit : modifie-les ici.
public static class TextesEnigme
{
    // --- Bandeau d'histoire ---
    public const string Lancement = "La nuit tombe sur la crypte… quelque chose t'observe.";
    public const string PanneauPose = "Un sceau ancien scelle le passage. Seules les reliques du clan peuvent l'ouvrir.";
    public const string Fin = "Le sceau se brise. Un cri retentit dans la crypte…";

    // --- Panneau ---
    public const string TitrePanneau = "Le sceau du vampire";
    public const string Aide = "Cherche le sceau dans l'obscurité";
    public const string Enigme = "Sept chroniques du clan sont cachées autour de toi. Certaines sont fausses : lis-les, compare-les, puis remets les vraies dans l'ordre du temps.";

    // --- Chroniques (les fragments sont dans les assets RelicData) ---
    public const string Leurre = "Cette chronique sonne faux…";
    public static string Compteur(int x) { return "Chroniques lues : " + x + "/7"; }

    // --- Clavier ---
    public const string Echec1 = "Le sceau reste scellé…";
    public const string Echec2 = "Le sceau te brûle les doigts. Les reliques ont un ordre dans le temps : lis leurs chroniques.";
    public const string Echec3 = "Les ombres t'encerclent. Attends l'aube…";
    public static string Essais(int x) { return "Essais : " + x + "/3"; }
    public static string Blocage(int secondes) { return "Attends l'aube… " + secondes + " s"; }

    // --- Carte finale ---
    public const string CarteTitre = "INDICE TROUVÉ";
    public const string CarteIndice = "La crypte du château renferme le plan.";
    public const string IndiceMission6 = "Indice mission 6 : la clé de la porte de fer est dans la salle du trône, derrière le portrait du seigneur Aldric."; // pour le grimoire (phase 5)
    public const string SousTitreChuchotements = "(Des chuchotements s'élèvent des murs…)";
    public const string XPDejaObtenus = "XP déjà obtenus";
    public const string CarteBouton = "POURSUIVRE LA CHASSE";
    public const string PlanHologramme = "PLAN - CRYPTE";
}
