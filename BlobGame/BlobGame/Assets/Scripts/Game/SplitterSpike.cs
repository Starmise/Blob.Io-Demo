using UnityEngine;

/// <summary>
/// Arena hazard: when the <b>local</b> player touches it, requests a server split (same as pressing E).
/// The spike is consumed only if the split request is allowed (not already split, enough mass).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public class SplitterSpike : MonoBehaviour
{
    [Header("Idle pulse (uniform — reads as a sphere)")]
    [Tooltip("Breaths per second, similar to the player blob but a bit calmer.")]
    [SerializeField] float breathHz = 2f;
    [SerializeField] float breathAmount = 0.055f;
    [Tooltip("Optional extra wobble on the same uniform scale (keep small).")]
    [SerializeField] float subtleWobbleHz = 4.2f;
    [SerializeField] float subtleWobbleAmount = 0.018f;

    Vector3 _baseScale = Vector3.one;
    SphereCollider _sphere;

    void Awake()
    {
        _sphere = GetComponent<SphereCollider>();
        if (_sphere != null)
            _sphere.isTrigger = true;

        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (_baseScale.sqrMagnitude < 1e-6f)
            _baseScale = transform.localScale;
    }

    void Update()
    {
        float t = Time.time;
        float omega = Mathf.PI * 2f;
        float main = Mathf.Sin(t * omega * breathHz) * breathAmount;
        float wobble = Mathf.Sin(t * omega * subtleWobbleHz) * subtleWobbleAmount;
        float factor = 1f + main + wobble;
        transform.localScale = _baseScale * factor;
    }

    /// <summary>Called by <see cref="GameManager"/> for arena spawns.</summary>
    public void InitializeForArena(Vector3 uniformScale)
    {
        _baseScale = uniformScale;
        transform.localScale = uniformScale;
    }

    void OnTriggerEnter(Collider other)
    {
        var pc = other.GetComponentInParent<PlayerController>();
        if (pc == null || !pc._isLocal) return;

        Vector3 launchDir = WorldXZAwayFromSpike(pc.transform.position);
        if (!pc.TryRequestSplitFromSpike(launchDir))
            return;

        AudioManager.Instance?.PlaySplitterSpikeTouch();
        Destroy(gameObject);
    }

    Vector3 WorldXZAwayFromSpike(Vector3 playerWorldPos)
    {
        Vector3 d = playerWorldPos - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }
        return d.normalized;
    }
}
