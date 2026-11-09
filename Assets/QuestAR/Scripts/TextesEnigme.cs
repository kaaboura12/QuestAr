// Tous les textes de l'énigme au même endroit, adaptés dynamiquement au Niveau actuel (2, 3 ou 4 chiffres).
public static class TextesEnigme
{
    // --- Bandeau d'histoire ---
    public const string Lancement = "La nuit tombe sur la crypte… touche le sol pour poser le sceau.";
    public const string PanneauPose = "Un sceau ancien scelle le coffre. Lis les chroniques autour de toi pour découvrir le code.";
    public const string Fin = "Le sceau se brise ! La récompense apparaît devant toi…";

    // --- Panneau ---
    public const string TitrePanneau = "Le sceau du vampire";
    public const string Aide = "Cherche le sceau dans l'obscurité";
    public const string Enigme = "Sept chroniques du clan sont cachées autour de toi. Certaines sont fausses : lis-les, compare-les, puis remets les vraies dans l'ordre du temps.";

    public static string TitrePanneauCourant()
    {
        int digits = QuestARLevelManager.GetPuzzleDigitsForCurrentLevel();
        return $"Énigme Niveau {QuestARLevelManager.CurrentLevel} (Code à {digits} chiffres)";
    }

    public static string EnigmeCourante()
    {
        int digits = QuestARLevelManager.GetPuzzleDigitsForCurrentLevel();
        int totalReliques = digits == 2 ? 3 : (digits == 3 ? 5 : 7);
        string reward = QuestARLevelManager.CurrentLevel == 1
            ? "le Fusil d'Assaut (2ème arme)"
            : (QuestARLevelManager.CurrentLevel == 2 ? "le Flash + Capteur de Lumière" : "le Capteur Microphone");
        return $"{totalReliques} chroniques sont cachées autour de toi ({digits} vraies). Lis-les et entre les {digits} chiffres dans l'ordre du temps pour débloquer {reward} !";
    }

    // --- Chroniques (les fragments sont dans les assets RelicData) ---
    public const string Leurre = "Attention, cette chronique sonne faux… c'est un piège !";
    public static string Compteur(int x)
    {
        int digits = QuestARLevelManager.GetPuzzleDigitsForCurrentLevel();
        int total = digits == 2 ? 3 : (digits == 3 ? 5 : 7);
        return $"Chroniques lues : {x}/{total} (Code : {digits} chiffres)";
    }

    // --- Clavier ---
    public const string Echec1 = "Le sceau reste scellé…";
    public const string Echec2 = "Le sceau te brûle les doigts. Remets les vraies reliques dans l'ordre chronologique.";
    public const string Echec3 = "Les ombres t'encerclent. Attends l'aube…";
    public static string Essais(int x) { return "Essais : " + x + "/3"; }
    public static string Blocage(int secondes) { return "Attends l'aube… " + secondes + " s"; }

    // --- Carte finale ---
    public static string CarteTitre => QuestARLevelManager.GetPuzzleRewardTitle();
    public static string CarteIndice => QuestARLevelManager.GetPuzzleRewardDescription();
    public const string IndiceMission6 = "Indice mission 6 : la clé de la porte de fer est dans la salle du trône, derrière le portrait du seigneur Aldric.";
    public const string SousTitreChuchotements = "(Des chuchotements s'élèvent des murs…)";
    public const string XPDejaObtenus = "XP déjà obtenus";
    public static string CarteBouton => QuestARLevelManager.GetNextMissionButtonText();
    public static string PlanHologramme => QuestARLevelManager.CurrentLevel == 1 ? "ARME 2 - FUSIL" : (QuestARLevelManager.CurrentLevel == 2 ? "TORCHE FLASH" : "MICRO SONORE");
}
