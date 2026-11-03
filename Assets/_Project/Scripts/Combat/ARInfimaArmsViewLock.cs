using UnityEngine;

/// <summary>
/// Keeps the Infima camera socket on the AR/gameplay camera every frame.
/// A one-shot align loses the gun as soon as the rig or the phone moves.
/// Arm motion relative to the socket (walk, aim, recoil) is preserved.
/// </summary>
[DefaultExecutionOrder(300)]
public class ARInfimaArmsViewLock : MonoBehaviour
{
    Transform view;
    Transform socket;

    public void Init(Transform viewTransform, Transform cameraSocket)
    {
        view = viewTransform;
        socket = cameraSocket;
        Align();
    }

    void OnEnable()
    {
        Application.onBeforeRender += Align;
    }

    void OnDisable()
    {
        Application.onBeforeRender -= Align;
    }

    void LateUpdate()
    {
        Align();
    }

    void Align()
    {
        if (view == null || socket == null)
            return;

        Quaternion rotationOffset = view.rotation * Quaternion.Inverse(socket.rotation);
        transform.rotation = rotationOffset * transform.rotation;

        Vector3 positionOffset = view.position - socket.position;
        transform.position += positionOffset;
    }
}
