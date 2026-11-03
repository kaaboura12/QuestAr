using System;
using InfimaGames.LowPolyShooterPack;
using UnityEngine;

/// <summary>
/// Drives Infima first-person arms (locomotion blend, fire/reload/inspect) for AR.
/// </summary>
[DefaultExecutionOrder(100)]
public class ARInfimaCharacterAnimator : MonoBehaviour
{
    static readonly int HashMovement = Animator.StringToHash("Movement");
    static readonly int HashAimingAlpha = Animator.StringToHash("Aiming");

    const float DampTimeLocomotion = 0.15f;
    const float DampTimeAiming = 0.3f;

    Animator characterAnimator;
    CharacterKinematics characterKinematics;
    WeaponBehaviour equippedWeapon;
    ARInfimaPlayerCharacter playerCharacter;

    ScopeBehaviour equippedScope;
    Vector3 weaponBaseLocalPosition;
    bool weaponPoseCached;
    float bobTime;

    int layerOverlay;
    int layerHolster;
    int layerActions;
    bool layersCached;

    public void Initialize(
        Animator animator,
        CharacterKinematics kinematics,
        WeaponBehaviour weapon,
        ARInfimaPlayerCharacter character)
    {
        characterAnimator = animator;
        characterKinematics = kinematics;
        playerCharacter = character;

        EnsureAnimationEventHandler();
        SetWeapon(weapon);
    }

    public void SetWeapon(WeaponBehaviour weapon)
    {
        equippedWeapon = weapon;
        if (characterAnimator == null || equippedWeapon == null)
            return;

        RuntimeAnimatorController controller = equippedWeapon.GetAnimatorController();
        if (controller != null)
            characterAnimator.runtimeAnimatorController = controller;

        WeaponAttachmentManagerBehaviour attachments = equippedWeapon.GetAttachmentManager();
        if (attachments != null)
            equippedScope = attachments.GetEquippedScope();

        weaponPoseCached = false;
        characterAnimator.SetBool("Holstered", false);
        layersCached = false;
        CacheLayers();
        for (int i = 0; i < characterAnimator.layerCount; i++)
            characterAnimator.SetLayerWeight(i, 1f);
    }

    void EnsureAnimationEventHandler()
    {
        if (GetComponent<CharacterAnimationEventHandler>() == null)
            gameObject.AddComponent<CharacterAnimationEventHandler>();
    }

    void Start() => CacheLayers();

    void Update()
    {
        if (characterAnimator == null || playerCharacter == null)
            return;

        CacheLayers();

        Vector2 move = playerCharacter.GetInputMovement();
        float fromKeys = Mathf.Clamp01(Mathf.Abs(move.x) + Mathf.Abs(move.y));
        float fromWorld = Mathf.Clamp01(playerCharacter.GetPlanarSpeed() / 4.2f);
        float movement = Mathf.Max(fromKeys, fromWorld);
        characterAnimator.SetFloat(
            HashMovement, movement, DampTimeLocomotion, Time.deltaTime);

        bool aiming = playerCharacter.IsAiming();
        characterAnimator.SetFloat(
            HashAimingAlpha,
            Convert.ToSingle(aiming),
            0.25f / 1.0f * DampTimeAiming,
            Time.deltaTime);
        characterAnimator.SetBool("Aim", aiming);
        characterAnimator.SetBool("Running", playerCharacter.IsRunning());
    }

    void LateUpdate()
    {
        ApplyHipFireBob();

        if (equippedWeapon == null || equippedScope == null || characterKinematics == null)
            return;

        characterKinematics.Compute();
    }

    /// <summary>
    /// Hip-fire walk clips are tiny additive deltas, so the arms look frozen until ADS.
    /// A small weapon bob makes walking readable from the hip without fighting the aim pose.
    /// </summary>
    void ApplyHipFireBob()
    {
        if (equippedWeapon == null || playerCharacter == null)
            return;

        if (playerCharacter.IsSwapping())
        {
            if (weaponPoseCached)
                equippedWeapon.transform.localPosition = weaponBaseLocalPosition;
            return;
        }

        if (!weaponPoseCached)
        {
            weaponBaseLocalPosition = equippedWeapon.transform.localPosition;
            weaponPoseCached = true;
        }

        Vector2 move = playerCharacter.GetInputMovement();
        float amount = Mathf.Max(
            Mathf.Clamp01(Mathf.Abs(move.x) + Mathf.Abs(move.y)),
            Mathf.Clamp01(playerCharacter.GetPlanarSpeed() / 4.2f));

        if (amount < 0.01f)
        {
            bobTime = 0f;
            equippedWeapon.transform.localPosition = Vector3.Lerp(
                equippedWeapon.transform.localPosition,
                weaponBaseLocalPosition,
                12f * Time.deltaTime);
            return;
        }

        float frequency = playerCharacter.IsRunning() ? 2.35f : 1.7f;
        float scale = playerCharacter.IsAiming() ? 0.25f : 1f;
        bobTime += Time.deltaTime * frequency * Mathf.PI * 2f;
        Vector3 offset = new Vector3(
            Mathf.Sin(bobTime) * 0.008f,
            Mathf.Cos(bobTime * 2f) * 0.006f,
            0f) * scale * amount;
        equippedWeapon.transform.localPosition = weaponBaseLocalPosition + offset;
    }

    void CacheLayers()
    {
        if (characterAnimator == null || layersCached)
            return;

        layerHolster = ResolveLayer("Layer Holster", 2);
        layerActions = ResolveLayer("Layer Actions", 3);
        layerOverlay = ResolveLayer("Layer Overlay", 1);
        layersCached = true;
    }

    int ResolveLayer(string layerName, int fallback)
    {
        int index = characterAnimator.GetLayerIndex(layerName);
        return index >= 0 ? index : fallback;
    }

    public void PlayFireAnimation()
    {
        if (characterAnimator == null)
            return;

        CacheLayers();
        characterAnimator.CrossFade("Fire", 0.05f, layerOverlay, 0f);
    }

    public void PlayFireEmptyAnimation()
    {
        if (characterAnimator == null)
            return;

        CacheLayers();
        characterAnimator.CrossFade("Fire Empty", 0.05f, layerOverlay, 0f);
    }

    public void PlayReloadAnimation()
    {
        if (characterAnimator == null || equippedWeapon == null)
            return;

        CacheLayers();
        string stateName = equippedWeapon.HasAmmunition() ? "Reload" : "Reload Empty";
        characterAnimator.Play(stateName, layerActions, 0f);
    }

    public void PlayInspectAnimation()
    {
        if (characterAnimator == null)
            return;

        CacheLayers();
        characterAnimator.CrossFade("Inspect", 0f, layerActions, 0f);
    }

    public void BeginHolster()
    {
        if (characterAnimator == null)
            return;

        CacheLayers();
        characterAnimator.SetBool("Holstered", true);
        characterAnimator.CrossFade("Holster", 0.05f, layerHolster, 0f);
    }

    public void PlayUnholster()
    {
        if (characterAnimator == null)
            return;

        CacheLayers();
        characterAnimator.SetBool("Holstered", false);
        characterAnimator.Play("Unholster", layerHolster, 0f);
    }

    public void SetHolstered(bool value)
    {
        if (characterAnimator == null)
            return;

        characterAnimator.SetBool("Holstered", value);
    }
}
