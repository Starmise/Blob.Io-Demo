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

    public void SetZoomEnabled(bool v) => _zoomEnabled = v;
    public void SetRotationEnabled(bool v) => _canRotate = v; // Disable camera rotation when the player dies, so they can see the death screen without the camera moving around.

    private Transform _target;
    private bool _canRotate = true;
    private bool _zoomEnabled = true;

    public void Setup(Transform target, bool isLocal)
    {
        target = transform.parent;

        // Only activate the camera for the local player (other players should not activate their camera)
        _target = target;
        gameObject.SetActive(isLocal);
    }

    float _yawVelocity;
    float _currentYaw;
    float _targetYaw;
    Vector3 _smoothTargetPos;
    Vector3 _vel;
    void LateUpdate()
    {
        if (_target == null) return;

        if (_zoomEnabled)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            if (Mathf.Abs(scroll) > 0.001f)
            {
                distance -= scroll * 10f;
                distance = Mathf.Clamp(distance, 5f, 25f);
            }
        }

        float input = Input.GetAxis("Horizontal");
        _targetYaw += input * 120f * Time.deltaTime;

        if (Input.GetMouseButton(1))
        {
            _targetYaw += Input.GetAxis("Mouse X") * 200f * Time.deltaTime;
            yAngle -= Input.GetAxis("Mouse Y") * sensitivity;
            yAngle = Mathf.Clamp(yAngle, minY, maxY);
        }

        _currentYaw = Mathf.SmoothDampAngle(
            _currentYaw,
            _targetYaw,
            ref _yawVelocity,
            0.1f
        );

        _smoothTargetPos = Vector3.SmoothDamp(
            _smoothTargetPos,
            _target.position,
            ref _vel,
            0.08f
        );

        float tilt = -_yawVelocity * 0.05f;

        Quaternion rot = Quaternion.Euler(yAngle, _currentYaw, tilt);

        transform.position = _smoothTargetPos - rot * Vector3.forward * distance;
        transform.LookAt(_smoothTargetPos + Vector3.up * 0.5f);
    }
}