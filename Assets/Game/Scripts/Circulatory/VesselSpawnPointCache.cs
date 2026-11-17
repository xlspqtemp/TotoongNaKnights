using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class VesselSpawnPointCache : MonoBehaviour
{
    private const int DefaultCandidateSampleCount = 1000;
    private const int DefaultKeptPointCount = 40;
    private const float DefaultMinimumPointDistance = 1.5f;
    private const float DefaultSurfaceInset = 0.02f;
    private const float DefaultEndpointExclusionDistance = 1.5f;
    private const float VertexWeldTolerance = 0.0001f;
    private const float GizmoSphereRadius = 0.08f;

    [SerializeField] private MeshFilter vesselMeshFilter;
    [SerializeField] private Transform leftFootAnchor;
    [SerializeField] private Transform rightFootAnchor;
    [SerializeField] private Transform leftHandAnchor;
    [SerializeField] private Transform rightHandAnchor;
    [SerializeField] private Transform headAnchor;
    [SerializeField, Min(200)] private int candidateSampleCount = DefaultCandidateSampleCount;
    [SerializeField, Min(1)] private int keptPointCount = DefaultKeptPointCount;
    [SerializeField, Min(0f)] private float minimumPointDistance = DefaultMinimumPointDistance;
    [SerializeField, Min(0f)] private float surfaceInset = DefaultSurfaceInset;
    [SerializeField, Min(0f)] private float endpointExclusionDistance = DefaultEndpointExclusionDistance;
    [SerializeField] private bool showGizmos = true;

    private readonly List<Vector3> localSpawnPoints = new List<Vector3>();
    private readonly List<Vector3> worldSpawnPoints = new List<Vector3>();
    private readonly List<BoundaryEdge> openEndpointEdges = new List<BoundaryEdge>();
    private Matrix4x4 cachedLocalToWorld;
    private bool worldPointsInitialized;

    public IReadOnlyList<Vector3> WorldSpawnPoints
    {
        get
        {
            RefreshWorldSpawnPoints();
            return worldSpawnPoints;
        }
    }

    private void Start()
    {
        CacheSpawnPoints();
    }

    private void CacheSpawnPoints()
    {
        if (vesselMeshFilter == null)
            vesselMeshFilter = GetComponent<MeshFilter>();

        if (vesselMeshFilter == null || vesselMeshFilter.sharedMesh == null)
        {
            Debug.LogError("VesselSpawnPointCache could not sample points because its vessel MeshFilter or mesh is missing.", this);
            return;
        }

        Mesh mesh = vesselMeshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        CacheOpenEndpointEdges(vertices, triangles);
        Matrix4x4 localToWorld = vesselMeshFilter.transform.localToWorldMatrix;
        List<TriangleArea> weightedTriangles = BuildWeightedTriangles(vertices, triangles, localToWorld, out double totalArea);
        if (weightedTriangles.Count == 0 || totalArea <= 0d)
        {
            Debug.LogError("VesselSpawnPointCache could not sample points because the vessel mesh has no usable triangles.", this);
            return;
        }

        System.Random random = new System.Random();
        List<SampledPoint> candidates = new List<SampledPoint>(Mathf.Max(200, candidateSampleCount));
        int samplesToGenerate = Mathf.Max(200, candidateSampleCount);
        for (int i = 0; i < samplesToGenerate; i++)
        {
            candidates.Add(SamplePoint(vertices, weightedTriangles, totalArea, localToWorld, random));
        }

        SelectSpreadOutPoints(candidates, Mathf.Max(1, keptPointCount), Mathf.Max(0f, minimumPointDistance));
        RefreshWorldSpawnPoints();
        LogPointSummary();

        if (localSpawnPoints.Count < Mathf.Max(1, keptPointCount))
        {
            Debug.LogWarning($"VesselSpawnPointCache kept {localSpawnPoints.Count} of {Mathf.Max(1, keptPointCount)} requested points at the configured minimum spacing. No fallback points were created.", this);
        }
    }

    /// <summary>Returns whether a world-space position is too close to an open end of the vessel mesh.</summary>
    public bool IsNearOpenVesselEnd(Vector3 worldPosition)
    {
        if (endpointExclusionDistance <= 0f || openEndpointEdges.Count == 0 || vesselMeshFilter == null)
            return false;

        float minimumDistanceSquared = endpointExclusionDistance * endpointExclusionDistance;
        Transform meshTransform = vesselMeshFilter.transform;
        for (int index = 0; index < openEndpointEdges.Count; index++)
        {
            BoundaryEdge edge = openEndpointEdges[index];
            Vector3 edgeStart = meshTransform.TransformPoint(edge.LocalStart);
            Vector3 edgeEnd = meshTransform.TransformPoint(edge.LocalEnd);
            if (GetPointToSegmentDistanceSquared(worldPosition, edgeStart, edgeEnd) < minimumDistanceSquared)
                return true;
        }

        return false;
    }

    private void CacheOpenEndpointEdges(Vector3[] vertices, int[] triangles)
    {
        openEndpointEdges.Clear();
        if (vertices == null || triangles == null || triangles.Length < 3)
            return;

        Dictionary<Vector3Int, int> weldedVertexIds = new Dictionary<Vector3Int, int>();
        List<int> representativeVertexIndices = new List<int>();
        int[] weldedVertexIndex = new int[vertices.Length];
        float inverseWeldTolerance = 1f / VertexWeldTolerance;
        for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
        {
            Vector3 vertex = vertices[vertexIndex];
            Vector3Int key = new Vector3Int(
                Mathf.RoundToInt(vertex.x * inverseWeldTolerance),
                Mathf.RoundToInt(vertex.y * inverseWeldTolerance),
                Mathf.RoundToInt(vertex.z * inverseWeldTolerance));
            if (!weldedVertexIds.TryGetValue(key, out int weldedId))
            {
                weldedId = representativeVertexIndices.Count;
                weldedVertexIds.Add(key, weldedId);
                representativeVertexIndices.Add(vertexIndex);
            }

            weldedVertexIndex[vertexIndex] = weldedId;
        }

        Dictionary<ulong, BoundaryEdgeCount> edgeCounts = new Dictionary<ulong, BoundaryEdgeCount>();
        for (int triangleIndex = 0; triangleIndex + 2 < triangles.Length; triangleIndex += 3)
        {
            int first = triangles[triangleIndex];
            int second = triangles[triangleIndex + 1];
            int third = triangles[triangleIndex + 2];
            AddEdgeOccurrence(edgeCounts, weldedVertexIndex[first], weldedVertexIndex[second]);
            AddEdgeOccurrence(edgeCounts, weldedVertexIndex[second], weldedVertexIndex[third]);
            AddEdgeOccurrence(edgeCounts, weldedVertexIndex[third], weldedVertexIndex[first]);
        }

        foreach (BoundaryEdgeCount edge in edgeCounts.Values)
        {
            if (edge.OccurrenceCount != 1)
                continue;

            Vector3 localStart = vertices[representativeVertexIndices[edge.FirstVertex]];
            Vector3 localEnd = vertices[representativeVertexIndices[edge.SecondVertex]];
            openEndpointEdges.Add(new BoundaryEdge(localStart, localEnd));
        }

        if (openEndpointEdges.Count == 0)
        {
            Debug.LogWarning("VesselSpawnPointCache found no open mesh ends; pathogen endpoint exclusion is inactive for this vessel mesh.", this);
        }
        else
        {
            Debug.Log($"VesselSpawnPointCache excluded pathogen spawns within {endpointExclusionDistance:0.##} world units of {openEndpointEdges.Count} open vessel-end edges.", this);
        }
    }

    private static void AddEdgeOccurrence(Dictionary<ulong, BoundaryEdgeCount> edgeCounts, int firstVertex, int secondVertex)
    {
        if (firstVertex == secondVertex)
            return;

        int minimumVertex = Mathf.Min(firstVertex, secondVertex);
        int maximumVertex = Mathf.Max(firstVertex, secondVertex);
        ulong edgeKey = ((ulong)(uint)minimumVertex << 32) | (uint)maximumVertex;
        if (edgeCounts.TryGetValue(edgeKey, out BoundaryEdgeCount edge))
        {
            edge.OccurrenceCount++;
            edgeCounts[edgeKey] = edge;
        }
        else
        {
            edgeCounts.Add(edgeKey, new BoundaryEdgeCount(minimumVertex, maximumVertex));
        }
    }

    private static float GetPointToSegmentDistanceSquared(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        Vector3 segment = segmentEnd - segmentStart;
        float segmentLengthSquared = segment.sqrMagnitude;
        if (segmentLengthSquared <= Mathf.Epsilon)
            return (point - segmentStart).sqrMagnitude;

        float projection = Mathf.Clamp01(Vector3.Dot(point - segmentStart, segment) / segmentLengthSquared);
        Vector3 closestPoint = segmentStart + segment * projection;
        return (point - closestPoint).sqrMagnitude;
    }

    private static List<TriangleArea> BuildWeightedTriangles(Vector3[] vertices, int[] triangles, Matrix4x4 localToWorld, out double totalArea)
    {
        List<TriangleArea> weightedTriangles = new List<TriangleArea>(triangles.Length / 3);
        totalArea = 0d;
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];
            Vector3 worldA = localToWorld.MultiplyPoint3x4(vertices[a]);
            Vector3 worldB = localToWorld.MultiplyPoint3x4(vertices[b]);
            Vector3 worldC = localToWorld.MultiplyPoint3x4(vertices[c]);
            float area = Vector3.Cross(worldB - worldA, worldC - worldA).magnitude * 0.5f;
            if (area <= Mathf.Epsilon)
                continue;

            totalArea += area;
            weightedTriangles.Add(new TriangleArea(a, b, c, totalArea));
        }

        return weightedTriangles;
    }

    private SampledPoint SamplePoint(Vector3[] vertices, List<TriangleArea> weightedTriangles, double totalArea, Matrix4x4 localToWorld, System.Random random)
    {
        double areaPick = random.NextDouble() * totalArea;
        int low = 0;
        int high = weightedTriangles.Count - 1;
        while (low < high)
        {
            int middle = (low + high) / 2;
            if (areaPick <= weightedTriangles[middle].CumulativeArea)
                high = middle;
            else
                low = middle + 1;
        }

        TriangleArea triangle = weightedTriangles[low];
        double root = Math.Sqrt(random.NextDouble());
        double second = random.NextDouble();
        float weightA = (float)(1d - root);
        float weightB = (float)(root * (1d - second));
        float weightC = (float)(root * second);
        Vector3 localSurfacePoint = vertices[triangle.A] * weightA + vertices[triangle.B] * weightB + vertices[triangle.C] * weightC;
        Vector3 worldA = localToWorld.MultiplyPoint3x4(vertices[triangle.A]);
        Vector3 worldB = localToWorld.MultiplyPoint3x4(vertices[triangle.B]);
        Vector3 worldC = localToWorld.MultiplyPoint3x4(vertices[triangle.C]);
        Vector3 worldNormal = Vector3.Cross(worldB - worldA, worldC - worldA).normalized;
        Vector3 worldPoint = localToWorld.MultiplyPoint3x4(localSurfacePoint) - worldNormal * Mathf.Max(0f, surfaceInset);
        Vector3 localPoint = vesselMeshFilter.transform.InverseTransformPoint(worldPoint);
        return new SampledPoint(localPoint, worldPoint);
    }

    private void SelectSpreadOutPoints(List<SampledPoint> candidates, int targetCount, float minimumDistance)
    {
        localSpawnPoints.Clear();
        worldSpawnPoints.Clear();
        bool[] selected = new bool[candidates.Count];
        float minimumDistanceSquared = minimumDistance * minimumDistance;

        if (candidates.Count == 0)
            return;

        int first = UnityEngine.Random.Range(0, candidates.Count);
        AddCandidate(candidates[first]);
        selected[first] = true;

        while (localSpawnPoints.Count < targetCount)
        {
            int bestIndex = -1;
            float bestNearestDistanceSquared = minimumDistanceSquared;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (selected[i])
                    continue;

                float nearestDistanceSquared = float.PositiveInfinity;
                for (int keptIndex = 0; keptIndex < worldSpawnPoints.Count; keptIndex++)
                {
                    float distanceSquared = (candidates[i].WorldPosition - worldSpawnPoints[keptIndex]).sqrMagnitude;
                    if (distanceSquared < nearestDistanceSquared)
                        nearestDistanceSquared = distanceSquared;
                }

                if (nearestDistanceSquared > bestNearestDistanceSquared)
                {
                    bestNearestDistanceSquared = nearestDistanceSquared;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
                break;

            selected[bestIndex] = true;
            AddCandidate(candidates[bestIndex]);
        }
    }

    private void AddCandidate(SampledPoint candidate)
    {
        localSpawnPoints.Add(candidate.LocalPosition);
        worldSpawnPoints.Add(candidate.WorldPosition);
    }

    private void RefreshWorldSpawnPoints()
    {
        if (vesselMeshFilter == null)
            return;

        Transform meshTransform = vesselMeshFilter.transform;
        Matrix4x4 currentLocalToWorld = meshTransform.localToWorldMatrix;
        if (worldPointsInitialized && currentLocalToWorld == cachedLocalToWorld)
            return;

        worldSpawnPoints.Clear();
        for (int i = 0; i < localSpawnPoints.Count; i++)
            worldSpawnPoints.Add(meshTransform.TransformPoint(localSpawnPoints[i]));

        cachedLocalToWorld = currentLocalToWorld;
        worldPointsInitialized = true;
    }

    private void LogPointSummary()
    {
        Transform[] anchors = { leftFootAnchor, rightFootAnchor, leftHandAnchor, rightHandAnchor, headAnchor };
        string[] names = { "Left Foot", "Right Foot", "Left Hand", "Right Hand", "Head" };
        int[] counts = new int[anchors.Length];
        RefreshWorldSpawnPoints();

        for (int i = 0; i < worldSpawnPoints.Count; i++)
        {
            int nearestAnchorIndex = -1;
            float nearestDistanceSquared = float.PositiveInfinity;
            for (int anchorIndex = 0; anchorIndex < anchors.Length; anchorIndex++)
            {
                if (anchors[anchorIndex] == null)
                    continue;

                float distanceSquared = (worldSpawnPoints[i] - anchors[anchorIndex].position).sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearestAnchorIndex = anchorIndex;
                }
            }

            if (nearestAnchorIndex >= 0)
                counts[nearestAnchorIndex]++;
        }

        string anchorSummary = string.Empty;
        for (int i = 0; i < names.Length; i++)
        {
            string count = anchors[i] == null ? "0 (anchor missing)" : counts[i].ToString();
            anchorSummary += $"{(i == 0 ? string.Empty : ", ")}{names[i]}: {count}";
        }

        if (worldSpawnPoints.Count == 0)
        {
            Debug.Log($"Vessel spawn points kept: 0; nearest anchors: {anchorSummary}; bounds: none.", this);
            return;
        }

        Bounds bounds = new Bounds(worldSpawnPoints[0], Vector3.zero);
        for (int i = 1; i < worldSpawnPoints.Count; i++)
            bounds.Encapsulate(worldSpawnPoints[i]);

        Debug.Log($"Vessel spawn points kept: {worldSpawnPoints.Count}; nearest anchors: {anchorSummary}; bounds min: {bounds.min}, max: {bounds.max}.", this);
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos)
            return;

#if UNITY_EDITOR
        if (SceneView.currentDrawingSceneView == null)
            return;
#endif

        RefreshWorldSpawnPoints();
        Gizmos.color = Color.cyan;
        for (int i = 0; i < worldSpawnPoints.Count; i++)
            Gizmos.DrawSphere(worldSpawnPoints[i], GizmoSphereRadius);
    }

    private struct BoundaryEdge
    {
        public readonly Vector3 LocalStart;
        public readonly Vector3 LocalEnd;

        public BoundaryEdge(Vector3 localStart, Vector3 localEnd)
        {
            LocalStart = localStart;
            LocalEnd = localEnd;
        }
    }

    private struct BoundaryEdgeCount
    {
        public readonly int FirstVertex;
        public readonly int SecondVertex;
        public int OccurrenceCount;

        public BoundaryEdgeCount(int firstVertex, int secondVertex)
        {
            FirstVertex = firstVertex;
            SecondVertex = secondVertex;
            OccurrenceCount = 1;
        }
    }

    private struct TriangleArea
    {
        public readonly int A;
        public readonly int B;
        public readonly int C;
        public readonly double CumulativeArea;

        public TriangleArea(int a, int b, int c, double cumulativeArea)
        {
            A = a;
            B = b;
            C = c;
            CumulativeArea = cumulativeArea;
        }
    }

    private struct SampledPoint
    {
        public readonly Vector3 LocalPosition;
        public readonly Vector3 WorldPosition;

        public SampledPoint(Vector3 localPosition, Vector3 worldPosition)
        {
            LocalPosition = localPosition;
            WorldPosition = worldPosition;
        }
    }
}
