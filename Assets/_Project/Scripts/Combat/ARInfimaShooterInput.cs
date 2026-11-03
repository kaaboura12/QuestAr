using System.Collections;
using InfimaGames.LowPolyShooterPack;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Weapon controls for editor play and a phone. Keyboard and mouse stay available in the editor.
/// On a device, fire and aim come only from the on-screen buttons, because a touch is otherwise
/// reported as a mouse click and would shoot from anywhere on the screen.
/// </summary>
[DefaultExecutionOrder(-100)]
public class ARInfimaShooterInput : MonoBehaviour
{
    const string InputAssetPath =
        "Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Input/IA_Player.inputactions";

    [SerializeField] ARInfimaPlayerCharacter playerCharacter;
    [SerializeField] InputActionAsset inputActions;

    InputAction fireAction;
    InputAction aimAction;
    InputAction movementAction;
    InputAction reloadAction;
    InputAction inspectAction;
    InputAction runAction;

    WeaponBehaviour weapon;
    ARInfimaCharacterAnimator characterAnimator;
    Coroutine reloadFallback;
    float lastShotTime;
    bool holdingButtonFire;
    bool holdingButtonAim;
    bool virtualFireHeld;
    bool virtualAimHeld;
    bool virtualFireWasHeld;
    bool reloading;
    bool inspecting;

    public bool TouchFireHeld => virtualFireHeld;
    public bool TouchAimHeld => virtualAimHeld;

    public void SetTouchFireHeld(bool held)
    {
        if (held && IsPlayerDead())
            held = false;
        virtualFireHeld = held;
    }

    public void SetTouchAimHeld(bool held)
    {
        if (held && IsPlayerDead())
            held = false;
        virtualAimHeld = held;
    }

    public void BindCharacterAnimator(ARInfimaCharacterAnimator animator)
    {
        characterAnimator = animator;
    }

    void Awake()
    {
        if (playerCharacter == null)
            playerCharacter = GetComponent<ARInfimaPlayerCharacter>();

        if (inputActions == null)
        {
#if UNITY_EDITOR
            inputActions = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
#endif
        }

        if (inputActions != null)
        {
            var map = inputActions.FindActionMap("Player", throwIfNotFound: true);
            fireAction = map.FindAction("Fire", throwIfNotFound: true);
            aimAction = map.FindAction("Aim", throwIfNotFound: true);
            movementAction = map.FindAction("Movement", throwIfNotFound: true);
            reloadAction = map.FindAction("Reload", throwIfNotFound: true);
            inspectAction = map.FindAction("Inspect", throwIfNotFound: true);
            runAction = map.FindAction("Run", throwIfNotFound: false);
        }
    }

    void Start()
    {
        ResolveWeapon();
        GetComponent<ARInfimaWeaponBinder>()?.Bind();
        EnsureWeaponAnimationBridge();

        var legacyPlayerInput = GetComponent<PlayerInput>();
        if (legacyPlayerInput != null)
            legacyPlayerInput.enabled = false;
    }

    void OnEnable()
    {
        if (inputActions == null)
            return;

        inputActions.Enable();
        if (fireAction != null)
        {
            fireAction.started += OnFireStarted;
            fireAction.performed += OnFirePerformed;
            fireAction.canceled += OnFireCanceled;
        }

        if (reloadAction != null)
            reloadAction.performed += OnReloadPerformed;

        if (inspectAction != null)
            inspectAction.performed += OnInspectPerformed;

        if (aimAction != null)
        {
            aimAction.started += OnAimStarted;
            aimAction.canceled += OnAimCanceled;
        }
    }

    void OnDisable()
    {
        if (fireAction != null)
        {
            fireAction.started -= OnFireStarted;
            fireAction.performed -= OnFirePerformed;
            fireAction.canceled -= OnFireCanceled;
        }

        if (reloadAction != null)
            reloadAction.performed -= OnReloadPerformed;

        if (inspectAction != null)
            inspectAction.performed -= OnInspectPerformed;

        if (aimAction != null)
        {
            aimAction.started -= OnAimStarted;
            aimAction.canceled -= OnAimCanceled;
        }

        inputActions?.Disable();
    }

    void Update()
    {
        if (IsPlayerDead())
        {
            virtualFireHeld = false;
            virtualAimHeld = false;
            virtualFireWasHeld = false;
            holdingButtonFire = false;
            holdingButtonAim = false;
            if (playerCharacter != null)
            {
                playerCharacter.SetInputMovement(Vector2.zero);
                playerCharacter.SetRunning(false);
                playerCharacter.SetAiming(false);
            }

            return;
        }

        if (weapon == null)
            ResolveWeapon();

        SyncMovementInput();
    }

    void LateUpdate()
    {
        if (IsPlayerDead())
        {
            virtualFireHeld = false;
            virtualAimHeld = false;
            virtualFireWasHeld = false;
            holdingButtonFire = false;
            holdingButtonAim = false;
            return;
        }

        if (weapon == null)
            ResolveWeapon();

        // After the UI has seen this frame's press, so a thumb on FIRE does not also count as a click.
        PollHardwareFallback();
        SyncAimingState();
        SyncRunningState();

        if (weapon == null)
        {
            virtualFireWasHeld = virtualFireHeld;
            return;
        }

        bool touchEdge = virtualFireHeld && !virtualFireWasHeld;
        virtualFireWasHeld = virtualFireHeld;

        if (touchEdge)
            TryFireSemiAuto();
        else if (holdingButtonFire && CanPlayAnimationFire() &&
                 weapon.HasAmmunition() && weapon.IsAutomatic() &&
                 Time.time - lastShotTime > 60f / weapon.GetRateOfFire())
            Fire();
    }

    void PollHardwareFallback()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.rKey.wasPressedThisFrame)
                TryReload();

            if (keyboard.tKey.wasPressedThisFrame)
                TryInspect();
        }

        bool hardwareFire = false;
        bool hardwareAim = false;
        if (!DeviceUsesTouchCombat())
        {
            var mouse = Mouse.current;
            bool overControls = mouse != null &&
                                ARMobileCombatControls.ContainsScreenPoint(mouse.position.ReadValue());
            if (!overControls)
            {
                if (mouse != null)
                {
                    // Mouse buttons stay for editor play. Action callbacks alone drop under AR / XR,
                    // which used to leave semi-auto weapons unable to fire.
                    hardwareFire = mouse.leftButton.isPressed;
                    hardwareAim = mouse.rightButton.isPressed;
                    if (weapon != null && !weapon.IsAutomatic() && mouse.leftButton.wasPressedThisFrame)
                        TryFireSemiAuto();
                }

                if (aimAction != null && aimAction.IsPressed())
                    hardwareAim = true;
            }
        }

        holdingButtonFire = virtualFireHeld || hardwareFire;
        holdingButtonAim = virtualAimHeld || hardwareAim;
    }

    static bool DeviceUsesTouchCombat()
    {
        return Application.isMobilePlatform;
    }

    bool IgnoreHardwareFire()
    {
        if (DeviceUsesTouchCombat())
            return true;

        var mouse = Mouse.current;
        return mouse != null &&
               ARMobileCombatControls.ContainsScreenPoint(mouse.position.ReadValue());
    }

    void SyncMovementInput()
    {
        if (playerCharacter == null)
            return;

        Vector2 movement = movementAction != null
            ? movementAction.ReadValue<Vector2>()
            : Vector2.zero;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                movement.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                movement.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                movement.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                movement.x -= 1f;
        }

        if (movement.sqrMagnitude > 1f)
            movement.Normalize();

        playerCharacter.SetInputMovement(movement);
    }

    void SyncRunningState()
    {
        if (playerCharacter == null)
            return;

        Vector2 movement = playerCharacter.GetInputMovement();
        bool sprintHeld = runAction != null && runAction.IsPressed();
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.leftShiftKey.isPressed)
            sprintHeld = true;

        bool movingForward = movement.y > 0.5f && Mathf.Abs(movement.x) < 0.75f;
        bool canSprint = sprintHeld && movingForward && !holdingButtonAim && !reloading && !inspecting
                         && (playerCharacter == null || !playerCharacter.IsSwapping());
        playerCharacter.SetRunning(canSprint);
    }

    void SyncAimingState()
    {
        if (playerCharacter == null)
            return;

        bool canAim = !reloading && !inspecting && !playerCharacter.IsSwapping();
        playerCharacter.SetAiming(holdingButtonAim && canAim);
    }

    void ResolveWeapon()
    {
        weapon = playerCharacter != null ? playerCharacter.GetEquippedWeapon() : null;
        if (weapon == null)
            weapon = GetComponentInChildren<WeaponBehaviour>(true);
    }

    void EnsureWeaponAnimationBridge()
    {
        if (weapon == null)
            return;

        var bridge = weapon.GetComponent<ARInfimaWeaponAnimationBridge>();
        if (bridge == null)
            bridge = weapon.gameObject.AddComponent<ARInfimaWeaponAnimationBridge>();

        bridge.Init(playerCharacter, this);
    }

    public bool CanSwapWeapon() => !reloading && !inspecting && weapon != null;

    bool CanPlayAnimationFire() => !reloading && !inspecting && !IsSwapping();

    bool CanPlayAnimationReload() => !reloading && !inspecting && !IsSwapping();

    bool CanPlayAnimationInspect() => !reloading && !inspecting && !IsSwapping();

    bool IsSwapping() => playerCharacter != null && playerCharacter.IsSwapping();

    bool IsPlayerDead()
    {
        var vitals = GetComponent<ARZombieVitals>();
        return vitals != null && vitals.IsDead;
    }

    public void NotifyWeaponChanged()
    {
        ResolveWeapon();
        EnsureWeaponAnimationBridge();
        reloading = false;
        inspecting = false;
        if (reloadFallback != null)
        {
            StopCoroutine(reloadFallback);
            reloadFallback = null;
        }
    }

    void EnsureCharacterAnimator()
    {
        if (characterAnimator == null)
            characterAnimator = GetComponentInChildren<ARInfimaCharacterAnimator>(true);
    }

    void Fire()
    {
        lastShotTime = Time.time;
        weapon.Fire();
        ARMobileShotHaptics.PulseShot();
        EnsureCharacterAnimator();
        characterAnimator?.PlayFireAnimation();
    }

    void FireEmpty()
    {
        lastShotTime = Time.time;
        var clip = weapon.GetAudioClipFireEmpty();
        if (clip != null && ServiceLocator.Current != null)
            ServiceLocator.Current.Get<IAudioManagerService>()?.PlayOneShot(clip);

        EnsureCharacterAnimator();
        characterAnimator?.PlayFireEmptyAnimation();
        TryCrossFadeWeaponAnim("Fire Empty", 0.05f);
    }

    static void TryCrossFadeWeaponAnim(WeaponBehaviour weaponRef, string stateName, float duration)
    {
        var animator = weaponRef?.GetAnimator();
        if (animator == null)
            return;

        int hash = Animator.StringToHash(stateName);
        if (animator.HasState(0, hash))
            animator.CrossFade(stateName, duration, 0, 0f);
    }

    void TryCrossFadeWeaponAnim(string stateName, float duration)
    {
        TryCrossFadeWeaponAnim(weapon, stateName, duration);
    }

    public void RequestReload()
    {
        if (weapon == null)
            ResolveWeapon();

        TryReload();
    }

    void TryReload()
    {
        if (weapon == null || !CanPlayAnimationReload())
            return;

        PlayReloadAnimation();
    }

    void TryInspect()
    {
        if (weapon == null || !CanPlayAnimationInspect())
            return;

        PlayInspectAnimation();
    }

    void PlayReloadAnimation()
    {
        reloading = true;
        weapon.Reload();
        characterAnimator?.PlayReloadAnimation();
        if (reloadFallback != null)
            StopCoroutine(reloadFallback);
        reloadFallback = StartCoroutine(ReloadFallback());
    }

    IEnumerator ReloadFallback()
    {
        yield return new WaitForSeconds(2.2f);
        if (!reloading)
            yield break;

        playerCharacter?.FillAmmunition(0);
        reloading = false;
    }

    void PlayInspectAnimation()
    {
        inspecting = true;
        TryCrossFadeWeaponAnim("Inspect", 0f);
        characterAnimator?.PlayInspectAnimation();
    }

    public void OnReloadAnimationEnded()
    {
        reloading = false;
        if (reloadFallback == null)
            return;

        StopCoroutine(reloadFallback);
        reloadFallback = null;
    }

    public void OnInspectAnimationEnded() => inspecting = false;

    void OnFireStarted(InputAction.CallbackContext _)
    {
        if (IgnoreHardwareFire())
            return;

        holdingButtonFire = true;
    }

    void OnFireCanceled(InputAction.CallbackContext _)
    {
        if (DeviceUsesTouchCombat())
            return;

        holdingButtonFire = virtualFireHeld;
    }

    void OnFirePerformed(InputAction.CallbackContext _)
    {
        if (IgnoreHardwareFire())
            return;

        if (weapon == null || weapon.IsAutomatic())
            return;

        TryFireSemiAuto();
    }

    void TryFireSemiAuto()
    {
        if (weapon == null || !CanPlayAnimationFire())
            return;

        if (weapon.HasAmmunition())
        {
            if (Time.time - lastShotTime > 60f / weapon.GetRateOfFire())
                Fire();
        }
        else
            FireEmpty();
    }

    void OnReloadPerformed(InputAction.CallbackContext _) => TryReload();

    void OnInspectPerformed(InputAction.CallbackContext _) => TryInspect();

    void OnAimStarted(InputAction.CallbackContext _) => holdingButtonAim = true;

    void OnAimCanceled(InputAction.CallbackContext _) => holdingButtonAim = false;
}
