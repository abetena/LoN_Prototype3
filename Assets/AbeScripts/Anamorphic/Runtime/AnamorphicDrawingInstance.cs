using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class AnamorphicDrawingInstance : MonoBehaviour
{
    [Header("Asset")]
    public AnamorphicDrawingAsset asset;

    [Header("Identity (Optional)")]
    [Tooltip("Used to distinguish multiple instances of the same Drawing Key in a scene.")]
    public string instanceTag = "";

    [Header("Rendering")]
    [Tooltip("Optional. If Override Stroke Material is ON, all strokes use this material.")]
    public Material lineMaterial;

    [Tooltip("If true, this instance forces all stroke LineRenderers to use 'Line Material'. " +
             "If false (recommended), each stroke keeps its own authored material + gradient.")]
    public bool overrideStrokeMaterial = false;

    [Min(0.0001f)] public float lineWidth = 0.03f;

    [Tooltip("If true, automatically rebuild LineRenderers in Edit Mode when asset changes.")]
    public bool autoRefreshInEditor = true;

    private readonly List<LineRenderer> _renderers = new List<LineRenderer>();

#if UNITY_EDITOR
    private bool _refreshQueued = false;
#endif

    private bool _runtimeMissingWarned = false;

    public Vector3 GetViewpointWorldPosition()
    {
        if (asset == null) return transform.position;
        return transform.TransformPoint(asset.viewpointLocalPosition);
    }

    public Quaternion GetViewpointWorldRotation()
    {
        if (asset == null) return transform.rotation;
        return transform.rotation * asset.viewpointLocalRotation;
    }

    public Vector3 GetLookAtWorldPosition()
    {
        if (asset == null) return transform.position;
        return transform.TransformPoint(asset.lookAtLocalPosition);
    }

    public LineRenderer GetStrokeRenderer(int strokeIndex)
    {
        if (asset == null) return null;
        if (strokeIndex < 0 || strokeIndex >= asset.strokes.Count) return null;

        if (Application.isPlaying)
        {
            RebuildRendererCacheFromChildren();
            PruneDestroyedRenderers();

            if (strokeIndex >= 0 && strokeIndex < _renderers.Count)
                return _renderers[strokeIndex];

            if (!_runtimeMissingWarned)
            {
                _runtimeMissingWarned = true;
                Debug.LogWarning($"[AnamorphicDrawingInstance] Missing runtime stroke renderers on '{name}'. " +
                                 $"Ensure the Stroke_XX LineRenderers exist as children before Play Mode.", this);
            }
            return null;
        }

        // Edit mode
        RebuildRendererCacheFromChildren();
        PruneDestroyedRenderers();

        if (strokeIndex >= 0 && strokeIndex < _renderers.Count)
            return _renderers[strokeIndex];

#if UNITY_EDITOR
        if (autoRefreshInEditor) QueueEditorRefresh();
#endif
        return null;
    }

    public void RefreshRenderers()
    {
        if (asset == null) return;

        PruneDestroyedRenderers();
        RebuildRendererCacheFromChildren();

        // EDITOR ONLY: ensure correct count by creating/removing children.
        if (!Application.isPlaying)
        {
            EnsureRendererCount(asset.strokes.Count);
        }
        else
        {
            // Runtime: never create/destroy. Just use what exists.
            if (_renderers.Count < asset.strokes.Count && !_runtimeMissingWarned)
            {
                _runtimeMissingWarned = true;
                Debug.LogWarning($"[AnamorphicDrawingInstance] Runtime renderer count ({_renderers.Count}) < stroke count ({asset.strokes.Count}) on '{name}'. " +
                                 $"Create stroke renderers in editor. (Avoid runtime creation to preserve authored gradients.)", this);
            }
        }

        int count = Mathf.Min(_renderers.Count, asset.strokes.Count);

        for (int i = 0; i < count; i++)
        {
            var stroke = asset.strokes[i];
            var lr = _renderers[i];
            if (lr == null) continue;

            lr.useWorldSpace = false;
            lr.loop = stroke.closed;
            lr.widthMultiplier = lineWidth;

            // ✅ Only override material if explicitly requested.
            if (overrideStrokeMaterial && lineMaterial != null)
                lr.sharedMaterial = lineMaterial;

            if (stroke.bakedPoints == null || stroke.bakedPoints.Count == 0)
            {
                lr.positionCount = 0;
                continue;
            }

            lr.positionCount = stroke.bakedPoints.Count;
            lr.SetPositions(stroke.bakedPoints.ToArray());
        }
    }

    private void EnsureRendererCount(int needed)
    {
        PruneDestroyedRenderers();

        while (_renderers.Count < needed)
        {
            var go = new GameObject($"Stroke_{_renderers.Count:00}");
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            // New strokes should not force a material unless you opted into override.
            if (overrideStrokeMaterial && lineMaterial != null)
                lr.sharedMaterial = lineMaterial;

            _renderers.Add(lr);
        }

        for (int i = _renderers.Count - 1; i >= needed; i--)
        {
            var lr = _renderers[i];
            if (lr != null)
            {
                if (Application.isPlaying) Destroy(lr.gameObject);
                else DestroyImmediate(lr.gameObject);
            }
            _renderers.RemoveAt(i);
        }
    }

    private void PruneDestroyedRenderers()
    {
        for (int i = _renderers.Count - 1; i >= 0; i--)
        {
            if (_renderers[i] == null)
                _renderers.RemoveAt(i);
        }
    }

    private void RebuildRendererCacheFromChildren()
    {
        _renderers.Clear();
        GetComponentsInChildren(true, _renderers);

        _renderers.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            return string.CompareOrdinal(a.gameObject.name, b.gameObject.name);
        });
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying || !autoRefreshInEditor) return;
        QueueEditorRefresh();
    }

    private void QueueEditorRefresh()
    {
        if (_refreshQueued) return;
        _refreshQueued = true;

        EditorApplication.delayCall += () =>
        {
            _refreshQueued = false;
            if (this == null) return;
            if (Application.isPlaying) return;
            RefreshRenderers();
        };
    }

    private void OnDrawGizmosSelected()
    {
        if (asset == null) return;

        Vector3 vp = GetViewpointWorldPosition();
        Vector3 la = GetLookAtWorldPosition();

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(vp, 0.08f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(la, 0.07f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(vp, la);
    }
#endif
}