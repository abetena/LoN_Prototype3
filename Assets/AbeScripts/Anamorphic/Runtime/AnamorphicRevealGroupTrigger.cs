using UnityEngine;

public class AnamorphicRevealGroupTrigger : MonoBehaviour
{
    [Header("Target Group")]
    public string drawingKey;
    public string groupKey;
    public string instanceTag = ""; // optional

    [Header("Reveal Behavior")]
    [Range(0f, 1f)]
    public float targetReveal = 1f;

    public float rampSeconds = 1.5f;

    public bool oneShot = true;

    [Header("Trigger Filter")]
    public string requiredTag = "Player";

    private bool _hasFired = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_hasFired && oneShot) return;
        if (!string.IsNullOrWhiteSpace(requiredTag) && !other.CompareTag(requiredTag)) return;

        if (AnamorphicRevealDirector.Instance == null)
        {
            Debug.LogWarning("AnamorphicRevealDirector not found in scene.");
            return;
        }

        // Reveal the whole group
        AnamorphicRevealDirector.Instance.RampRevealGroup(
            drawingKey,
            groupKey,
            targetReveal,
            rampSeconds,
            instanceTag
        );

        _hasFired = true;
    }
}