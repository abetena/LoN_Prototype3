#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class AnamorphicDrawingEditor : EditorWindow
{
    private enum ToolMode { Draw, Edit }
    private ToolMode mode = ToolMode.Draw;

    private bool sessionActive;
    private bool isMouseDown;
    private float mouseDownTime;
    private Vector3 mouseDownPos;
    private const float holdThreshold = 0.2f;

    private AnamorphicDrawingInstance activeInstance;
    private AnamorphicDrawingAsset activeAsset;

    private int activeStrokeIndex = 0;

    [MenuItem("Tools/Nahuales/Anamorphic Drawing Editor")]
    public static void Open() => GetWindow<AnamorphicDrawingEditor>("Anamorphic Drawing");

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Selection.selectionChanged += Repaint;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Selection.selectionChanged -= Repaint;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Anamorphic Drawing Editor", EditorStyles.boldLabel);

        ResolveActiveSelection();

        using (new EditorGUI.DisabledScope(activeInstance == null))
        {
            sessionActive = EditorGUILayout.ToggleLeft("Session Active", sessionActive);

            mode = (ToolMode)EditorGUILayout.EnumPopup("Mode", mode);

            EditorGUILayout.Space(6);

            if (activeAsset == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject with AnamorphicDrawingInstance and assign an Asset.", MessageType.Warning);
            }
            else
            {
                DrawAssetControls();
                DrawStrokeControls();
                DrawBakeControls();
            }
        }
    }

    private void ResolveActiveSelection()
    {
        var go = Selection.activeGameObject;
        activeInstance = go ? go.GetComponent<AnamorphicDrawingInstance>() : null;
        activeAsset = activeInstance ? activeInstance.asset : null;
        if (activeAsset != null && activeStrokeIndex >= activeAsset.strokes.Count) activeStrokeIndex = Mathf.Max(0, activeAsset.strokes.Count - 1);
    }

    private void DrawAssetControls()
    {
        EditorGUI.BeginChangeCheck();
        activeInstance.asset = (AnamorphicDrawingAsset)EditorGUILayout.ObjectField("Asset", activeInstance.asset, typeof(AnamorphicDrawingAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            activeAsset = activeInstance.asset;
            activeStrokeIndex = 0;
            MarkDirty(activeInstance);
        }

        if (activeAsset == null) return;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("View Contract", EditorStyles.boldLabel);

        activeAsset.recommendedFOV = EditorGUILayout.Slider("Recommended FOV", activeAsset.recommendedFOV, 1f, 179f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Capture Viewpoint from SceneView"))
            {
                CaptureViewpointFromSceneView();
            }

            if (GUILayout.Button("LookAt = Bounds Center"))
            {
                activeAsset.lookAtLocalPosition = ComputeBoundsCenterLocal();
                MarkDirty(activeAsset);
            }
        }

        // Allow manual tweak of LookAt target via fields
        activeAsset.lookAtLocalPosition = EditorGUILayout.Vector3Field("LookAt Local Position", activeAsset.lookAtLocalPosition);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Sampling + Simplification", EditorStyles.boldLabel);

        activeAsset.maxSegmentLength = EditorGUILayout.FloatField("Max Segment Length", activeAsset.maxSegmentLength);
        activeAsset.maxBezierError = EditorGUILayout.FloatField("Max Bezier Error", activeAsset.maxBezierError);
        activeAsset.simplifyTolerance = EditorGUILayout.FloatField("Simplify Tolerance", activeAsset.simplifyTolerance);

        activeAsset.maxPointsPerStroke = EditorGUILayout.IntField("Max Points / Stroke", activeAsset.maxPointsPerStroke);
        activeAsset.maxTotalPoints = EditorGUILayout.IntField("Max Total Points", activeAsset.maxTotalPoints);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Deterministic Z Distortion", EditorStyles.boldLabel);

        activeAsset.seed = EditorGUILayout.IntField("Seed", activeAsset.seed);
        activeAsset.zAmplitude = EditorGUILayout.FloatField("Z Amplitude", activeAsset.zAmplitude);
        activeAsset.zFrequency = EditorGUILayout.FloatField("Z Frequency", activeAsset.zFrequency);

        if (GUI.changed) MarkDirty(activeAsset);
    }

    private void DrawStrokeControls()
    {
        if (activeAsset == null) return;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Strokes", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Stroke"))
            {
                Undo.RecordObject(activeAsset, "Add Stroke");
                activeAsset.strokes.Add(new AnamorphicDrawingAsset.Stroke { name = $"Stroke {activeAsset.strokes.Count + 1}" });
                activeStrokeIndex = activeAsset.strokes.Count - 1;
                MarkDirty(activeAsset);
            }

            using (new EditorGUI.DisabledScope(activeAsset.strokes.Count == 0))
            {
                if (GUILayout.Button("Delete Stroke"))
                {
                    Undo.RecordObject(activeAsset, "Delete Stroke");
                    activeAsset.strokes.RemoveAt(activeStrokeIndex);
                    activeStrokeIndex = Mathf.Clamp(activeStrokeIndex, 0, activeAsset.strokes.Count - 1);
                    MarkDirty(activeAsset);
                }
            }
        }

        if (activeAsset.strokes.Count == 0)
        {
            EditorGUILayout.HelpBox("No strokes yet. Add one.", MessageType.Info);
            return;
        }

        string[] names = new string[activeAsset.strokes.Count];
        for (int i = 0; i < names.Length; i++) names[i] = $"{i:00} - {activeAsset.strokes[i].name}";
        activeStrokeIndex = EditorGUILayout.Popup("Active Stroke", activeStrokeIndex, names);

        var stroke = activeAsset.strokes[activeStrokeIndex];
        stroke.name = EditorGUILayout.TextField("Name", stroke.name);
        stroke.closed = EditorGUILayout.Toggle("Closed", stroke.closed);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Undo Last Point"))
            {
                if (stroke.controlPoints.Count > 0)
                {
                    Undo.RecordObject(activeAsset, "Undo Last Point");
                    stroke.controlPoints.RemoveAt(stroke.controlPoints.Count - 1);
                    MarkDirty(activeAsset);
                }
            }
            if (GUILayout.Button("Clear Stroke"))
            {
                Undo.RecordObject(activeAsset, "Clear Stroke");
                stroke.controlPoints.Clear();
                stroke.ClearBake();
                MarkDirty(activeAsset);
            }
        }
    }

    private void DrawBakeControls()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Bake", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Bake All Strokes"))
            {
                BakeAll();
                activeInstance.RefreshRenderers();
            }

            if (GUILayout.Button("Bake Active Stroke"))
            {
                BakeStroke(activeStrokeIndex);
                activeInstance.RefreshRenderers();
            }
        }

        if (GUILayout.Button("Refresh Renderers Only"))
        {
            activeInstance.RefreshRenderers();
        }
    }

    private void OnSceneGUI(SceneView sv)
    {
        if (!sessionActive) return;

        ResolveActiveSelection();
        if (activeInstance == null || activeAsset == null) return;
        if (activeAsset.strokes.Count == 0) return;

        // Draw helper gizmos: viewpoint and lookAt
        Handles.color = Color.yellow;
        Vector3 vpW = activeInstance.transform.TransformPoint(activeAsset.viewpointLocalPosition);
        Handles.SphereHandleCap(0, vpW, Quaternion.identity, 0.15f, EventType.Repaint);

        Handles.color = Color.magenta;
        Vector3 laW = activeInstance.transform.TransformPoint(activeAsset.lookAtLocalPosition);
        Handles.SphereHandleCap(0, laW, Quaternion.identity, 0.13f, EventType.Repaint);

        Handles.color = Color.cyan;
        Handles.DrawLine(vpW, laW);

        // Allow moving LookAt target directly
        EditorGUI.BeginChangeCheck();
        Vector3 newLaW = Handles.PositionHandle(laW, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(activeAsset, "Move LookAt Target");
            activeAsset.lookAtLocalPosition = activeInstance.transform.InverseTransformPoint(newLaW);
            MarkDirty(activeAsset);
        }

        // Drawing interaction uses the instance's local plane (XY plane in local space)
        Event e = Event.current;

        if (mode == ToolMode.Draw)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        Vector3 localMouse = GetLocalMouseOnDrawingPlane(activeInstance.transform, e.mousePosition);

        var stroke = activeAsset.strokes[activeStrokeIndex];

        if (mode == ToolMode.Draw)
        {
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                    isMouseDown = true;
                    mouseDownTime = (float)EditorApplication.timeSinceStartup;
                    mouseDownPos = localMouse;
                    e.Use();
                    break;

                case EventType.MouseUp when e.button == 0 && isMouseDown:
                    isMouseDown = false;
                    float holdTime = (float)EditorApplication.timeSinceStartup - mouseDownTime;
                    var type = holdTime >= holdThreshold ? AnamorphicDrawingAsset.PointType.Curved : AnamorphicDrawingAsset.PointType.Straight;

                    Undo.RecordObject(activeAsset, "Add Point");

                    var pp = new AnamorphicDrawingAsset.PathPoint(mouseDownPos, type);

                    // Auto-initialize neighboring handles for nicer curves
                    if (type == AnamorphicDrawingAsset.PointType.Curved && stroke.controlPoints.Count > 0)
                    {
                        var prev = stroke.controlPoints[stroke.controlPoints.Count - 1];
                        Vector3 a0 = prev.anchor;
                        Vector3 a1 = mouseDownPos;

                        pp.handleIn = Vector3.Lerp(a0, a1, 0.25f);
                        prev.handleOut = Vector3.Lerp(a0, a1, 0.75f);
                    }

                    stroke.controlPoints.Add(pp);
                    MarkDirty(activeAsset);
                    e.Use();
                    break;
            }
        }

        // Draw + edit control points
        DrawStrokePreview(stroke);

        if (mode == ToolMode.Edit)
        {
            EditStrokeHandles(stroke);
        }

        // Repaint only when interacting; SceneView already repaints during handle drags
        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) sv.Repaint();
    }

    private void DrawStrokePreview(AnamorphicDrawingAsset.Stroke stroke)
    {
        // control point anchors + handles
        for (int i = 0; i < stroke.controlPoints.Count; i++)
        {
            var p = stroke.controlPoints[i];

            Vector3 wA = activeInstance.transform.TransformPoint(p.anchor);

            Handles.color = (p.type == AnamorphicDrawingAsset.PointType.Curved) ? Color.cyan : Color.red;
            Handles.SphereHandleCap(0, wA, Quaternion.identity, 0.12f, EventType.Repaint);

            if (p.type == AnamorphicDrawingAsset.PointType.Curved)
            {
                Handles.color = Color.gray;
                Handles.DrawLine(wA, activeInstance.transform.TransformPoint(p.handleIn));
                Handles.DrawLine(wA, activeInstance.transform.TransformPoint(p.handleOut));
            }
        }

        // segments
        Handles.color = Color.green;
        for (int i = 0; i < stroke.controlPoints.Count - 1; i++)
        {
            var p0 = stroke.controlPoints[i];
            var p1 = stroke.controlPoints[i + 1];

            Vector3 a0 = activeInstance.transform.TransformPoint(p0.anchor);
            Vector3 a1 = activeInstance.transform.TransformPoint(p1.anchor);

            if (p0.type == AnamorphicDrawingAsset.PointType.Curved || p1.type == AnamorphicDrawingAsset.PointType.Curved)
            {
                Vector3 c0 = activeInstance.transform.TransformPoint(p0.type == AnamorphicDrawingAsset.PointType.Curved ? p0.handleOut : p0.anchor);
                Vector3 c1 = activeInstance.transform.TransformPoint(p1.type == AnamorphicDrawingAsset.PointType.Curved ? p1.handleIn : p1.anchor);

                Handles.DrawBezier(a0, a1, c0, c1, Color.green, null, 2f);
            }
            else
            {
                Handles.DrawLine(a0, a1);
            }
        }
    }

    private void EditStrokeHandles(AnamorphicDrawingAsset.Stroke stroke)
    {
        for (int i = 0; i < stroke.controlPoints.Count; i++)
        {
            var p = stroke.controlPoints[i];

            // Move anchor
            EditorGUI.BeginChangeCheck();
            Vector3 wA = activeInstance.transform.TransformPoint(p.anchor);
            Vector3 wA2 = Handles.PositionHandle(wA, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(activeAsset, "Move Anchor");
                Vector3 newLocal = activeInstance.transform.InverseTransformPoint(wA2);
                Vector3 delta = newLocal - p.anchor;

                p.anchor = newLocal;
                // Move handles with anchor (keeps shape stable)
                p.handleIn += delta;
                p.handleOut += delta;
                MarkDirty(activeAsset);
            }

            if (p.type != AnamorphicDrawingAsset.PointType.Curved) continue;

            // Move handles
            EditorGUI.BeginChangeCheck();
            Vector3 wIn = activeInstance.transform.TransformPoint(p.handleIn);
            Vector3 wOut = activeInstance.transform.TransformPoint(p.handleOut);

            Vector3 wIn2 = Handles.PositionHandle(wIn, Quaternion.identity);
            Vector3 wOut2 = Handles.PositionHandle(wOut, Quaternion.identity);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(activeAsset, "Move Handles");
                p.handleIn = activeInstance.transform.InverseTransformPoint(wIn2);
                p.handleOut = activeInstance.transform.InverseTransformPoint(wOut2);
                MarkDirty(activeAsset);
            }
        }
    }

    private Vector3 GetLocalMouseOnDrawingPlane(Transform root, Vector2 guiMouse)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(guiMouse);

        // Plane is root local XY => world plane with normal root.forward passing through root.position
        Plane plane = new Plane(root.forward, root.position);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hit = ray.GetPoint(enter);
            return root.InverseTransformPoint(hit);
        }

        // fallback
        return Vector3.zero;
    }

    private void CaptureViewpointFromSceneView()
    {
        if (activeInstance == null || activeAsset == null) return;
        SceneView sv = SceneView.lastActiveSceneView;
        if (sv == null || sv.camera == null) return;

        Undo.RecordObject(activeAsset, "Capture Viewpoint");

        var cam = sv.camera.transform;
        activeAsset.viewpointLocalPosition = activeInstance.transform.InverseTransformPoint(cam.position);
        activeAsset.viewpointLocalRotation = Quaternion.Inverse(activeInstance.transform.rotation) * cam.rotation;

        // FOV from scene camera
        activeAsset.recommendedFOV = sv.camera.fieldOfView;

        // Default LookAt target: bounds center
        activeAsset.lookAtLocalPosition = ComputeBoundsCenterLocal();

        MarkDirty(activeAsset);
    }

    private Vector3 ComputeBoundsCenterLocal()
    {
        if (activeAsset == null) return Vector3.zero;

        // Prefer baked bounds if available, else use control points
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        bool has = false;

        foreach (var s in activeAsset.strokes)
        {
            var pts = (s.bakedPoints != null && s.bakedPoints.Count > 0) ? s.bakedPoints : null;
            if (pts != null)
            {
                for (int i = 0; i < pts.Count; i++)
                {
                    if (!has) { b = new Bounds(pts[i], Vector3.zero); has = true; }
                    else b.Encapsulate(pts[i]);
                }
            }
            else
            {
                for (int i = 0; i < s.controlPoints.Count; i++)
                {
                    if (!has) { b = new Bounds(s.controlPoints[i].anchor, Vector3.zero); has = true; }
                    else b.Encapsulate(s.controlPoints[i].anchor);
                }
            }
        }

        if (!has) return Vector3.zero;
        return b.center;
    }

    private void BakeAll()
    {
        if (activeAsset == null) return;

        Undo.RecordObject(activeAsset, "Bake All Strokes");

        int total = 0;
        for (int i = 0; i < activeAsset.strokes.Count; i++)
        {
            total += BakeStrokeInternal(i, ref total);
        }

        MarkDirty(activeAsset);
        AssetDatabase.SaveAssets();
    }

    private void BakeStroke(int index)
    {
        if (activeAsset == null) return;

        Undo.RecordObject(activeAsset, "Bake Stroke");

        int total = 0;
        BakeStrokeInternal(index, ref total);

        MarkDirty(activeAsset);
        AssetDatabase.SaveAssets();
    }

    private int BakeStrokeInternal(int index, ref int totalPointsSoFar)
    {
        var stroke = activeAsset.strokes[index];
        stroke.ClearBake();

        if (stroke.controlPoints == null || stroke.controlPoints.Count < 2) return 0;

        // Sample curve adaptively into local points on plane, then apply deterministic z noise, then simplify.
        List<Vector3> sampled = new List<Vector3>(512);

        int segCount = stroke.closed ? stroke.controlPoints.Count : stroke.controlPoints.Count - 1;

        for (int i = 0; i < segCount; i++)
        {
            var p0 = stroke.controlPoints[i];
            var p1 = stroke.controlPoints[(i + 1) % stroke.controlPoints.Count];

            bool bez = (p0.type == AnamorphicDrawingAsset.PointType.Curved || p1.type == AnamorphicDrawingAsset.PointType.Curved);
            if (bez)
            {
                Vector3 a0 = p0.anchor;
                Vector3 a1 = p1.anchor;
                Vector3 c0 = (p0.type == AnamorphicDrawingAsset.PointType.Curved) ? p0.handleOut : p0.anchor;
                Vector3 c1 = (p1.type == AnamorphicDrawingAsset.PointType.Curved) ? p1.handleIn : p1.anchor;

                SampleCubicBezierAdaptive(a0, c0, c1, a1, activeAsset.maxBezierError, activeAsset.maxSegmentLength, sampled, includeStart: sampled.Count == 0);
            }
            else
            {
                SampleLineBySpacing(p0.anchor, p1.anchor, activeAsset.maxSegmentLength, sampled, includeStart: sampled.Count == 0);
            }

            // safety
            if (sampled.Count > activeAsset.maxPointsPerStroke) break;
        }

        // Apply deterministic z noise by arc-length
        ApplyDeterministicZNoise(sampled, activeAsset.seed, index, activeAsset.zAmplitude, activeAsset.zFrequency);

        // Simplify (RDP)
        if (activeAsset.simplifyTolerance > 0f && sampled.Count > 3)
        {
            sampled = SimplifyRDP(sampled, activeAsset.simplifyTolerance, stroke.closed);
        }

        // Caps
        if (sampled.Count > activeAsset.maxPointsPerStroke)
        {
            sampled.RemoveRange(activeAsset.maxPointsPerStroke, sampled.Count - activeAsset.maxPointsPerStroke);
        }

        // Total cap across all strokes
        if (totalPointsSoFar + sampled.Count > activeAsset.maxTotalPoints)
        {
            int allowed = Mathf.Max(0, activeAsset.maxTotalPoints - totalPointsSoFar);
            if (allowed < sampled.Count) sampled.RemoveRange(allowed, sampled.Count - allowed);
        }

        // Write bake + lengths
        stroke.bakedPoints = sampled;
        RebuildLengths(stroke);

        totalPointsSoFar += sampled.Count;
        return sampled.Count;
    }

    private static void SampleLineBySpacing(Vector3 a, Vector3 b, float maxSegLen, List<Vector3> outPts, bool includeStart)
    {
        float dist = Vector3.Distance(a, b);
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / Mathf.Max(0.0001f, maxSegLen)));
        for (int i = 0; i <= steps; i++)
        {
            if (!includeStart && i == 0) continue;
            float t = i / (float)steps;
            outPts.Add(Vector3.Lerp(a, b, t));
        }
    }

    private static Vector3 CubicBezier(Vector3 a0, Vector3 c0, Vector3 c1, Vector3 a1, float t)
    {
        float u = 1f - t;
        return (u * u * u) * a0 +
               (3f * u * u * t) * c0 +
               (3f * u * t * t) * c1 +
               (t * t * t) * a1;
    }

    private static void SampleCubicBezierAdaptive(
        Vector3 a0, Vector3 c0, Vector3 c1, Vector3 a1,
        float maxError, float maxSegLen,
        List<Vector3> outPts,
        bool includeStart)
    {
        // recursive subdivision based on flatness + max segment length
        void Recurse(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int depth)
        {
            if (depth > 18)
            {
                // emergency stop
                if (includeStart || outPts.Count > 0) outPts.Add(p3);
                return;
            }

            if (IsBezierFlatEnough(p0, p1, p2, p3, maxError) && Vector3.Distance(p0, p3) <= maxSegLen * 1.5f)
            {
                if (includeStart || outPts.Count > 0) outPts.Add(p3);
                return;
            }

            // split
            SplitBezier(p0, p1, p2, p3, out var l0, out var l1, out var l2, out var l3, out var r0, out var r1, out var r2, out var r3);

            Recurse(l0, l1, l2, l3, depth + 1);
            Recurse(r0, r1, r2, r3, depth + 1);
        }

        if (includeStart) outPts.Add(a0);
        Recurse(a0, c0, c1, a1, 0);
    }

    private static bool IsBezierFlatEnough(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float maxError)
    {
        // Distance of control points from chord
        float d1 = DistancePointToLine(p1, p0, p3);
        float d2 = DistancePointToLine(p2, p0, p3);
        return (d1 <= maxError && d2 <= maxError);
    }

    private static float DistancePointToLine(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Vector3.Dot(p - a, ab) / Mathf.Max(0.000001f, Vector3.Dot(ab, ab));
        Vector3 proj = a + Mathf.Clamp01(t) * ab;
        return Vector3.Distance(p, proj);
    }

    private static void SplitBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
        out Vector3 l0, out Vector3 l1, out Vector3 l2, out Vector3 l3,
        out Vector3 r0, out Vector3 r1, out Vector3 r2, out Vector3 r3)
    {
        Vector3 p01 = (p0 + p1) * 0.5f;
        Vector3 p12 = (p1 + p2) * 0.5f;
        Vector3 p23 = (p2 + p3) * 0.5f;

        Vector3 p012 = (p01 + p12) * 0.5f;
        Vector3 p123 = (p12 + p23) * 0.5f;

        Vector3 p0123 = (p012 + p123) * 0.5f;

        l0 = p0;   l1 = p01;  l2 = p012; l3 = p0123;
        r0 = p0123; r1 = p123; r2 = p23;  r3 = p3;
    }

    private static void ApplyDeterministicZNoise(List<Vector3> pts, int assetSeed, int strokeIndex, float amp, float freq)
    {
        if (pts == null || pts.Count == 0) return;
        if (Mathf.Approximately(amp, 0f)) return;

        float seedY = (assetSeed * 0.001f) + (strokeIndex * 0.173f);

        float s = 0f;
        pts[0] = new Vector3(pts[0].x, pts[0].y, ZAt(0f));

        for (int i = 1; i < pts.Count; i++)
        {
            s += Vector3.Distance(pts[i - 1], pts[i]);
            float z = ZAt(s);
            pts[i] = new Vector3(pts[i].x, pts[i].y, z);
        }

        float ZAt(float arcLen)
        {
            float x = arcLen * Mathf.Max(0.00001f, freq);
            float n = Mathf.PerlinNoise(x, seedY);      // [0..1]
            float signed = (n * 2f) - 1f;               // [-1..1]
            return signed * amp;
        }
    }

    private static void RebuildLengths(AnamorphicDrawingAsset.Stroke stroke)
    {
        stroke.cumulativeLengths.Clear();
        stroke.totalLength = 0f;

        if (stroke.bakedPoints == null || stroke.bakedPoints.Count == 0) return;

        stroke.cumulativeLengths.Add(0f);
        float sum = 0f;

        for (int i = 1; i < stroke.bakedPoints.Count; i++)
        {
            sum += Vector3.Distance(stroke.bakedPoints[i - 1], stroke.bakedPoints[i]);
            stroke.cumulativeLengths.Add(sum);
        }

        stroke.totalLength = sum;
    }

    // Ramer–Douglas–Peucker simplification
    private static List<Vector3> SimplifyRDP(List<Vector3> pts, float tol, bool closed)
    {
        if (pts == null || pts.Count < 3) return pts;

        List<Vector3> input = pts;

        // For closed strokes, simplify the open polyline and ensure closure visually via LR.loop.
        // (If you truly need last==first, handle that later. Robust-first keeps it simple.)
        bool[] keep = new bool[input.Count];
        keep[0] = true;
        keep[input.Count - 1] = true;

        RDPRecursive(input, 0, input.Count - 1, tol, keep);

        List<Vector3> output = new List<Vector3>(input.Count);
        for (int i = 0; i < input.Count; i++)
        {
            if (keep[i]) output.Add(input[i]);
        }

        return output;
    }

    private static void RDPRecursive(List<Vector3> pts, int start, int end, float tol, bool[] keep)
    {
        if (end <= start + 1) return;

        float maxDist = -1f;
        int idx = -1;

        Vector3 a = pts[start];
        Vector3 b = pts[end];

        for (int i = start + 1; i < end; i++)
        {
            float d = DistancePointToSegment(pts[i], a, b);
            if (d > maxDist)
            {
                maxDist = d;
                idx = i;
            }
        }

        if (maxDist > tol && idx != -1)
        {
            keep[idx] = true;
            RDPRecursive(pts, start, idx, tol, keep);
            RDPRecursive(pts, idx, end, tol, keep);
        }
    }

    private static float DistancePointToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Vector3.Dot(p - a, ab) / Mathf.Max(0.000001f, Vector3.Dot(ab, ab));
        t = Mathf.Clamp01(t);
        Vector3 proj = a + t * ab;
        return Vector3.Distance(p, proj);
    }

    private static void MarkDirty(Object obj)
    {
        EditorUtility.SetDirty(obj);
    }
}
#endif
