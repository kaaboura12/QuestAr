using InfimaGames.LowPolyShooterPack;
using UnityEngine;
using UnityEngine.XR.Management;

/// <summary>
/// Moves the XR rig from WASD on the ground plane. Aiming slows the walk; it never blocks it.
/// XR Simulation already flies the camera from WASD, so this motor stays out of the way there
/// and only reports speed so the arms still play a walk cycle.
/// </summary>
[DefaultExecutionOrder(140)]
public class ARInfimaPlayerMotor : MonoBehaviour
{
    [SerializeField] float walkSpeed = 4.2f;
    [SerializeField] float aimSpeed = 2.1f;
    [SerializeField] float sprintSpeed = 7f;
    [SerializeField] float bodyRadius = 0.28f;

    ARInfimaPlayerCharacter playerCharacter;
    Transform rig;
    Camera viewCamera;
    Vector3 lastCameraPlanar;
    bool hasLastCamera;

    void Awake()
    {
        playerCharacter = GetComponent<ARInfimaPlayerCharacter>();
        viewCamera = playerCharacter != null ? playerCharacter.GetCameraWorld() : Camera.main;
        rig = viewCamera != null ? viewCamera.transform.root : transform.root;
    }

    void Update()
    {
        if (playerCharacter == null || rig == null)
            return;

        var vitals = GetComponent<ARZombieVitals>();
        if (vitals != null && vitals.IsDead)
        {
            playerCharacter.SetPlanarSpeed(0f);
            return;
        }

        if (viewCamera == null)
            viewCamera = playerCharacter.GetCameraWorld();

        ReportPlanarSpeed();

        if (!SimulationOwnsMovement())
            MoveRig();
    }

    /// <summary>
    /// Editor XR Simulation moves the tracked camera itself. Moving the rig as well doubles WASD.
    /// </summary>
    static bool SimulationOwnsMovement()
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

    void ReportPlanarSpeed()
    {
        if (viewCamera == null)
            return;

        Vector3 planar = viewCamera.transform.position;
        planar.y = 0f;

        if (!hasLastCamera)
        {
            lastCameraPlanar = planar;
            hasLastCamera = true;
            return;
        }

        float speed = Time.deltaTime > 0f
            ? (planar - lastCameraPlanar).magnitude / Time.deltaTime
            : 0f;
        lastCameraPlanar = planar;
        playerCharacter.SetPlanarSpeed(speed);
    }

    void MoveRig()
    {
        Vector2 input = playerCharacter.GetInputMovement();
        if (input.sqrMagnitude < 0.0001f || viewCamera == null)
            return;

        float speed = walkSpeed;
        if (playerCharacter.IsAiming())
            speed = aimSpeed;
        else if (playerCharacter.IsRunning())
            speed = sprintSpeed;

        Transform view = viewCamera.transform;
        Vector3 forward = view.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = rig.forward;
        forward.Normalize();

        Vector3 right = view.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.0001f)
            right = rig.right;
        right.Normalize();

        Vector3 motion = (forward * input.y + right * input.x) * (speed * Time.deltaTime);
        motion = Slide(motion);
        rig.position += motion;
    }

    Vector3 Slide(Vector3 motion)
    {
        if (motion.sqrMagnitude < 0.000001f)
            return motion;

        int mask = Physics.DefaultRaycastLayers;
        int fpv = LayerMask.NameToLayer("First Person View");
        int projectile = LayerMask.NameToLayer("Projectile");
        if (fpv >= 0)
            mask &= ~(1 << fpv);
        if (projectile >= 0)
            mask &= ~(1 << projectile);

        Vector3 origin = rig.position + Vector3.up * 0.9f;
        float distance = motion.magnitude;
        if (!Physics.SphereCast(
                origin,
                bodyRadius,
                motion / distance,
                out RaycastHit hit,
                distance + 0.02f,
                mask,
                QueryTriggerInteraction.Ignore))
            return motion;

        Vector3 slide = Vector3.ProjectOnPlane(motion, hit.normal);
        slide.y = 0f;
        return slide;
    }
}
