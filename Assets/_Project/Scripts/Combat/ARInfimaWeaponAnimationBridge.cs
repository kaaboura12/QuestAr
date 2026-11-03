using UnityEngine;

/// <summary>
/// Receives animation events on the weapon mesh (reload fill, reload end).
/// </summary>
public class ARInfimaWeaponAnimationBridge : MonoBehaviour
{
    ARInfimaPlayerCharacter playerCharacter;
    ARInfimaShooterInput shooterInput;

    public void Init(ARInfimaPlayerCharacter character, ARInfimaShooterInput shooter)
    {
        playerCharacter = character;
        shooterInput = shooter;
    }

    void OnAmmunitionFill(int amount = 0)
    {
        playerCharacter?.FillAmmunition(amount);
    }

    void OnAnimationEndedReload()
    {
        shooterInput?.OnReloadAnimationEnded();
        playerCharacter?.AnimationEndedReload();
    }

    void OnAnimationEndedInspect()
    {
        shooterInput?.OnInspectAnimationEnded();
        playerCharacter?.AnimationEndedInspect();
    }
}
