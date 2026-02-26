using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Nahuales/Anamorphic Drawing Asset", fileName = "NewAnamorphicDrawing")]
public class AnamorphicDrawingAsset : ScriptableObject
{
    // ------------------------------------------------------------
    // Identity
    // ------------------------------------------------------------
    [Header("Identity")]
    [Tooltip("Stable ID used by gameplay/VN systems. If empty, defaults to the asset name.")]
    public string drawingKey = "";

    /// <summary>
    /// Stable key for addressing this drawing from gameplay/VN.
    /// Falls back to the asset name if drawingKey is not set.
    /// </summary>
    public string DrawingKey => string.IsNullOrWhiteSpace(drawingKey) ? name : drawingKey;

    // ------------------------------------------------------------
    // View Contract
    // ------------------------------------------------------------
    [Header("View Contract (Local to Drawing Root)")]
    public Vector3 viewpointLocalPosition = new Vector3(0, 0, -5);
    public Quaternion viewpointLocalRotation = Quaternion.identity; // roll can live here; yaw/pitch can be derived from lookAt
    public Vector3 lookAtLocalPosition = Vector3.zero;
    [Range(1f, 179f)] public float recommendedFOV = 60f;

    // ------------------------------------------------------------
    // Bake Settings
    // ------------------------------------------------------------
    [Header("Bake Settings")]
    [Min(0.001f)] public float maxSegmentLength = 0.1f;     // adaptive sampling target spacing
    [Min(0.00001f)] public float maxBezierError = 0.01f;     // flatness tolerance for Bezier subdivision
    [Min(0f)] public float simplifyTolerance = 0.005f;       // RDP tolerance
    [Min(16)] public int maxPointsPerStroke = 4000;
    [Min(16)] public int maxTotalPoints = 20000;

    // ------------------------------------------------------------
    // Deterministic Z Distortion
    // ------------------------------------------------------------
    [Header("Deterministic Z Distortion (Noise by Arc-Length)")]
    public int seed = 12345;
    public float zAmplitude = 0.25f;
    public float zFrequency = 0.5f;

    // ------------------------------------------------------------
    // Data
    // ------------------------------------------------------------
    [Header("Strokes")]
    public List<Stroke> strokes = new List<Stroke>();

    [Header("Attachment Points")]
    public List<AttachmentPoint> attachmentPoints = new List<AttachmentPoint>();

    [Serializable]
    public class AttachmentPoint
    {
        public string name = "Point";
        public Vector3 localPosition = Vector3.zero;
        public Quaternion localRotation = Quaternion.identity;
    }

    [Serializable]
    public class PathPoint
    {
        public Vector3 anchor;
        public Vector3 handleIn;
        public Vector3 handleOut;
        public PointType type;

        public PathPoint(Vector3 a, PointType t)
        {
            anchor = a;
            type = t;
            handleIn = a;
            handleOut = a;
        }
    }

    public enum PointType { Straight, Curved }

    [Serializable]
    public class Stroke
    {
        public string name = "Stroke";
        public bool closed = false;
        public List<PathPoint> controlPoints = new List<PathPoint>();

        [Header("Baked (Local Space)")]
        public List<Vector3> bakedPoints = new List<Vector3>();
        public List<float> cumulativeLengths = new List<float>(); // same count as bakedPoints
        public float totalLength = 0f;

        public void ClearBake()
        {
            bakedPoints.Clear();
            cumulativeLengths.Clear();
            totalLength = 0f;
        }

        /// Evaluate along baked polyline by normalized t [0..1], returns local position.
        public Vector3 EvaluateLocal(float t)
        {
            if (bakedPoints == null || bakedPoints.Count == 0) return Vector3.zero;
            if (bakedPoints.Count == 1) return bakedPoints[0];

            t = Mathf.Clamp01(t);
            if (totalLength <= Mathf.Epsilon) return bakedPoints[0];

            float targetDist = t * totalLength;

            // binary search cumulativeLengths for segment
            int lo = 0;
            int hi = cumulativeLengths.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (cumulativeLengths[mid] < targetDist) lo = mid + 1;
                else hi = mid;
            }

            int idx = Mathf.Clamp(lo, 1, cumulativeLengths.Count - 1);
            float d1 = cumulativeLengths[idx - 1];
            float d2 = cumulativeLengths[idx];
            float segLen = Mathf.Max(d2 - d1, 0.000001f);
            float u = Mathf.Clamp01((targetDist - d1) / segLen);

            return Vector3.Lerp(bakedPoints[idx - 1], bakedPoints[idx], u);
        }

        /// Local tangent (approx) from baked polyline at t
        public Vector3 TangentLocal(float t)
        {
            if (bakedPoints == null || bakedPoints.Count < 2) return Vector3.right;
            t = Mathf.Clamp01(t);
            Vector3 p = EvaluateLocal(t);
            Vector3 p2 = EvaluateLocal(Mathf.Clamp01(t + 0.0025f));
            Vector3 v = (p2 - p);
            if (v.sqrMagnitude < 1e-10f) v = bakedPoints[Mathf.Min(1, bakedPoints.Count - 1)] - bakedPoints[0];
            return v.normalized;
        }
    }
}
