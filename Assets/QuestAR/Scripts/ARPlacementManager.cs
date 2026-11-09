using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;

// Place UN panneau holographique quand on touche un plan détecté.
// Cache aussi le texte d'aide dès qu'un plan est détecté.
public class ARPlacementManager : MonoBehaviour
{
    [Header("Références (à glisser dans l'Inspector)")]
    public ARRaycastManager raycastManager;   // lance les "rayons" vers les plans
    public ARPlaneManager planeManager;       // détecte les surfaces
    public TextMeshProUGUI texteAide;         // "Pointe ton téléphone vers le sol"

    [Header("Panneau à placer")]
    public GameObject panneauPrefab;          // prefab HoloPuzzlePanel

    // Liste réutilisée pour stocker les résultats du raycast
    static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

    bool dejaPlace = false;   // pour ne placer qu'une seule fois

    void Update()
    {
        // 1) Cache le texte dès qu'au moins un plan existe
        if (texteAide != null && texteAide.gameObject.activeSelf && planeManager.trackables.count > 0)
            texteAide.gameObject.SetActive(false);

        // 2) Déjà placé ? On ne fait plus rien
        if (dejaPlace) return;

        // 3) Détecte un toucher (ou un clic souris dans l'éditeur)
        if (!ToucheOuClic(out Vector2 position)) return;
        Debug.Log("[Placement] Clic détecté en " + position);

        // Un élément UI (clavier, carte...) est sous le doigt ? On ignore le clic
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log("[Placement] Clic ignoré : un élément UI bloque l'écran");
            return;
        }

        // 4) Rayon depuis l'écran vers les plans détectés (ou placement direct devant la caméra en Éditeur / secours)
        if (raycastManager != null && raycastManager.Raycast(position, hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneEstimated))
        {
            Debug.Log("[Placement] Raycast touché : plan à " + hits[0].pose.position);
            Pose pose = hits[0].pose;
            PlacerPanneau(pose.position);
            dejaPlace = true;
        }
        else if (Application.isEditor || planeManager == null || planeManager.trackables.count == 0)
        {
            Camera c = Camera.main;
            if (c != null)
            {
                Vector3 fwd = c.transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
                fwd.Normalize();
                Vector3 pos = c.transform.position + fwd * 2.0f;
                pos.y = c.transform.position.y - 0.9f;
                if (texteAide != null) texteAide.gameObject.SetActive(false);
                PlacerPanneau(pos);
                dejaPlace = true;
            }
        }
        else
        {
            Debug.Log("[Placement] Raycast raté : aucun plan sous le clic (plans détectés : " + planeManager.trackables.count + ")");
        }
    }

    // Retourne true si l'écran vient d'être touché
    bool ToucheOuClic(out Vector2 position)
    {
        position = Vector2.zero;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            position = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            position = Mouse.current.position.ReadValue();
            return true;
        }
        return false;
    }

    void PlacerPanneau(Vector3 positionMonde)
    {
        // Le prefab a son point d'origine au sol : on le pose là où on a touché
        if (panneauPrefab == null) { Debug.LogError("[Placement] panneauPrefab est vide !"); return; }
        GameObject go = Instantiate(panneauPrefab, positionMonde, Quaternion.identity);
        Debug.Log("[Placement] Panneau créé : " + go.name);
    }
}
