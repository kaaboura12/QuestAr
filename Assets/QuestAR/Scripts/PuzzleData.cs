using UnityEngine;

// Données d'une énigme (un fichier "asset" que tu peux créer dans Unity)
[CreateAssetMenu(fileName = "NouvelleEnigme", menuName = "QuestAR/PuzzleData")]
public class PuzzleData : ScriptableObject
{
    public string titre;                // Titre affiché sur le panneau
    public string codeSecret;           // Code à 4 chiffres (en texte)
    [TextArea] public string indice;    // Indice donné si le code est bon
    public int recompenseXP;            // XP gagnés en réussissant
    public RelicData[] reliques;        // les 7 reliques (4 vraies + 3 leurres)
}
