using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;

/// <summary>
/// Mouse look for play when no tracker is driving the camera.
/// XR Simulation already looks with the mouse, so this only locks the cursor there.
/// A tracked phone keeps the AR pose and this stays out of the way.
/// </summary>
[DefaultExecutionOrder(120)]
public class ARInfimaDesktopLook : MonoBehaviour
{
    [SerializeField] float sensitivity = 0.08f;
    [SerializeField] float pitchMin = -80f;
    [SerializeField] float pitchMax = 80f;
    [SerializeField] float eyeHeight = 1.6f;

    Camera viewCamera;
    Transform cameraOffset;
    TrackedPoseDriver poseDriver;
    float pitch;
    bool raisedEyeHeight;
    bool invertLook;

    void Awake()
    {
        var player = GetComponent<ARInfimaPlayerCharacter>();
        viewCamera = player != null ? player.GetCameraWorld() : Camera.main;
        if (viewCamera == null)
            return;

        poseDriver = viewCamera.GetComponent<TrackedPoseDriver>();
        cameraOffset = viewCamera.transform.parent;
        pitch = viewCamera.transform.localEulerAngles.x;
        if (pitch > 180f)
            pitch -= 360f;
    }

    void Start()
    {
        sensitivity *= GameSettings.LookScale;
        invertLook = GameSettings.InvertLook;

        if (!RealDeviceTracking())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        if (viewCamera == null)
            return;

        var vitals = GetComponent<ARZombieVitals>();
        if (vitals != null && vitals.IsDead)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (RealDeviceTracking())
        {
            if (poseDriver != null && !poseDriver.enabled)
                poseDriver.enabled = true;

            if (raisedEyeHeight && cameraOffset != null)
            {
                Vector3 local = cameraOffset.localPosition;
                local.y = 0f;
                cameraOffset.localPosition = local;
                raisedEyeHeight = false;
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            return;
        }

        if (SimulationOwnsView())
        {
            if (poseDriver != null && !poseDriver.enabled)
                poseDriver.enabled = true;

            HoldCursorForLook();
            return;
        }

        if (poseDriver != null)
            poseDriver.enabled = false;

        RaiseEyes();
        ApplyMouseLook();
    }

    static bool RealDeviceTracking()
    {
        return ARSession.state == ARSessionState.SessionTracking && !SimulationOwnsView();
    }

    static bool SimulationOwnsView()
    {
#if UNITY_EDITOR
        var settings = XRGeneralSettings.Instance;
        var loader = settings != null && settings.Manager != null
            ? settings.Manager.activeLoader
            : null;
        return loader is UnityEngine.XR.Simulation.SimulationLoader;
#else
        return false;
#endif
    }

    void RaiseEyes()
    {
        if (raisedEyeHeight || cameraOffset == null)
            return;

        if (cameraOffset.localPosition.y > 0.5f)
            return;

        Vector3 local = cameraOffset.localPosition;
        local.y = eyeHeight;
        cameraOffset.localPosition = local;
        raisedEyeHeight = true;
    }

    void HoldCursorForLook()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            bool lockCursor = Cursor.lockState != CursorLockMode.Locked;
            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockCursor;
            return;
        }

        if (Cursor.lockState == CursorLockMode.Locked)
            return;

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void ApplyMouseLook()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            bool lockCursor = Cursor.lockState != CursorLockMode.Locked;
            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockCursor;
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            var mouseClick = Mouse.current;
            if (mouseClick != null && mouseClick.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null)
            return;

        Vector2 delta = mouse.delta.ReadValue();
        Transform rig = viewCamera.transform.root;
        rig.Rotate(0f, delta.x * sensitivity, 0f, Space.World);

        float vertical = invertLook ? delta.y : -delta.y;
        pitch = Mathf.Clamp(pitch + vertical * sensitivity, pitchMin, pitchMax);
        Vector3 euler = viewCamera.transform.localEulerAngles;
        euler.x = pitch;
        euler.z = 0f;
        viewCamera.transform.localEulerAngles = euler;
    }

    void OnDisable()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (poseDriver != null)
            poseDriver.enabled = true;
    }
}
