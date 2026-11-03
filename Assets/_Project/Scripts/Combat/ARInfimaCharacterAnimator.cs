using System;
using InfimaGames.LowPolyShooterPack;
using UnityEngine;
using UnityEngine.InputSystem;
using Gyroscope = UnityEngine.InputSystem.Gyroscope;

/// <summary>
/// Drives Infima first-person arms (locomotion blend, gyroscope motion response, fire/reload/inspect) for AR.
/// </summary>
[DefaultExecutionOrder(100)]
public class ARInfimaCharacterAnimator : MonoBehaviour
{
    static readonly int HashMovement = Animator.StringToHash("Movement");
    static readonly int HashHorizontal = Animator.StringToHash("Horizontal");
    static readonly int HashVertical = Animator.StringToHash("Vertical");
    static readonly int HashAimingAlpha = Animator.StringToHash("Aiming");

    const float DampTimeLocomotion = 0.15f;
    const float DampTimeAiming = 0.3f;
    const float GyroDeadzoneRadPerSec = 0.06f;
    const float GyroFullWalkRadPerSec = 1.4f;
    const float MaxGyroWalkMovement = 0.65f;

    Animator characterAnimator;
    CharacterKinematics characterKinematics;
    WeaponBehaviour equippedWeapon;
    ARInfimaPlayerCharacter playerCharacter;

    ScopeBehaviour equippedScope;
    Vector3 weaponBaseLocalPosition;
    Quaternion weaponBaseLocalRotation = Quaternion.identity;
    bool weaponPoseCached;
    float bobTime;
    float idleTime;

    Quaternion lastCameraRotation = Quaternion.identity;
    bool hasLastCameraRotation;
    float smoothedGyroMovement;
    Vector2 smoothedGyroAxes;
    Vector3 smoothedGyroSway;

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
        if (characterAnimator != null)
            characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        characterKinematics = kinematics;
        playerCharacter = character;

        EnsureGyroEnabled();
        EnsureAnimationEventHandler();
        SetWeapon(weapon);
    }

    void OnEnable()
    {
        EnsureGyroEnabled();
        hasLastCameraRotation = false;
    }

    static void EnsureGyroEnabled()
    {
        if (Gyroscope.current != null && !Gyroscope.current.enabled)
            InputSystem.EnableDevice(Gyroscope.current);

        if (AttitudeSensor.current != null && !AttitudeSensor.current.enabled)
            InputSystem.EnableDevice(AttitudeSensor.current);
    }

    public void SetWeapon(WeaponBehaviour weapon)
    {
        if (equippedWeapon != null && weaponPoseCached)
        {
            equippedWeapon.transform.localPosition = weaponBaseLocalPosition;
            equippedWeapon.transform.localRotation = weaponBaseLocalRotation;
        }

        equippedWeapon = weapon;
        if (characterAnimator == null || equippedWeapon == null)
            return;

        characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

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

    void Start()
    {
        EnsureGyroEnabled();
        CacheLayers();
    }

    void Update()
    {
        if (characterAnimator == null || playerCharacter == null)
            return;

        CacheLayers();
        SampleGyroMotion(Time.deltaTime);

        Vector2 move = playerCharacter.GetInputMovement();
        float fromKeys = Mathf.Clamp01(Mathf.Abs(move.x) + Mathf.Abs(move.y));
        float fromWorld = Mathf.Clamp01(playerCharacter.GetPlanarSpeed() / 4.2f);
        float movement = Mathf.Max(fromKeys, Mathf.Max(fromWorld, smoothedGyroMovement));

        float horiz = Mathf.Abs(move.x) > 0.01f ? move.x : smoothedGyroAxes.x;
        float vert = Mathf.Abs(move.y) > 0.01f ? move.y : Mathf.Max(smoothedGyroAxes.y, movement);

        characterAnimator.SetFloat(
            HashMovement, movement, DampTimeLocomotion, Time.deltaTime);
        characterAnimator.SetFloat(
            HashHorizontal, horiz, DampTimeLocomotion, Time.deltaTime);
        characterAnimator.SetFloat(
            HashVertical, vert, DampTimeLocomotion, Time.deltaTime);

        bool aiming = playerCharacter.IsAiming();
        characterAnimator.SetFloat(
            HashAimingAlpha,
            Convert.ToSingle(aiming),
            0.25f / 1.0f * DampTimeAiming,
            Time.deltaTime);
        characterAnimator.SetBool("Aim", aiming);
        characterAnimator.SetBool("Running", playerCharacter.IsRunning());
    }

    void SampleGyroMotion(float dt)
    {
        if (dt <= 0.0001f)
            return;

        EnsureGyroEnabled();

        Vector3 gyroRadPerSec = Vector3.zero;
        if (Gyroscope.current != null && Gyroscope.current.enabled)
            gyroRadPerSec = Gyroscope.current.angularVelocity.ReadValue();

        Vector3 camRadPerSec = Vector3.zero;
        Camera cam = playerCharacter != null ? playerCharacter.GetCameraWorld() : Camera.main;
        if (cam != null)
        {
            Quaternion currentRot = cam.transform.rotation;
            if (hasLastCameraRotation)
            {
                Quaternion deltaRot = Quaternion.Inverse(lastCameraRotation) * currentRot;
                deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
                if (angleDeg > 180f)
                    angleDeg -= 360f;

                if (Mathf.Abs(angleDeg) > 0.001f && float.IsFinite(angleDeg))
                {
                    Vector3 angularDegPerSec = axis * (angleDeg / dt);
                    camRadPerSec = angularDegPerSec * Mathf.Deg2Rad;
                }
            }

            lastCameraRotation = currentRot;
            hasLastCameraRotation = true;
        }

        Vector3 angularVel = gyroRadPerSec.sqrMagnitude >= camRadPerSec.sqrMagnitude
            ? gyroRadPerSec
            : camRadPerSec;

        float angularSpeed = angularVel.magnitude;
        float normalizedGyro = 0f;
        if (angularSpeed > GyroDeadzoneRadPerSec)
        {
            normalizedGyro = Mathf.Clamp01(
                (angularSpeed - GyroDeadzoneRadPerSec) / (GyroFullWalkRadPerSec - GyroDeadzoneRadPerSec));
        }

        float targetMovement = normalizedGyro * MaxGyroWalkMovement;
        float smoothRate = targetMovement > smoothedGyroMovement ? 9f : 5.5f;
        smoothedGyroMovement = Mathf.Lerp(
            smoothedGyroMovement,
            targetMovement,
            1f - Mathf.Exp(-smoothRate * dt));

        Vector2 targetAxes = new Vector2(
            Mathf.Clamp(-angularVel.y / GyroFullWalkRadPerSec, -0.65f, 0.65f),
            Mathf.Clamp(angularVel.x / GyroFullWalkRadPerSec, -0.65f, 0.65f));
        smoothedGyroAxes = Vector2.Lerp(
            smoothedGyroAxes,
            targetAxes,
            1f - Mathf.Exp(-7f * dt));

        Vector3 targetSway = new Vector3(
            Mathf.Clamp(-angularVel.y * 0.0045f, -0.0085f, 0.0085f),
            Mathf.Clamp(angularVel.x * 0.0040f, -0.0075f, 0.0075f),
            Mathf.Clamp(-angularVel.z * 0.0030f, -0.0050f, 0.0050f));
        smoothedGyroSway = Vector3.Lerp(
            smoothedGyroSway,
            targetSway,
            1f - Mathf.Exp(-10f * dt));
    }

    void LateUpdate()
    {
        ApplyHipFireBob();

        if (equippedWeapon == null || characterKinematics == null)
            return;

        if (equippedScope == null)
        {
            var attachments = equippedWeapon.GetAttachmentManager();
            if (attachments != null)
                equippedScope = attachments.GetEquippedScope();
        }

        if (equippedScope == null)
            return;

        characterKinematics.Compute();
    }

    /// <summary>
    /// Hip-fire walk clips are tiny additive deltas, so the arms look frozen until ADS.
    /// A small weapon bob + gyroscope sway makes walking and idle movement readable from the hip.
    /// </summary>
    void ApplyHipFireBob()
    {
        if (equippedWeapon == null || playerCharacter == null)
            return;

        if (playerCharacter.IsSwapping())
        {
            if (weaponPoseCached)
            {
                equippedWeapon.transform.localPosition = weaponBaseLocalPosition;
                equippedWeapon.transform.localRotation = weaponBaseLocalRotation;
            }
            return;
        }

        if (!weaponPoseCached)
        {
            weaponBaseLocalPosition = equippedWeapon.transform.localPosition;
            weaponBaseLocalRotation = equippedWeapon.transform.localRotation;
            weaponPoseCached = true;
        }

        Vector2 move = playerCharacter.GetInputMovement();
        float amount = Mathf.Max(
            Mathf.Clamp01(Mathf.Abs(move.x) + Mathf.Abs(move.y)),
            Mathf.Max(
                Mathf.Clamp01(playerCharacter.GetPlanarSpeed() / 4.2f),
                smoothedGyroMovement));

        float scale = playerCharacter.IsAiming() ? 0.25f : 1f;
        idleTime += Time.deltaTime * 1.6f;

        Vector3 idleOffset = new Vector3(
            Mathf.Sin(idleTime * 0.8f) * 0.0012f,
            Mathf.Sin(idleTime * 1.6f) * 0.0018f,
            0f) * scale;

        Vector3 walkOffset = Vector3.zero;
        if (amount >= 0.01f)
        {
            float frequency = playerCharacter.IsRunning() ? 2.35f : 1.75f;
            bobTime += Time.deltaTime * frequency * Mathf.PI * 2f;
            walkOffset = new Vector3(
                Mathf.Sin(bobTime) * 0.009f,
                Mathf.Cos(bobTime * 2f) * 0.007f,
                0f) * (scale * amount);
        }
        else
        {
            bobTime = 0f;
        }

        Vector3 totalOffset = Vector3.Lerp(idleOffset, walkOffset, Mathf.Clamp01(amount * 2f))
            + smoothedGyroSway * scale;

        equippedWeapon.transform.localPosition = Vector3.Lerp(
            equippedWeapon.transform.localPosition,
            weaponBaseLocalPosition + totalOffset,
            14f * Time.deltaTime);

        Quaternion swayRotation = Quaternion.Euler(
            smoothedGyroSway.y * -210f * scale,
            smoothedGyroSway.x * 240f * scale,
            smoothedGyroSway.x * -180f * scale);
        equippedWeapon.transform.localRotation = Quaternion.Slerp(
            equippedWeapon.transform.localRotation,
            weaponBaseLocalRotation * swayRotation,
            12f * Time.deltaTime);
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
