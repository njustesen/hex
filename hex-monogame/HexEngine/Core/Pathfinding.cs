using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using HexEngine.Maps;
using HexEngine.Tiles;

namespace HexEngine.Core;

public static class Pathfinding
{
    public static bool CanTraverse(Tile from, Tile to, int edge, GridMap map, bool isFlying)
    {
        if (isFlying)
            return true;

        if (from.Elevation == to.Elevation)
            return true;

        return from.Ramps.Contains(edge);
    }

    public static Dictionary<Tile, int> GetReachableTiles(GridMap map, Tile start, int maxCost, bool isFlying)
    {
        var result = new Dictionary<Tile, int>();
        var visited = new Dictionary<Tile, int> { { start, 0 } };
        var frontier = new PriorityQueue<Tile, int>();
        frontier.Enqueue(start, 0);

        int edgeCount = map.EdgeCount;

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            int currentCost = visited[current];

            for (int e = 0; e < edgeCount; e++)
            {
                var neighbor = map.GetNeighbor(current, e);
                if (neighbor == null) continue;

                if (!CanTraverse(current, neighbor, e, map, isFlying))
                    continue;

                int newCost = currentCost + 1;
                if (newCost > maxCost) continue;

                if (!visited.ContainsKey(neighbor) || newCost < visited[neighbor])
                {
                    visited[neighbor] = newCost;
                    frontier.Enqueue(neighbor, newCost);
                }
            }
        }

        // Exclude start tile and occupied tiles from result
        foreach (var kvp in visited)
        {
            if (kvp.Key == start) continue;
            if (kvp.Key.Unit != null) continue;
            result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    public static List<Tile>? FindPath(GridMap map, Tile start, Tile goal, bool isFlying)
    {
        if (goal.Unit != null) return null;
        if (start == goal) return null;

        int edgeCount = map.EdgeCount;

        var cameFrom = new Dictionary<Tile, Tile>();
        var gScore = new Dictionary<Tile, int> { { start, 0 } };
        var frontier = new PriorityQueue<Tile, float>();
        frontier.Enqueue(start, 0f);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();

            if (current == goal)
            {
                // Reconstruct path
                var path = new List<Tile> { current };
                while (cameFrom.ContainsKey(current))
                {
                    current = cameFrom[current];
                    path.Add(current);
                }
                path.Reverse();
                return path;
            }

            int currentG = gScore[current];

            for (int e = 0; e < edgeCount; e++)
            {
                var neighbor = map.GetNeighbor(current, e);
                if (neighbor == null) continue;

                if (!CanTraverse(current, neighbor, e, map, isFlying))
                    continue;

                int newG = currentG + 1;

                if (!gScore.ContainsKey(neighbor) || newG < gScore[neighbor])
                {
                    gScore[neighbor] = newG;
                    cameFrom[neighbor] = current;
                    float h = Heuristic(neighbor, goal);
                    frontier.Enqueue(neighbor, newG + h);
                }
            }
        }

        return null;
    }

    public static HashSet<Tile> GetTilesInRange(GridMap map, Tile center, int range)
    {
        var result = new HashSet<Tile>();
        var visited = new HashSet<Tile> { center };
        var frontier = new Queue<(Tile tile, int dist)>();
        frontier.Enqueue((center, 0));
        int edgeCount = map.EdgeCount;

        while (frontier.Count > 0)
        {
            var (current, dist) = frontier.Dequeue();
            if (dist >= range) continue;

            for (int e = 0; e < edgeCount; e++)
            {
                var neighbor = map.GetNeighbor(current, e);
                if (neighbor == null || visited.Contains(neighbor)) continue;
                visited.Add(neighbor);
                result.Add(neighbor);
                frontier.Enqueue((neighbor, dist + 1));
            }
        }

        return result;
    }

    private static float Heuristic(Tile a, Tile b)
    {
        float dx = a.Pos.X - b.Pos.X;
        float dy = a.Pos.Y - b.Pos.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        float tileSize = MathF.Max(a.Width, a.Height);
        return tileSize > 0 ? dist / tileSize : dist;
    }
}
