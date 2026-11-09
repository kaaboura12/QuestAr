using UnityEngine;

/// <summary>
/// Ensures WeaponAnchor has the combat rig, including on-screen fire and aim for a phone.
/// </summary>
[DefaultExecutionOrder(-300)]
public class ARInfimaWeaponRig : MonoBehaviour
{
    void Awake()
    {
        if (GetComponent<ARInfimaPlayerCharacter>() == null)
            gameObject.AddComponent<ARInfimaPlayerCharacter>();

        if (GetComponent<ARInfimaShooterInput>() == null)
            gameObject.AddComponent<ARInfimaShooterInput>();

        if (GetComponent<ARInfimaWeaponBinder>() == null)
            gameObject.AddComponent<ARInfimaWeaponBinder>();

        if (GetComponent<ARInfimaArmsInstaller>() == null)
            gameObject.AddComponent<ARInfimaArmsInstaller>();

        if (GetComponent<ARInfimaCombatHud>() == null)
            gameObject.AddComponent<ARInfimaCombatHud>();

        if (GetComponent<ARInfimaPlayerMotor>() == null)
            gameObject.AddComponent<ARInfimaPlayerMotor>();

        if (GetComponent<ARInfimaDesktopLook>() == null)
            gameObject.AddComponent<ARInfimaDesktopLook>();

        if (GetComponent<ARInfimaAimPoint>() == null)
            gameObject.AddComponent<ARInfimaAimPoint>();

        if (GetComponent<QuestARLevelManager>() == null)
            gameObject.AddComponent<QuestARLevelManager>();

        if (GetComponent<ARMobileCombatControls>() == null)
            gameObject.AddComponent<ARMobileCombatControls>();

        if (GetComponent<ARInfimaWeaponSwitcher>() == null)
            gameObject.AddComponent<ARInfimaWeaponSwitcher>();

        if (GetComponent<ARGameplayAudio>() == null)
            gameObject.AddComponent<ARGameplayAudio>();

        if (GetComponent<ARGameOver>() == null)
            gameObject.AddComponent<ARGameOver>();

        if (GetComponent<ARZombieVitals>() == null)
            gameObject.AddComponent<ARZombieVitals>();

        if (GetComponent<HamaFlashStunManager>() == null)
            gameObject.AddComponent<HamaFlashStunManager>();

        if (GetComponent<ARZombieSpawner>() == null)
            gameObject.AddComponent<ARZombieSpawner>();

        if (GetComponent<ARMicDetectionManager>() == null)
            gameObject.AddComponent<ARMicDetectionManager>();

        if (GetComponent<ARAlertVignetteController>() == null)
            gameObject.AddComponent<ARAlertVignetteController>();

        if (GetComponent<ARMicTestWindow>() == null)
            gameObject.AddComponent<ARMicTestWindow>();
    }
}
