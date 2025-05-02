using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NavMesh : MonoBehaviour
{
    public Graph MakeNavMesh(List<Wall> outline)
    {
        Graph g = new Graph();
        g.all_nodes = new List<GraphNode>();
        g.outline = outline;

        List<Vector3> vertices = outline.Select(w => w.start).ToList();
        List<List<Vector3>> convexPolygons = new List<List<Vector3>>();
        SplitPolygon(vertices, convexPolygons);

        for (int i = 0; i < convexPolygons.Count; i++)
        {
            List<Wall> polygonWalls = new List<Wall>();
            List<Vector3> polyVertices = convexPolygons[i];

            for (int j = 0; j < polyVertices.Count; j++)
            {
                Vector3 start = RoundToGrid(polyVertices[j]);
                Vector3 end = RoundToGrid(polyVertices[(j + 1) % polyVertices.Count]);
                polygonWalls.Add(new Wall(start, end));
            }

            GraphNode node = new GraphNode(i, polygonWalls);
            g.all_nodes.Add(node);
        }

        FindNeighbors(g.all_nodes);
        return g;
    }

    private void FindNeighbors(List<GraphNode> nodes)
    {
        foreach (var a in nodes)
        {
            List<Wall> a_walls = a.GetPolygon();
            foreach (var b in nodes)
            {
                if (a.GetID() == b.GetID()) continue;
                List<Wall> b_walls = b.GetPolygon();

                for (int i = 0; i < a_walls.Count; i++)
                {
                    for (int j = 0; j < b_walls.Count; j++)
                    {
                        if (a_walls[i].Same(b_walls[j]))
                        {
                            a.AddNeighbor(b, i);
                            b.AddNeighbor(a, j);
                        }
                    }
                }
            }
        }
    }

    private void SplitPolygon(List<Vector3> vertices, List<List<Vector3>> result)
    {
        if (vertices.Count <= 3 || IsConvex(vertices))
        {
            result.Add(new List<Vector3>(vertices));
            return;
        }

        int reflexIndex = FindReflexVertex(vertices);
        if (reflexIndex == -1)
        {
            result.Add(new List<Vector3>(vertices));
            return;
        }

        int bestVertex = FindBestDiagonal(vertices, reflexIndex);
        if (bestVertex == -1)
        {
            vertices.RemoveAt(reflexIndex);
            SplitPolygon(vertices, result);
            return;
        }

        List<Vector3> poly1 = new List<Vector3>();
        List<Vector3> poly2 = new List<Vector3>();

        int current = reflexIndex;
        do
        {
            poly1.Add(vertices[current]);
            current = (current + 1) % vertices.Count;
        } while (current != bestVertex);
        poly1.Add(vertices[bestVertex]);

        current = bestVertex;
        do
        {
            poly2.Add(vertices[current]);
            current = (current + 1) % vertices.Count;
        } while (current != reflexIndex);
        poly2.Add(vertices[reflexIndex]);

        SplitPolygon(poly1, result);
        SplitPolygon(poly2, result);
    }

    private bool IsConvex(List<Vector3> vertices)
    {
        int n = vertices.Count;
        if (n < 3) return false;

        bool gotNegative = false;
        bool gotPositive = false;

        for (int i = 0; i < n; i++)
        {
            Vector2 a = new Vector2(vertices[i].x, vertices[i].z);
            Vector2 b = new Vector2(vertices[(i + 1) % n].x, vertices[(i + 1) % n].z);
            Vector2 c = new Vector2(vertices[(i + 2) % n].x, vertices[(i + 2) % n].z);

            Vector2 ab = b - a;
            Vector2 bc = c - b;
            float cross = ab.x * bc.y - ab.y * bc.x;

            if (cross < 0) gotNegative = true;
            if (cross > 0) gotPositive = true;

            if (gotNegative && gotPositive) return false;
        }

        return true;
    }

    private int FindReflexVertex(List<Vector3> vertices)
    {
        int n = vertices.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 prev = new Vector2(vertices[(i - 1 + n) % n].x, vertices[(i - 1 + n) % n].z);
            Vector2 curr = new Vector2(vertices[i].x, vertices[i].z);
            Vector2 next = new Vector2(vertices[(i + 1) % n].x, vertices[(i + 1) % n].z);

            Vector2 dir1 = curr - prev;
            Vector2 dir2 = next - curr;

            float cross = dir1.x * dir2.y - dir1.y * dir2.x;
            if (cross < 0) return i;
        }

        return -1;
    }

    private int FindBestDiagonal(List<Vector3> vertices, int fromIndex)
    {
        Vector3 from = vertices[fromIndex];
        float bestScore = float.MinValue;
        int bestVertex = -1;

        for (int i = 0; i < vertices.Count; i++)
        {
            if (i == fromIndex || i == (fromIndex - 1 + vertices.Count) % vertices.Count || i == (fromIndex + 1) % vertices.Count)
                continue;

            if (IsDiagonalValid(vertices, fromIndex, i))
            {
                float score = ScoreDiagonal(vertices, fromIndex, i);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestVertex = i;
                }
            }
        }

        return bestVertex;
    }

    private bool IsDiagonalValid(List<Vector3> vertices, int from, int to)
    {
        Vector3 a = vertices[from];
        Vector3 b = vertices[to];

        for (int i = 0; i < vertices.Count; i++)
        {
            int j = (i + 1) % vertices.Count;
            if (i == from || i == to || j == from || j == to)
                continue;

            Vector3 c = vertices[i];
            Vector3 d = vertices[j];

            if (new Wall(a, b).Crosses(c, d))
                return false;
        }

        return true;
    }

    private float ScoreDiagonal(List<Vector3> vertices, int from, int to)
    {
        Vector2 a = new Vector2(vertices[from].x, vertices[from].z);
        Vector2 b = new Vector2(vertices[to].x, vertices[to].z);
        float length = Vector2.Distance(a, b);

        Vector2 prev = new Vector2(vertices[(from - 1 + vertices.Count) % vertices.Count].x,
                                   vertices[(from - 1 + vertices.Count) % vertices.Count].z);
        Vector2 dir1 = (a - prev).normalized;
        Vector2 dir2 = (b - a).normalized;
        float angle = Vector2.Angle(dir1, dir2);

        float angleScore = 1f - Mathf.Abs(angle - 90f) / 90f;
        float balance = 1f - Mathf.Abs((to - from + vertices.Count) % vertices.Count - vertices.Count / 2f) / (vertices.Count / 2f);

        return angleScore * 0.5f + balance * 0.3f - length * 0.2f;
    }

    private Vector3 RoundToGrid(Vector3 v, float precision = 0.001f)
    {
        return new Vector3(
            Mathf.Round(v.x / precision) * precision,
            Mathf.Round(v.y / precision) * precision,
            Mathf.Round(v.z / precision) * precision
        );
    }

    void Start()
    {
        EventBus.OnSetMap += SetMap;
    }

    public void SetMap(List<Wall> outline)
    {
        Graph navmesh = MakeNavMesh(outline);

        if (navmesh == null || navmesh.all_nodes.Count == 0)
        {
            Debug.LogError("NavMesh is empty or null.");
            return;
        }

        Debug.Log("NavMesh built with " + navmesh.all_nodes.Count + " nodes.");
        EventBus.SetGraph(navmesh);

        GameObject car = GameObject.FindWithTag("Player");
        if (car == null)
        {
            Debug.LogError("Car not found. Tag it as 'Player'.");
            return;
        }

        Vector3 carPos = car.transform.position;

        GraphNode start = null;
        foreach (var node in navmesh.all_nodes)
        {
            if (Util.PointInPolygon(carPos, node.GetPolygon()))
            {
                start = node;
                break;
            }
        }

        if (start == null)
        {
            Debug.LogWarning("Start node not found.");
            return;
        }

        // Pick farthest node from car as destination (excluding the start)
        GraphNode destination = navmesh.all_nodes
            .Where(n => n != start)
            .OrderByDescending(n => Vector3.Distance(n.GetCenter(), carPos))
            .FirstOrDefault();

        if (destination == null)
        {
            Debug.LogWarning("Destination node not found.");
            return;
        }

        Vector3 target = destination.GetCenter();
        Debug.Log($"Start node ID: {start.GetID()} → Destination node ID: {destination.GetID()}");

        EventBus.ShowTarget(target);
        EventBus.SetTarget(target);

    }
}
