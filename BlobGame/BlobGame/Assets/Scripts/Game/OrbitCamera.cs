using UnityEngine;
using static UnityEngine.GraphicsBuffer;

/// <summary>
/// A simple orbit camera that rotates around a target object based on mouse input. The camera can
/// be enabled or disabled for rotation, which is useful for showing the death screen when the player dies without the camera moving around.
/// </summary>
public class OrbitCamera : MonoBehaviour
{
    public float distance = 12f;
    public float yAngle = 45f;
    public float xAngle = 0f;
    public float sensitivity = 3f;
    public float minY = 10f;
    public float maxY = 80f;

    private Transform _target;
    private bool _canRotate = true;
    public void SetRotationEnabled(bool v) => _canRotate = v; // Disable camera rotation when the player dies, so they can see the death screen without the camera moving around.

    public void Setup(Transform target, bool isLocal)
    {
        target = transform.parent;

        // Only activate the camera for the local player (other players should not activate their camera)
        _target = target;
        gameObject.SetActive(isLocal);
    }

    void LateUpdate()
    {
        if (_target == null) return;

        // Rotate the camera around the target based on mouse input when the right mouse button is held down.
        if (_canRotate && Input.GetMouseButton(1))
        {
            xAngle += Input.GetAxis("Mouse X") * sensitivity;
            yAngle -= Input.GetAxis("Mouse Y") * sensitivity;
            yAngle = Mathf.Clamp(yAngle, minY, maxY);
        }

        // Calculate the new position and rotation of the camera based on the angles and distance from the target.
        Quaternion rot = Quaternion.Euler(yAngle, xAngle, 0);
        transform.position = _target.position - rot * Vector3.forward * distance;
        transform.LookAt(_target.position + Vector3.up * 0.5f);
    }
}