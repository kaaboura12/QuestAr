using System;

public enum EtatEnigme { Intro, Exploration, SaisieCode, Verrouille, Resolue }

// États de l'énigme (C# pur). VisionActive et GrimoireOuvert sont indépendants de l'état principal.
public class PuzzleStateMachine
{
    public EtatEnigme Etat { get; private set; }
    public bool VisionActive;
    public bool GrimoireOuvert;
    public event Action<EtatEnigme> Change;

    // "Resolue" est définitif
    public void Aller(EtatEnigme nouveau)
    {
        if (Etat == EtatEnigme.Resolue || Etat == nouveau) return;
        Etat = nouveau;
        if (Change != null) Change(nouveau);
    }
}
