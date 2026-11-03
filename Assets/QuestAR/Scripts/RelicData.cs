using UnityEngine;

// Une relique (chronique) : 4 authentiques + 3 leurres. Un fichier "asset" par relique.
[CreateAssetMenu(fileName = "NouvelleRelique", menuName = "QuestAR/RelicData")]
public class RelicData : ScriptableObject
{
    public int id;                      // identifiant unique (1 à 7)
    public int chiffre;                 // chiffre affiché sur la relique
    public int rang;                    // rang chronologique (1 à 4), 0 pour un leurre
    public bool authentique;            // false = leurre (chronique falsifiée)
    public string titre;
    [TextArea] public string fragment;  // texte lu par le joueur
    public int[] rangsContredits;       // leurre : rangs des reliques authentiques qu'il contredit
    public Sprite runeSprite;           // rune gravée (phase 2)
}
