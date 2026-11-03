using InfimaGames.LowPolyShooterPack;
using UnityEngine;

/// <summary>
/// Minimal Infima character for AR: provides camera + inventory so Weapon.cs can raycast and fire.
/// </summary>
[DefaultExecutionOrder(-200)]
public class ARInfimaPlayerCharacter : CharacterBehaviour
{
    [SerializeField] Camera worldCamera;
    [SerializeField] InventoryBehaviour inventory;

    WeaponBehaviour equippedWeapon;
    MagazineBehaviour equippedMagazine;

    bool aiming;
    bool running;
    bool swapping;
    Vector2 inputMovement;
    float planarSpeed;
    ARInfimaShooterInput shooterInput;

    void Awake()
    {
        if (worldCamera == null)
            worldCamera = GetComponentInParent<Camera>();

        if (worldCamera == null)
            worldCamera = Camera.main;

        EnsurePlayerColliderForProjectiles();

        if (inventory == null)
            inventory = GetComponent<InventoryBehaviour>();

        if (inventory == null)
        {
            var arInventory = GetComponent<ARInfimaWeaponInventory>();
            if (arInventory == null)
                arInventory = gameObject.AddComponent<ARInfimaWeaponInventory>();
            inventory = arInventory;
        }

        inventory?.Init();
        CacheEquippedWeapon();

        if (inventory is ARInfimaWeaponInventory weaponInventory && equippedWeapon != null)
            weaponInventory.Bind(equippedWeapon);

        shooterInput = GetComponent<ARInfimaShooterInput>();
    }

    public void CacheEquippedWeaponFromScene() => CacheEquippedWeapon();

    public void RefreshEquippedFromInventory()
    {
        equippedWeapon = inventory != null ? inventory.GetEquipped() : null;
        equippedMagazine = null;
        if (equippedWeapon == null)
            return;

        WeaponAttachmentManagerBehaviour attachments = equippedWeapon.GetAttachmentManager();
        if (attachments != null)
            equippedMagazine = attachments.GetEquippedMagazine();
    }

    /// <summary>
    /// Infima Projectile.cs calls Physics.IgnoreCollision on the player CharacterBehaviour collider.
    /// </summary>
    void EnsurePlayerColliderForProjectiles()
    {
        if (GetComponent<Collider>() != null)
            return;

        var capsule = gameObject.AddComponent<CapsuleCollider>();
        capsule.isTrigger = true;
        capsule.radius = 0.15f;
        capsule.height = 0.3f;
        capsule.center = Vector3.zero;
    }

    void CacheEquippedWeapon()
    {
        equippedWeapon = inventory != null ? inventory.GetEquipped() : null;
        if (equippedWeapon == null)
            equippedWeapon = GetComponentInChildren<WeaponBehaviour>(true);

        if (equippedWeapon != null)
        {
            var attachments = equippedWeapon.GetAttachmentManager();
            if (attachments != null)
                equippedMagazine = attachments.GetEquippedMagazine();

            if (inventory is ARInfimaWeaponInventory weaponInventory)
                weaponInventory.Bind(equippedWeapon);
        }
    }

    public override Camera GetCameraWorld() => worldCamera;

    public override InventoryBehaviour GetInventory() => inventory;

    public void SetAiming(bool value) => aiming = value;

    public void SetRunning(bool value) => running = value;

    public void SetSwapping(bool value) => swapping = value;

    public bool IsSwapping() => swapping;

    public void SetInputMovement(Vector2 value) => inputMovement = value;

    /// <summary>Planar meters per second of the rig, including real-world camera motion.</summary>
    public void SetPlanarSpeed(float metersPerSecond) => planarSpeed = Mathf.Max(0f, metersPerSecond);

    public float GetPlanarSpeed() => planarSpeed;

    public override bool IsCrosshairVisible() => true;
    public override bool IsRunning() => running;
    public override bool IsAiming() => aiming;
    public override bool IsCursorLocked() => false;
    public override bool IsTutorialTextVisible() => false;
    public override Vector2 GetInputMovement() => inputMovement;
    public override Vector2 GetInputLook() => Vector2.zero;

    public override void EjectCasing()
    {
        equippedWeapon?.EjectCasing();
    }

    public override void FillAmmunition(int amount)
    {
        equippedWeapon?.FillAmmunition(amount);
    }

    public override void SetActiveMagazine(int active)
    {
        if (equippedMagazine != null)
            equippedMagazine.gameObject.SetActive(active != 0);
    }

    public override void AnimationEndedReload()
    {
        shooterInput?.OnReloadAnimationEnded();
    }

    public override void AnimationEndedInspect()
    {
        shooterInput?.OnInspectAnimationEnded();
    }

    public override void AnimationEndedHolster()
    {
        GetComponent<ARInfimaWeaponSwitcher>()?.OnHolsterAnimationEnded();
    }

    public WeaponBehaviour GetEquippedWeapon()
    {
        if (equippedWeapon == null)
            CacheEquippedWeapon();
        return equippedWeapon;
    }
}
