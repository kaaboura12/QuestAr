using System;
using System.IO;
using UnityEngine;

// Données sauvegardées (JSON dans Application.persistentDataPath)
[Serializable]
public class SauvegardeQuestAR
{
    public int xp;
    public bool enigmeResolue;
    public bool xpDonnes;            // les 50 XP ont déjà été donnés
    public bool grimoireAjoute;      // le plan a été ajouté au grimoire
    public bool planDebloque;
    public bool indice6Debloque;
    public int[] reliquesLues = new int[0];
    // Réglages
    public bool effetsReduits;
    public bool son = true;
    public bool vibrations = true;
    public bool texteGrand;
}

// Sauvegarde et chargement simples. Ne plante jamais si le fichier est absent ou abîmé.
public static class ProgressManager
{
    static SauvegardeQuestAR d;
    public static bool ResolueAuLancement;   // l'énigme était déjà résolue au démarrage de l'appli

    static string Chemin { get { return Path.Combine(Application.persistentDataPath, "questar_save.json"); } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Vider() { d = null; }

    public static SauvegardeQuestAR Data
    {
        get { if (d == null) Charger(); return d; }
    }

    static void Charger()
    {
        try
        {
            if (File.Exists(Chemin)) d = JsonUtility.FromJson<SauvegardeQuestAR>(File.ReadAllText(Chemin));
        }
        catch (Exception) { d = null; }
        if (d == null) d = new SauvegardeQuestAR();
        if (d.reliquesLues == null) d.reliquesLues = new int[0];
        ResolueAuLancement = d.enigmeResolue;
    }

    public static void Sauver()
    {
        try { File.WriteAllText(Chemin, JsonUtility.ToJson(Data)); }
        catch (Exception) { /* pas de sauvegarde possible : on continue sans */ }
    }

    // --- Reliques lues ---
    public static bool EstLue(int id) { return Array.IndexOf(Data.reliquesLues, id) >= 0; }

    public static void MarquerLue(int id)
    {
        if (EstLue(id)) return;
        int[] n = new int[Data.reliquesLues.Length + 1];
        Array.Copy(Data.reliquesLues, n, Data.reliquesLues.Length);
        n[n.Length - 1] = id;
        Data.reliquesLues = n;
        Sauver();
    }

    // --- Énigme résolue : XP donnés UNE SEULE FOIS, pages du grimoire débloquées ---
    public static bool ValiderEnigme(int xp)
    {
        Data.enigmeResolue = true;
        Data.planDebloque = true;
        Data.indice6Debloque = true;
        bool nouveau = !Data.xpDonnes;
        if (nouveau) { Data.xpDonnes = true; Data.xp += xp; }
        Sauver();
        return nouveau;
    }

    public static void Reinitialiser()
    {
        bool son = Data.son, vib = Data.vibrations, grand = Data.texteGrand, reduit = Data.effetsReduits;
        d = new SauvegardeQuestAR { son = son, vibrations = vib, texteGrand = grand, effetsReduits = reduit };   // les réglages restent
        ResolueAuLancement = false;
        Sauver();
    }

    public static float FacteurTexte { get { return Data.texteGrand ? 1.25f : 1f; } }
}
