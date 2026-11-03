using InfimaGames.LowPolyShooterPack;
using UnityEngine;

/// <summary>
/// Links scene weapon, arms animator override, and weapon animation event bridge.
/// </summary>
[DefaultExecutionOrder(-50)]
public class ARInfimaWeaponBinder : MonoBehaviour
{
    [SerializeField] ARInfimaPlayerCharacter playerCharacter;
    [SerializeField] ARInfimaShooterInput shooterInput;

    void Awake()
    {
        if (playerCharacter == null)
            playerCharacter = GetComponent<ARInfimaPlayerCharacter>();

        if (shooterInput == null)
            shooterInput = GetComponent<ARInfimaShooterInput>();
    }

    public void Bind()
    {
        if (playerCharacter == null)
            return;

        playerCharacter.CacheEquippedWeaponFromScene();

        WeaponBehaviour weapon = playerCharacter.GetEquippedWeapon();
        if (weapon == null)
        {
            Debug.LogWarning("ARInfimaWeaponBinder: No WeaponBehaviour under WeaponAnchor.", this);
            return;
        }

        ARInfimaCharacterAnimator armsAnimator =
            GetComponentInChildren<ARInfimaCharacterAnimator>(true);

        if (armsAnimator != null)
            armsAnimator.SetWeapon(weapon);

        EnsureWeaponAnimationBridge(weapon);
        shooterInput?.BindCharacterAnimator(armsAnimator);
    }

    static void EnsureWeaponAnimationBridge(WeaponBehaviour weapon)
    {
        if (weapon == null)
            return;

        GameObject weaponObject = weapon.gameObject;
        var bridge = weaponObject.GetComponent<ARInfimaWeaponAnimationBridge>();
        if (bridge == null)
            bridge = weaponObject.AddComponent<ARInfimaWeaponAnimationBridge>();

        var player = weaponObject.GetComponentInParent<ARInfimaPlayerCharacter>();
        var shooter = weaponObject.GetComponentInParent<ARInfimaShooterInput>();
        bridge.Init(player, shooter);
    }
}
