using UnityEngine;

/// <summary>
/// Simple bobbing + rotation for the crown when it is a child of a player.
/// The manager script is responsible for instantiating / re-parenting it.
/// </summary>
public class LeaderCrown : MonoBehaviour
{
    public float baseHeight = 2.0f;
    public float bobAmplitude = 0.25f;
    public float bobSpeed = 2.0f;
    public float rotationSpeed = 60f;

    void OnValidate()
    {
        // prevent values from being accidentally set to 0 or extremely small
        baseHeight = Mathf.Max(baseHeight, 0.1f);
        bobAmplitude = Mathf.Max(bobAmplitude, 0.01f);
        bobSpeed = Mathf.Max(bobSpeed, 0.01f);
    }

    private float _time;

    void Update()
    {
        _time += Time.deltaTime * bobSpeed;
        float bob = Mathf.Sin(_time) * bobAmplitude;

        // Keep X/Z from whatever was set, animate local Y.
        var localPos = transform.localPosition;
        localPos.y = baseHeight + bob;
        transform.localPosition = localPos;

        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
    }
}

