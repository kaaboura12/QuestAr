using System;
using System.Collections.Generic;

// Résultat d'une tentative de code
public enum ResultatCode { Correct, Echec, Verrouillage, Bloque }

// Données minimales d'une relique pour la logique (pas de Unity ici)
public struct InfoRelique
{
    public int id, chiffre, rang;
    public bool authentique;
    public int[] rangsContredits;
}

// Logique pure de l'énigme (C# sans UnityEngine, donc testable) :
// code, essais, blocage, reliques lues, contradictions, récompense unique.
public class EnigmeLogic
{
    readonly string code;
    readonly int essaisMax;
    readonly float dureeBlocage;
    readonly Func<float> horloge;                 // injectable (Time.time dans le jeu)
    readonly Dictionary<int, InfoRelique> reliques = new Dictionary<int, InfoRelique>();
    readonly HashSet<int> lues = new HashSet<int>();
    int echecs;
    float finBlocage = -1f;
    bool recompenseDonnee;

    public EnigmeLogic(string codeSecret, int essaisMax, float dureeBlocage, Func<float> horloge)
    {
        code = codeSecret; this.essaisMax = essaisMax; this.dureeBlocage = dureeBlocage; this.horloge = horloge;
    }

    // --- Code, essais, blocage ---
    public bool EstBloque { get { Actualiser(); return finBlocage >= 0f; } }
    public int Echecs { get { Actualiser(); return echecs; } }
    public int EssaisRestants { get { Actualiser(); return essaisMax - echecs; } }
    public float SecondesRestantes { get { Actualiser(); return finBlocage < 0f ? 0f : finBlocage - horloge(); } }

    // Fin du blocage : on repart avec de nouveaux essais
    void Actualiser()
    {
        if (finBlocage >= 0f && horloge() >= finBlocage) { finBlocage = -1f; echecs = 0; }
    }

    public ResultatCode Valider(string saisie)
    {
        if (EstBloque) return ResultatCode.Bloque;
        if (saisie == code) return ResultatCode.Correct;
        echecs++;
        if (echecs >= essaisMax) { finBlocage = horloge() + dureeBlocage; return ResultatCode.Verrouillage; }
        return ResultatCode.Echec;
    }

    // --- Récompense : une seule fois ---
    public bool AttribuerRecompense()
    {
        if (recompenseDonnee) return false;
        recompenseDonnee = true;
        return true;
    }

    // --- Reliques ---
    public void AjouterRelique(InfoRelique r) { reliques[r.id] = r; }
    public int NbLues { get { return lues.Count; } }
    public bool EstLue(int id) { return lues.Contains(id); }

    // Lire une relique (vraie ou fausse) ne coûte JAMAIS d'essai. Renvoie false si déjà lue.
    public bool MarquerLue(int id) { return lues.Add(id); }

    // Deux chroniques se contredisent-elles ?
    // authentique + authentique = cohérentes ; leurre + authentique = contradiction
    // si le leurre contredit le rang de l'authentique ; leurre + leurre = contradiction.
    public bool Contradit(int idA, int idB)
    {
        InfoRelique a, b;
        if (idA == idB || !reliques.TryGetValue(idA, out a) || !reliques.TryGetValue(idB, out b)) return false;
        if (a.authentique && b.authentique) return false;
        if (!a.authentique && !b.authentique) return true;
        InfoRelique faux = a.authentique ? b : a;
        InfoRelique vrai = a.authentique ? a : b;
        return faux.rangsContredits != null && Array.IndexOf(faux.rangsContredits, vrai.rang) >= 0;
    }

    // Code déduit des textes : chiffres des authentiques dans l'ordre chronologique
    public string DeduireCode()
    {
        List<InfoRelique> vraies = new List<InfoRelique>();
        foreach (InfoRelique r in reliques.Values) if (r.authentique) vraies.Add(r);
        vraies.Sort((x, y) => x.rang.CompareTo(y.rang));
        string s = "";
        foreach (InfoRelique r in vraies) s += r.chiffre;
        return s;
    }
}
