using UnityEngine;

public class AnamorphicFollowStroke : MonoBehaviour
{
    [Header("Target")]
    public AnamorphicDrawingInstance drawing;
    [Min(0)] public int strokeIndex = 0;

    [Header("Identity")]
    [Tooltip("Stable ID for this follower (used by RevealDirector/VN). If empty, defaults to stroke name or Stroke_XX.")]
    public string followerKey = "";

    [Tooltip("Optional group label for batch reveal (e.g., Bees, Reeds, Birds, OuterRing). If empty, follower is 'ungrouped'.")]
    public string groupKey = "";

    [Header("Motion")]
    public float duration = 5f;
    public bool loop = true;

    [Tooltip("Optional curve to remap motion time (0..1 -> 0..1). Use for ease-in/out movement along the stroke.")]
    public AnimationCurve motionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Orientation")]
    public bool orientToTangent = true;
    public Vector3 localUp = Vector3.up;

    /// <summary>Normalized progress along the stroke (0..1). This is the raw time fraction (not curved).</summary>
    public float NormalizedT { get; private set; }

    /// <summary>Resolved follower key (never empty at runtime if drawing/stroke is valid).</summary>
    public string ResolvedFollowerKey { get; private set; } = "";

    /// <summary>Resolved group key (trimmed). Empty means ungrouped.</summary>
    public string ResolvedGroupKey { get; private set; } = "";

    private float _elapsed;

    private void Start()
    {
        _elapsed = 0f;
        if (drawing == null) drawing = GetComponentInParent<AnamorphicDrawingInstance>();
        ResolveIdentity();
    }

    private void OnEnable()
    {
        if (drawing == null) drawing = GetComponentInParent<AnamorphicDrawingInstance>();
        ResolveIdentity();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (drawing == null) drawing = GetComponentInParent<AnamorphicDrawingInstance>();
        ResolveIdentity();
    }
#endif

    private void Update()
    {
        if (drawing == null || drawing.asset == null) return;
        if (strokeIndex < 0 || strokeIndex >= drawing.asset.strokes.Count) return;

        var stroke = drawing.asset.strokes[strokeIndex];
        if (stroke.bakedPoints == null || stroke.bakedPoints.Count < 2) return;

        _elapsed += Time.deltaTime;
        if (duration <= 0.0001f) duration = 0.0001f;

        if (loop) _elapsed %= duration;
        else _elapsed = Mathf.Min(_elapsed, duration);

        float tRaw = Mathf.Clamp01(_elapsed / duration);
        NormalizedT = tRaw;

        // Motion remap (local authoring)
        float tMotion = tRaw;
        if (motionCurve != null)
            tMotion = Mathf.Clamp01(motionCurve.Evaluate(tRaw));

        // Local-space follow: object should be parented under drawing instance (recommended)
        Vector3 localPos = stroke.EvaluateLocal(tMotion);
        transform.localPosition = localPos;

        if (orientToTangent)
        {
            Vector3 tan = stroke.TangentLocal(tMotion);
            if (tan.sqrMagnitude > 1e-8f)
            {
                transform.localRotation = Quaternion.LookRotation(tan, localUp);
            }
        }

        // Keep identity stable (does not overwrite explicit fields, only resolves fallbacks)
        if (string.IsNullOrWhiteSpace(ResolvedFollowerKey) || ResolvedGroupKey == null)
            ResolveIdentity();
    }

    /// <summary>
    /// Resolve followerKey and groupKey into trimmed runtime values.
    /// followerKey priority: explicit followerKey -> stroke.name -> Stroke_XX
    /// groupKey: just trimmed; empty means ungrouped.
    /// </summary>
    private void ResolveIdentity()
    {
        // Resolve group first (simple)
        ResolvedGroupKey = string.IsNullOrWhiteSpace(groupKey) ? "" : groupKey.Trim();

        // Resolve follower key
        if (!string.IsNullOrWhiteSpace(followerKey))
        {
            ResolvedFollowerKey = followerKey.Trim();
            return;
        }

        if (drawing != null && drawing.asset != null &&
            strokeIndex >= 0 && strokeIndex < drawing.asset.strokes.Count)
        {
            string strokeName = drawing.asset.strokes[strokeIndex].name;
            if (!string.IsNullOrWhiteSpace(strokeName))
            {
                ResolvedFollowerKey = strokeName.Trim();
                return;
            }
        }

        ResolvedFollowerKey = $"Stroke_{strokeIndex:00}";
    }
}