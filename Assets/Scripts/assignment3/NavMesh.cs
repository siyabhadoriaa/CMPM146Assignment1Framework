using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class NavMesh : MonoBehaviour
{
    // implement NavMesh generation here:
    //    the outline are Walls in counterclockwise order
    //    iterate over them, and if you find a reflex angle
    //    you have to split the polygon into two
    //    then perform the same operation on both parts
    //    until no more reflex angles are present
    //
    //    when you have a number of polygons, you will have
    //    to convert them into a graph: each polygon is a node
    //    you can find neighbors by finding shared edges between
    //    different polygons (or you can keep track of this while 
    //    you are splitting)
    public Graph MakeNavMesh(List<Wall> outline)
    {
        Graph g = new Graph();
        g.all_nodes = new List<GraphNode>();
        g.outline = outline;

        // Convert walls to vertices for processing
        List<Vector3> vertices = outline.Select(w => w.start).ToList();
        List<List<Vector3>> convexPolygons = new List<List<Vector3>>();

        // Process the polygon until it can't be split anymore
        SplitPolygon(vertices, convexPolygons);

        // Create graph nodes from the convex polygons
        for (int i = 0; i < convexPolygons.Count; i++)
        {
            List<Wall> polygonWalls = new List<Wall>();
            List<Vector3> polyVertices = convexPolygons[i];

            // Create walls for the polygon in counterclockwise order
            for (int j = 0; j < polyVertices.Count; j++)
            {
                Vector3 start = polyVertices[j];
                Vector3 end = polyVertices[(j + 1) % polyVertices.Count];
                polygonWalls.Add(new Wall(start, end));
            }

            // Create node and add to graph
            GraphNode node = new GraphNode(i, polygonWalls);
            g.all_nodes.Add(node);
        }

        // Find neighbors between nodes
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
                if (a.GetID() != b.GetID())
                {
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
    }

    private void SplitPolygon(List<Vector3> vertices, List<List<Vector3>> result)
    {
        // If polygon is already convex or a triangle, add it to results
        if (vertices.Count <= 3 || IsConvex(vertices))
        {
            result.Add(new List<Vector3>(vertices));
            return;
        }

        // Find a reflex vertex and a suitable diagonal
        int reflexIndex = FindReflexVertex(vertices);
        if (reflexIndex == -1)
        {
            // No reflex vertices found (shouldn't happen if not convex)
            result.Add(new List<Vector3>(vertices));
            return;
        }

        // Find best vertex to connect to
        int bestVertex = FindBestDiagonal(vertices, reflexIndex);
        if (bestVertex == -1)
        {
            // No valid diagonal found, try next reflex vertex
            vertices.RemoveAt(reflexIndex);
            SplitPolygon(vertices, result);
            return;
        }

        // Split the polygon into two parts
        List<Vector3> poly1 = new List<Vector3>();
        List<Vector3> poly2 = new List<Vector3>();

        // Build the two new polygons
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

        // Recursively process the two new polygons
        SplitPolygon(poly1, result);
        SplitPolygon(poly2, result);
    }

    private bool IsConvex(List<Vector3> vertices)
    {
        if (vertices.Count < 3) return false;

        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 current = vertices[i];
            Vector3 next = vertices[(i + 1) % vertices.Count];
            Vector3 next2 = vertices[(i + 2) % vertices.Count];

            Vector3 edge1 = next - current;
            Vector3 edge2 = next2 - next;

            // Check if the cross product's y component is negative (for CCW order)
            if (Vector3.Cross(edge1, edge2).y < 0)
                return false;
        }
        return true;
    }

    private int FindReflexVertex(List<Vector3> vertices)
    {
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 prev = vertices[(i - 1 + vertices.Count) % vertices.Count];
            Vector3 current = vertices[i];
            Vector3 next = vertices[(i + 1) % vertices.Count];

            Vector3 edge1 = current - prev;
            Vector3 edge2 = next - current;

            // If cross product's y component is negative, this is a reflex vertex
            if (Vector3.Cross(edge1, edge2).y < 0)
                return i;
        }
        return -1;
    }

    private int FindBestDiagonal(List<Vector3> vertices, int fromIndex)
    {
        Vector3 from = vertices[fromIndex];
        float bestScore = float.MinValue;
        int bestVertex = -1;

        // Try to find the best vertex to connect to
        for (int i = 0; i < vertices.Count; i++)
        {
            // Skip adjacent vertices and self
            if (i == fromIndex || i == (fromIndex - 1 + vertices.Count) % vertices.Count ||
                i == (fromIndex + 1) % vertices.Count)
                continue;

            if (IsDiagonalValid(vertices, fromIndex, i))
            {
                // Score the diagonal based on angles it creates
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
        Vector3 diagonal = vertices[to] - vertices[from];

        // Check if diagonal intersects with any edge
        for (int i = 0; i < vertices.Count; i++)
        {
            int next = (i + 1) % vertices.Count;

            // Skip edges that share vertices with the diagonal
            if (i == from || i == to || next == from || next == to)
                continue;

            Vector3 edge = vertices[next] - vertices[i];
            
            // Check for intersection
            if (LinesIntersect(vertices[from], vertices[to], vertices[i], vertices[next]))
                return false;
        }

        return true;
    }

    private float ScoreDiagonal(List<Vector3> vertices, int from, int to)
    {
        // Score based on the angles created by the diagonal
        // Prefer diagonals that create angles closer to 90 degrees
        Vector3 diagonal = vertices[to] - vertices[from];
        float length = diagonal.magnitude;
        
        // Shorter diagonals are preferred
        return -length;
    }

    private bool LinesIntersect(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
    {
        // Convert to 2D for intersection test (ignore Y component)
        Vector2 a = new Vector2(p1.x, p1.z);
        Vector2 b = new Vector2(p2.x, p2.z);
        Vector2 c = new Vector2(p3.x, p3.z);
        Vector2 d = new Vector2(p4.x, p4.z);

        float denominator = (b.x - a.x) * (d.y - c.y) - (b.y - a.y) * (d.x - c.x);
        if (Mathf.Approximately(denominator, 0))
            return false;

        float t = ((c.x - a.x) * (d.y - c.y) - (c.y - a.y) * (d.x - c.x)) / denominator;
        float u = ((c.x - a.x) * (b.y - a.y) - (c.y - a.y) * (b.x - a.x)) / denominator;

        return t >= 0 && t <= 1 && u >= 0 && u <= 1;
    }

    void Start()
    {
        EventBus.OnSetMap += SetMap;
    }

    void Update()
    {
    }

    public void SetMap(List<Wall> outline)
    {
        Graph navmesh = MakeNavMesh(outline);
        if (navmesh != null)
        {
            Debug.Log("got navmesh: " + navmesh.all_nodes.Count);
            EventBus.SetGraph(navmesh);
        }
    }
}
