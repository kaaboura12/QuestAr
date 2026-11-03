using UnityEngine;
using UnityEngine.SceneManagement;

// Démarre tout seul dans la scène Mission5_Enigme et ajoute les systèmes (grimoire, réglages, modes AR, audio...).
// Aucun objet à ajouter à la main dans la scène.
public class QuestARSysteme : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Demarrer()
    {
        if (SceneManager.GetActiveScene().name != "Mission5_Enigme") return;
        if (FindAnyObjectByType<QuestARSysteme>() != null) return;
        new GameObject("QuestARSysteme").AddComponent<QuestARSysteme>();
    }

    void Awake()
    {
        gameObject.AddComponent<AudioManager>();
        gameObject.AddComponent<GrimoireUI>();
        gameObject.AddComponent<ReglagesUI>();
        gameObject.AddComponent<SelecteurMode>();
    }

    void Start()
    {
        // Les textes qui ne sont pas des boutons ne bloquent pas les touchers
        UIOutils.DesactiverRaycastTextes();
    }
}
