using UnityEngine;

public class AnamorphicRevealTrigger : MonoBehaviour
{
    [Header("Target Identity")]
    public string drawingKey;
    public string followerKey;
    public string instanceTag = ""; // optional

    [Header("Reveal Behavior")]
    [Range(0f, 1f)]
    public float targetReveal = 1f;

    public float rampSeconds = 1.5f;

    public bool oneShot = true;

    private bool _hasFired = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_hasFired && oneShot) return;

        if (!other.CompareTag("Player")) return;

        if (AnamorphicRevealDirector.Instance == null)
        {
            Debug.LogWarning("RevealDirector not found in scene.");
            return;
        }

        AnamorphicRevealDirector.Instance.RampReveal(
            drawingKey,
            followerKey,
            targetReveal,
            rampSeconds,
            instanceTag
        );

        _hasFired = true;
    }
}
