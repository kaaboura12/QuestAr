using System.Collections;
using InfimaGames.LowPolyShooterPack;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// B holsters the current gun, swaps to the other one, and draws it.
/// The new weapon's animator controller brings its own walk, fire, and reload clips.
/// </summary>
[DefaultExecutionOrder(80)]
public class ARInfimaWeaponSwitcher : MonoBehaviour
{
    ARInfimaPlayerCharacter playerCharacter;
    ARInfimaShooterInput shooterInput;
    ARInfimaCharacterAnimator characterAnimator;
    ARInfimaWeaponInventory inventory;
    bool swapping;
    bool stageComplete;

    void Awake()
    {
        playerCharacter = GetComponent<ARInfimaPlayerCharacter>();
        shooterInput = GetComponent<ARInfimaShooterInput>();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.bKey.wasPressedThisFrame)
            TrySwap();
    }

    public void TrySwap()
    {
        if (swapping)
            return;

        if (inventory == null && playerCharacter != null)
            inventory = playerCharacter.GetInventory() as ARInfimaWeaponInventory;

        if (inventory == null || inventory.Count < 2)
            return;

        if (shooterInput != null && !shooterInput.CanSwapWeapon())
            return;

        StartCoroutine(SwapRoutine());
    }

    public void OnHolsterAnimationEnded()
    {
        stageComplete = true;
    }

    IEnumerator SwapRoutine()
    {
        swapping = true;
        playerCharacter?.SetSwapping(true);
        playerCharacter?.SetAiming(false);
        ResolveAnimator();

        stageComplete = false;
        characterAnimator?.BeginHolster();
        yield return WaitForStage(1.25f);

        int next = inventory.GetNextIndex();
        inventory.Equip(next);
        playerCharacter?.RefreshEquippedFromInventory();

        WeaponBehaviour weapon = inventory.GetEquipped();
        ResolveAnimator();
        characterAnimator?.SetWeapon(weapon);
        characterAnimator?.PlayUnholster();
        shooterInput?.NotifyWeaponChanged();

        stageComplete = false;
        yield return WaitForStage(1.4f);

        characterAnimator?.SetHolstered(false);
        playerCharacter?.SetSwapping(false);
        swapping = false;
    }

    IEnumerator WaitForStage(float timeout)
    {
        float elapsed = 0f;
        while (!stageComplete && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    void ResolveAnimator()
    {
        if (characterAnimator == null)
            characterAnimator = GetComponentInChildren<ARInfimaCharacterAnimator>(true);
    }
}
