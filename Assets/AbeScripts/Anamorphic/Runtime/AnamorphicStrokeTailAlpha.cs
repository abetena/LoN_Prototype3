using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class AnamorphicStrokeTailAlpha : MonoBehaviour
{
    [Header("Target")]
    public AnamorphicDrawingInstance drawing;
    [Min(0)] public int strokeIndex = 0;

    [Header("Tail Style (Authoring)")]
    [Range(0.0001f, 1f)]
    public float tailWindow = 0.10f;

    [Range(0.000001f, 0.02f)]
    public float keyEpsilon = 0.0001f;

    [Header("Editor Preview (Optional)")]
    [Tooltip("If true, TailAlpha will preview its gradient edits in Edit Mode. Off keeps authored gradients untouched.")]
    public bool previewInEditor = false;

    // Cached
    private LineRenderer _lr;
    private Gradient _runtimeGradient;

    // Preserve colors, drive alpha only
    private GradientColorKey[] _preservedColorKeys;
    private GradientAlphaKey[] _alphaKeys; // 3 keys

    private float _lastProgress = -999f;
    private float _lastTailAlpha = -999f;

    private bool _capturedAtRuntime = false;

#if UNITY_EDITOR
    private bool _wireQueued = false;
#endif

    private void OnEnable()
    {
        if (drawing == null) drawing = GetComponentInParent<AnamorphicDrawingInstance>();

        TryWireLineRenderer(immediateIfPossible: true);

        // In Edit Mode: DO NOT overwrite authored gradients.
        // In Play Mode: capture once and drive alpha.
        if (Application.isPlaying)
        {
            CaptureColorKeysFromLineRenderer();   // capture authored colors
            EnsureRuntimeGradient();
            _capturedAtRuntime = true;

            // Initialize to current state (usually 0)
            ApplyGradientIfReady(force: true);
        }
        else
        {
            // Edit mode: just cache colors if empty (non-destructive)
            if (_preservedColorKeys == null || _preservedColorKeys.Length == 0)
                CaptureColorKeysFromLineRenderer();

            if (previewInEditor)
            {
                EnsureRuntimeGradient();
                ApplyGradientIfReady(force: true);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (drawing == null) drawing = GetComponentInParent<AnamorphicDrawingInstance>();
        QueueEditorWireAndApply();
    }
#endif

    private void Update()
    {
        // Late wiring in edit mode
        if (!Application.isPlaying && _lr == null)
        {
            TryWireLineRenderer(immediateIfPossible: false);

            // Still non-destructive: only capture if we have nothing.
            if ((_preservedColorKeys == null || _preservedColorKeys.Length == 0) && _lr != null)
                CaptureColorKeysFromLineRenderer();

            if (previewInEditor && _lr != null)
            {
                EnsureRuntimeGradient();
                ApplyGradientIfReady(force: false);
            }
            return;
        }

        // Play mode safety: capture after enable-order weirdness
        if (Application.isPlaying && !_capturedAtRuntime)
        {
            TryWireLineRenderer(immediateIfPossible: true);
            if (_lr != null)
            {
                CaptureColorKeysFromLineRenderer();
                EnsureRuntimeGradient();
                _capturedAtRuntime = true;
                ApplyGradientIfReady(force: true);
            }
        }
    }

    // ----------------------------
    // API used by Driver
    // ----------------------------

    public void SetProgress(float normalizedT)
    {
        normalizedT = Mathf.Clamp01(normalizedT);
        if (Mathf.Abs(normalizedT - _lastProgress) < 0.0001f) return;

        _lastProgress = normalizedT;

        if (Application.isPlaying || previewInEditor)
            ApplyGradientIfReady();
    }

    public void SetTailAlpha(float tailAlpha)
    {
        tailAlpha = Mathf.Clamp01(tailAlpha);
        if (Mathf.Abs(tailAlpha - _lastTailAlpha) < 0.0001f) return;

        _lastTailAlpha = tailAlpha;

        if (Application.isPlaying || previewInEditor)
            ApplyGradientIfReady();
    }

    /// <summary>
    /// Manual helper: if you change the LineRenderer gradient in inspector
    /// and want TailAlpha to re-capture those color keys, call this.
    /// </summary>
    [ContextMenu("Sync Color Keys From LineRenderer")]
    public void SyncColorKeysFromLineRenderer()
    {
        if (_lr == null) TryWireLineRenderer(immediateIfPossible: true);
        if (_lr == null) return;

        CaptureColorKeysFromLineRenderer();

        if (Application.isPlaying || previewInEditor)
        {
            EnsureRuntimeGradient();
            ApplyGradientIfReady(force: true);
        }
    }

    // ----------------------------
    // Wiring + Gradient
    // ----------------------------

    private void TryWireLineRenderer(bool immediateIfPossible)
    {
        if (_lr != null) return;
        if (drawing == null) return;

        _lr = drawing.GetStrokeRenderer(strokeIndex);

        if (_lr == null && immediateIfPossible && Application.isPlaying)
            _lr = drawing.GetStrokeRenderer(strokeIndex);
    }

    private void EnsureRuntimeGradient()
    {
        if (_runtimeGradient == null) _runtimeGradient = new Gradient();

        if (_alphaKeys == null || _alphaKeys.Length != 3)
        {
            _alphaKeys = new GradientAlphaKey[3];
            _alphaKeys[0] = new GradientAlphaKey(0f, 0f);
            _alphaKeys[1] = new GradientAlphaKey(0f, 0f);
            _alphaKeys[2] = new GradientAlphaKey(0f, 0f);
        }
    }

    private void CaptureColorKeysFromLineRenderer()
    {
        if (_lr == null) return;

        Gradient lrGrad = _lr.colorGradient;

        // Capture authored gradient colors (what you set in inspector)
        if (lrGrad != null && lrGrad.colorKeys != null && lrGrad.colorKeys.Length > 0)
        {
            _preservedColorKeys = lrGrad.colorKeys;
            return;
        }

        // Fallback: start/end colors
        _preservedColorKeys = new GradientColorKey[2];
        _preservedColorKeys[0] = new GradientColorKey(_lr.startColor, 0f);
        _preservedColorKeys[1] = new GradientColorKey(_lr.endColor, 1f);
    }

    private void ApplyGradientIfReady(bool force = false)
    {
        if (_lr == null) return;
        if (_preservedColorKeys == null || _preservedColorKeys.Length == 0) return;
        EnsureRuntimeGradient();

        float progress = (_lastProgress < -100f) ? 0f : Mathf.Clamp01(_lastProgress);
        float tailAlpha = (_lastTailAlpha < -100f) ? 0f : Mathf.Clamp01(_lastTailAlpha);

        float w = Mathf.Clamp(tailWindow, 0.0001f, 1f);
        float eps = Mathf.Clamp(keyEpsilon, 0.000001f, 0.02f);

        float t0 = Mathf.Clamp01(progress - w);
        float t1 = Mathf.Clamp01(progress);
        float t2 = Mathf.Clamp01(progress + eps);

        _alphaKeys[0].time = t0;
        _alphaKeys[1].time = t1;
        _alphaKeys[2].time = t2;

        _alphaKeys[0].alpha = 0f;
        _alphaKeys[1].alpha = tailAlpha;
        _alphaKeys[2].alpha = 0f;

        _runtimeGradient.SetKeys(_preservedColorKeys, _alphaKeys);

        // ✅ Only write into LineRenderer when playing OR explicitly previewing in editor
        if (Application.isPlaying || previewInEditor)
            _lr.colorGradient = _runtimeGradient;
    }

#if UNITY_EDITOR
    private void QueueEditorWireAndApply()
    {
        if (_wireQueued) return;
        _wireQueued = true;

        EditorApplication.delayCall += () =>
        {
            _wireQueued = false;
            if (this == null) return;

            if (drawing == null) drawing = GetComponentInParent<AnamorphicDrawingInstance>();

            // Non-destructive wiring + capture
            _lr = null;
            TryWireLineRenderer(immediateIfPossible: false);

            if (_lr != null && (_preservedColorKeys == null || _preservedColorKeys.Length == 0))
                CaptureColorKeysFromLineRenderer();

            if (previewInEditor && _lr != null)
            {
                EnsureRuntimeGradient();
                ApplyGradientIfReady(force: true);
            }
        };
    }
#endif
}