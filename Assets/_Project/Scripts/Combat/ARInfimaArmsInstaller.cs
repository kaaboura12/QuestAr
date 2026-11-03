using System.Collections;
using InfimaGames.LowPolyShooterPack;
using InfimaGames.LowPolyShooterPack.Interface;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Spawns Infima first-person arms under WeaponAnchor and hooks them to the equipped AR weapon.
/// </summary>
[DefaultExecutionOrder(50)]
public class ARInfimaArmsInstaller : MonoBehaviour
{
    const string FpCharacterPrefabPath =
        "Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Prefabs/P_LPSP_FP_CH.prefab";

    const string HandgunPrefabPath =
        "Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Prefabs/Weapons/P_LPSP_WEP_Handgun_03.prefab";

    const string ArmsRootName = "SK_FP_CH_Default_Root";

    [SerializeField] GameObject fpCharacterPrefab;
    [SerializeField] GameObject handgunPrefab;
    [SerializeField] ARInfimaPlayerCharacter playerCharacter;

    GameObject armsInstance;
    ARInfimaCharacterAnimator characterAnimator;

    public ARInfimaCharacterAnimator CharacterAnimator => characterAnimator;

    void Awake()
    {
        if (playerCharacter == null)
            playerCharacter = GetComponent<ARInfimaPlayerCharacter>();

        var registry = ARCombatAssetRegistry.Instance;
        if (registry != null)
        {
            if (fpCharacterPrefab == null)
                fpCharacterPrefab = registry.fpCharacterPrefab;

            if (handgunPrefab == null)
                handgunPrefab = registry.handgunPrefab;
        }

#if UNITY_EDITOR
        if (fpCharacterPrefab == null)
        {
            fpCharacterPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                FpCharacterPrefabPath);
        }

        if (handgunPrefab == null)
        {
            handgunPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                HandgunPrefabPath);
        }
#endif
    }

    void Start()
    {
        StartCoroutine(InstallWhenReady());
    }

    IEnumerator InstallWhenReady()
    {
        yield return null;
        yield return null;

        if (fpCharacterPrefab == null)
        {
            Debug.LogWarning("ARInfimaArmsInstaller: Assign P_LPSP_FP_CH prefab.", this);
            GameplayReadiness.MarkReady();
            yield break;
        }

        Transform existingArms = transform.Find(ArmsRootName);
        if (existingArms != null)
        {
            FinishArmsSetup(existingArms);
            yield break;
        }

        if (transform.Find("Infima Arms (AR)") != null)
        {
            GameplayReadiness.MarkReady();
            yield break;
        }

        armsInstance = Instantiate(fpCharacterPrefab, transform);
        armsInstance.name = "Infima Arms (AR)";
        armsInstance.transform.localPosition = Vector3.zero;
        armsInstance.transform.localRotation = Quaternion.identity;
        armsInstance.transform.localScale = Vector3.one;

        ARInfimaUiCleanup.DestroyClonedInfimaCanvases();
        DisableEmbeddedGameplay(armsInstance);

        Transform armsRoot = FindDeepChild(armsInstance.transform, ArmsRootName);
        if (armsRoot == null)
        {
            Debug.LogError("ARInfimaArmsInstaller: Could not find SK_FP_CH_Default_Root.", this);
            GameplayReadiness.MarkReady();
            yield break;
        }

        armsRoot.SetParent(transform, true);

        Destroy(armsInstance);
        armsInstance = armsRoot.gameObject;

        FinishArmsSetup(armsRoot);
    }

    void FinishArmsSetup(Transform armsRoot)
    {
        AlignArmsRootToView(armsRoot);
        EnsureViewLock(armsRoot);
        ConfigureViewCamera();
        var animator = armsRoot.GetComponent<Animator>();
        if (animator != null)
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var kinematics = armsRoot.GetComponent<CharacterKinematics>();

        WeaponBehaviour weapon = playerCharacter != null
            ? playerCharacter.GetEquippedWeapon()
            : GetComponentInChildren<WeaponBehaviour>(true);

        characterAnimator = armsRoot.GetComponent<ARInfimaCharacterAnimator>();
        if (characterAnimator == null)
            characterAnimator = armsRoot.gameObject.AddComponent<ARInfimaCharacterAnimator>();

        characterAnimator.Initialize(animator, kinematics, weapon, playerCharacter);

        AttachWeaponLikeInfimaInventory(armsRoot, weapon);
        WeaponBehaviour handgun = SpawnHandgun(armsRoot);
        RegisterLoadout(weapon, handgun);

        StripConflictingInfimaComponents(armsRoot.gameObject);

        if (playerCharacter != null && weapon != null &&
            playerCharacter.GetInventory() is ARInfimaWeaponInventory weaponInventory &&
            handgun == null)
            weaponInventory.Bind(weapon);

        var shooter = GetComponent<ARInfimaShooterInput>();
        if (shooter != null)
            shooter.BindCharacterAnimator(characterAnimator);

        GetComponent<ARInfimaWeaponBinder>()?.Bind();

        ARInfimaUiCleanup.KeepSingleInfimaHud();
        ARInfimaUiCleanup.ApplyToAllInfimaCanvasesInScene();
        GameplayReadiness.MarkReady();
    }

    static void DisableEmbeddedGameplay(GameObject root)
    {
        foreach (CanvasSpawner spawner in root.GetComponentsInChildren<CanvasSpawner>(true))
            Destroy(spawner);

        foreach (var camera in root.GetComponentsInChildren<Camera>(true))
        {
            camera.enabled = false;
            camera.gameObject.SetActive(false);
        }

        foreach (var listener in root.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;

        foreach (var input in root.GetComponentsInChildren<PlayerInput>(true))
            input.enabled = false;

        foreach (var movement in root.GetComponentsInChildren<Movement>(true))
            movement.enabled = false;

        foreach (var character in root.GetComponentsInChildren<Character>(true))
        {
            character.enabled = false;
            DestroyImmediate(character);
        }

        foreach (var inventory in root.GetComponentsInChildren<Inventory>(true))
        {
            inventory.enabled = false;
            DestroyImmediate(inventory);
        }

        foreach (var weapon in root.GetComponentsInChildren<WeaponBehaviour>(true))
            weapon.gameObject.SetActive(false);

        StripConflictingInfimaComponents(root);
    }

    static void StripConflictingInfimaComponents(GameObject root)
    {
        foreach (var cameraLook in root.GetComponentsInChildren<CameraLook>(true))
            Destroy(cameraLook);

        foreach (var movement in root.GetComponentsInChildren<Movement>(true))
            Destroy(movement);
    }

    void EnsureViewLock(Transform armsRoot)
    {
        Transform viewTransform = GetViewTransform();
        Transform socketCamera = FindDeepChild(armsRoot, "SOCKET_Camera");
        var viewLock = armsRoot.GetComponent<ARInfimaArmsViewLock>();
        if (viewLock == null)
            viewLock = armsRoot.gameObject.AddComponent<ARInfimaArmsViewLock>();

        viewLock.Init(viewTransform, socketCamera);
    }

    void ConfigureViewCamera()
    {
        Camera cam = playerCharacter != null ? playerCharacter.GetCameraWorld() : Camera.main;
        if (cam == null)
            return;

        int fpvLayer = LayerMask.NameToLayer("First Person View");
        if (fpvLayer >= 0)
            cam.cullingMask |= 1 << fpvLayer;

        if (cam.nearClipPlane > 0.05f)
            cam.nearClipPlane = 0.05f;

        if (cam.farClipPlane < 80f)
            cam.farClipPlane = 80f;
    }

    void AlignArmsRootToView(Transform armsRoot)
    {
        Transform viewTransform = GetViewTransform();
        Transform socketCamera = FindDeepChild(armsRoot, "SOCKET_Camera");
        if (socketCamera == null)
        {
            Debug.LogWarning(
                "ARInfimaArmsInstaller: SOCKET_Camera missing; arms may not align with the view.",
                this);
            return;
        }

        Quaternion rotationOffset =
            viewTransform.rotation * Quaternion.Inverse(socketCamera.rotation);
        armsRoot.rotation = rotationOffset * armsRoot.rotation;

        Vector3 positionOffset = viewTransform.position - socketCamera.position;
        armsRoot.position += positionOffset;
    }

    Transform GetViewTransform()
    {
        if (playerCharacter != null)
        {
            Camera cam = playerCharacter.GetCameraWorld();
            if (cam != null)
                return cam.transform;
        }

        Camera main = Camera.main;
        if (main != null)
            return main.transform;

        return transform;
    }

    void RegisterLoadout(WeaponBehaviour rifle, WeaponBehaviour handgun)
    {
        if (playerCharacter == null || rifle == null)
            return;

        if (playerCharacter.GetInventory() is not ARInfimaWeaponInventory weaponInventory)
            return;

        if (handgun == null)
        {
            weaponInventory.Bind(rifle);
            playerCharacter.RefreshEquippedFromInventory();
            return;
        }

        weaponInventory.SetLoadout(new[] { rifle, handgun }, 0);
        playerCharacter.RefreshEquippedFromInventory();
        characterAnimator?.SetWeapon(rifle);

        EnsureBridge(rifle);
        EnsureBridge(handgun);
    }

    void EnsureBridge(WeaponBehaviour weapon)
    {
        if (weapon == null)
            return;

        var bridge = weapon.GetComponent<ARInfimaWeaponAnimationBridge>();
        if (bridge == null)
            bridge = weapon.gameObject.AddComponent<ARInfimaWeaponAnimationBridge>();

        bridge.Init(playerCharacter, GetComponent<ARInfimaShooterInput>());
    }

    WeaponBehaviour SpawnHandgun(Transform armsRoot)
    {
        Transform ikHandGun = FindDeepChild(armsRoot, "ik_hand_gun");
        if (ikHandGun == null)
            return null;

        Transform existing = ikHandGun.Find("P_LPSP_WEP_Handgun_03");
        if (existing != null)
            return existing.GetComponent<WeaponBehaviour>();

        if (handgunPrefab == null)
        {
            Debug.LogWarning("ARInfimaArmsInstaller: Assign the handgun prefab to swap weapons.", this);
            return null;
        }

        GameObject instance = Instantiate(handgunPrefab, ikHandGun);
        instance.name = "P_LPSP_WEP_Handgun_03";
        WeaponBehaviour handgun = instance.GetComponent<WeaponBehaviour>();
        if (handgun == null)
        {
            Debug.LogWarning("ARInfimaArmsInstaller: Handgun prefab has no WeaponBehaviour.", instance);
            Destroy(instance);
            return null;
        }

        PlaceWeaponOnHand(ikHandGun, handgun, active: false);
        return handgun;
    }

    static void AttachWeaponLikeInfimaInventory(Transform armsRoot, WeaponBehaviour weapon)
    {
        if (armsRoot == null || weapon == null)
            return;

        Transform ikHandGun = FindDeepChild(armsRoot, "ik_hand_gun");
        if (ikHandGun == null)
        {
            Debug.LogWarning(
                "ARInfimaArmsInstaller: ik_hand_gun not found; weapon will stay on WeaponAnchor.",
                armsRoot);
            return;
        }

        DisableEmbeddedInventoryWeapons(ikHandGun);
        PlaceWeaponOnHand(ikHandGun, weapon, active: true);
    }

    static void PlaceWeaponOnHand(Transform ikHandGun, WeaponBehaviour weapon, bool active)
    {
        if (ikHandGun == null || weapon == null)
            return;

        int fpvLayer = LayerMask.NameToLayer("First Person View");
        if (fpvLayer < 0)
            fpvLayer = 9;

        weapon.transform.SetParent(ikHandGun, false);
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        weapon.transform.localScale = Vector3.one;
        SetLayerRecursively(weapon.gameObject, fpvLayer);

        Animator weaponAnimator = weapon.GetComponent<Animator>();
        if (weaponAnimator != null)
            weaponAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        weapon.gameObject.SetActive(active);

        foreach (var renderer in weapon.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = true;
    }

    static void DisableEmbeddedInventoryWeapons(Transform ikHandGun)
    {
        for (int i = 0; i < ikHandGun.childCount; i++)
        {
            Transform child = ikHandGun.GetChild(i);
            if (child.GetComponentInChildren<InventoryBehaviour>(true) != null)
                child.gameObject.SetActive(false);
        }

        foreach (WeaponBehaviour embedded in ikHandGun.GetComponentsInChildren<WeaponBehaviour>(true))
        {
            if (embedded.transform.parent == ikHandGun)
                continue;

            embedded.gameObject.SetActive(false);
        }
    }

    static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        for (int i = 0; i < root.transform.childCount; i++)
            SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
    }

    static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent.name == childName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
