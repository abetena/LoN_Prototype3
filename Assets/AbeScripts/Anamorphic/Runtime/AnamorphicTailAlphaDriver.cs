using UnityEngine;

/// <summary>
/// Drives an AnamorphicStrokeTailAlpha module using:
/// - progress from AnamorphicFollowStroke (NormalizedT)
/// - reveal amount from Local slider, Global RevealDirector, or an Override value
/// plus LOCAL styling controls (reveal curve/multiplier/offset) for in-situ look.
/// </summary>
[RequireComponent(typeof(AnamorphicFollowStroke))]
public class AnamorphicTailAlphaDriver : MonoBehaviour
{
    public enum RevealSource
    {
        Local,      // use inspector slider
        Global,     // read from AnamorphicRevealDirector
        Override    // force override value
    }

    [Header("Target Tail Module")]
    [Tooltip("The Tail Alpha module that controls the stroke's LineRenderer gradient.")]
    public AnamorphicStrokeTailAlpha tail;

    [Header("Reveal Source")]
    public RevealSource revealSource = RevealSource.Local;

    [Tooltip("Used when Reveal Source is Local.")]
    [Range(0f, 1f)]
    public float localRevealAmount = 0f;

    [Tooltip("Used when Reveal Source is Override.")]
    [Range(0f, 1f)]
    public float overrideRevealAmount = 0f;

    [Tooltip("If true, the driver will try to auto-find drawing/tail when missing (best-effort).")]
    public bool autoWireIfMissing = true;

    [Header("Local Reveal Styling (does NOT affect global state)")]
    [Tooltip("Optional curve to remap reveal (0..1) for local pacing (ease in/out). Default is linear.")]
    public AnimationCurve revealCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Tooltip("Multiply reveal by this factor before curve (useful for making some followers reveal 'stronger').")]
    public float revealMultiplier = 1f;

    [Tooltip("Add this offset to reveal before curve (useful for baseline shimmer).")]
    public float revealOffset = 0f;

    private AnamorphicFollowStroke _follow;

    private void Awake()
    {
        _follow = GetComponent<AnamorphicFollowStroke>();
    }

    private void OnEnable()
    {
        if (_follow == null) _follow = GetComponent<AnamorphicFollowStroke>();

        if (autoWireIfMissing)
            TryAutoWire();

        // Register with reveal director (if it exists)
        AnamorphicRevealDirector.TryRegisterFollower(this);
    }

    private void OnDisable()
    {
        AnamorphicRevealDirector.TryUnregisterFollower(this);
    }

    private void Update()
    {
        if (tail == null || _follow == null) return;

        // Always push progress to tail module
        tail.SetProgress(_follow.NormalizedT);

        // Determine reveal amount (raw 0..1 from source)
        float rawReveal = GetRevealAmountRaw();

        // Apply local styling (still returns 0..1)
        float styledReveal = ApplyLocalStyling(rawReveal);

        tail.SetTailAlpha(styledReveal);
    }

    private float GetRevealAmountRaw()
    {
        switch (revealSource)
        {
            case RevealSource.Local:
                return Mathf.Clamp01(localRevealAmount);

            case RevealSource.Override:
                return Mathf.Clamp01(overrideRevealAmount);

            case RevealSource.Global:
            default:
                // If director isn't present yet, fall back to Local to avoid "nothing happens"
                float global = AnamorphicRevealDirector.GetRevealOrFallback(this, localRevealAmount);
                return Mathf.Clamp01(global);
        }
    }

    private float ApplyLocalStyling(float rawReveal)
    {
        float v = rawReveal;

        v = (v * revealMultiplier) + revealOffset;
        v = Mathf.Clamp01(v);

        if (revealCurve != null)
            v = Mathf.Clamp01(revealCurve.Evaluate(v));

        return v;
    }

    /// <summary>
    /// Convenience for gameplay/VN to force an override without changing wiring.
    /// </summary>
    public void SetOverride(float amount)
    {
        overrideRevealAmount = Mathf.Clamp01(amount);
        revealSource = RevealSource.Override;
    }

    /// <summary>
    /// Convenience to return to Global behavior.
    /// </summary>
    public void UseGlobal()
    {
        revealSource = RevealSource.Global;
    }

    /// <summary>
    /// Convenience to return to Local (debug) behavior.
    /// </summary>
    public void UseLocal()
    {
        revealSource = RevealSource.Local;
    }

    /// <summary>
    /// Exposed identity info for RevealDirector.
    /// </summary>
    public bool TryGetIdentity(out string drawingKey, out string followerKey, out string instanceTag)
    {
        drawingKey = "";
        followerKey = "";
        instanceTag = "";

        if (_follow == null) _follow = GetComponent<AnamorphicFollowStroke>();
        if (_follow == null) return false;

        if (_follow.drawing == null || _follow.drawing.asset == null) return false;

        drawingKey = _follow.drawing.asset.DrawingKey;
        followerKey = _follow.ResolvedFollowerKey;

        // Optional instance tag on drawing instance
        instanceTag = _follow.drawing.instanceTag;

        return !string.IsNullOrWhiteSpace(drawingKey) && !string.IsNullOrWhiteSpace(followerKey);
    }

    private void TryAutoWire()
    {
        if (_follow == null) return;

        // If tail is missing but we have drawing + stroke index, try to find a matching tail module on the drawing root
        if (tail == null && _follow.drawing != null)
        {
            var tails = _follow.drawing.GetComponents<AnamorphicStrokeTailAlpha>();
            for (int i = 0; i < tails.Length; i++)
            {
                if (tails[i] != null && tails[i].strokeIndex == _follow.strokeIndex)
                {
                    tail = tails[i];
                    break;
                }
            }
        }
    }
}