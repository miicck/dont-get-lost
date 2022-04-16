using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class dungeon : MonoBehaviour
{
    class node
    {
        public int x { get; private set; }
        public int y { get; private set; }
        public int z { get; private set; }

        public Vector3 vector => new Vector3(x, y, z);

        public node(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static bool operator <(node lhs, node rhs)
        {
            if (lhs.x < rhs.x) return true;
            if (lhs.y < rhs.y) return true;
            if (lhs.z < rhs.z) return true;
            return false;
        }

        public static bool operator >(node lhs, node rhs) => !(lhs == rhs || lhs < rhs);

        public override bool Equals(object obj)
        {
            if (obj is node)
            {
                var n = obj as node;
                return x == n.x && y == n.y && z == n.z;
            }
            return false;
        }

        public override int GetHashCode() => x ^ y ^ z;

        public static bool operator ==(node lhs, node rhs) => lhs.Equals(rhs);
        public static bool operator !=(node lhs, node rhs) => !lhs.Equals(rhs);
    }

    class link
    {
        public node from { get; private set; }
        public node to { get; private set; }

        private link(node from, node to)
        {
            this.from = from;
            this.to = to;
        }

        public static link create(node a, node b)
        {
            if (a > b)
                return create(b, a);
            return new link(a, b);
        }

        public override bool Equals(object obj)
        {
            if (obj is link)
            {
                var l = obj as link;
                return l.from == from && l.to == to;
            }
            return false;
        }

        public override int GetHashCode() => from.GetHashCode() ^ to.GetHashCode();
    }

    HashSet<link> self_avoiding_walk = new HashSet<link>();

    bool in_range(bool[,,] grid, int x, int y, int z)
    {
        if (x < 0 || x >= grid.GetLength(0)) return false;
        if (y < 0 || y >= grid.GetLength(1)) return false;
        if (z < 0 || z >= grid.GetLength(2)) return false;
        return true;
    }

    private void Start()
    {
        int size = 64;
        bool[,,] grid = new bool[size, size, size];

        int x = size / 2;
        int y = size / 2;
        int z = size / 2;

        grid[x, y, z] = true;

        int[] dxs = { -1, 1, 0, 0, 0, 0 };
        int[] dys = { 0, 0, -1, 1, 0, 0 };
        int[] dzs = { 0, 0, 0, 0, -1, 1 };

        for (int n = 0; n < 10000; ++n)
        {
            int m = Random.Range(0, dxs.Length);
            int dx = dxs[m];
            int dy = dys[m];
            int dz = dzs[m];

            if (Random.Range(0, 100) == 0)
            {
                dx = size / 2 - x;
                dy = size / 2 - y;
                dz = size / 2 - z;

                if (dx != 0) dy = dz = 0;
                else if (dy != 0) dx = dz = 0;
                else if (dz != 0) dx = dy = 0;
            }

            if (!in_range(grid, x + dx, y + dy, z + dz))
                continue;
            if (grid[x + dx, y + dy, z + dz])
                continue;

            self_avoiding_walk.Add(link.create(new node(x, y, z), new node(x + dx, y + dy, z + dz)));

            x += dx;
            y += dy;
            z += dz;

            grid[x, y, z] = true;
        }
    }

    private void OnDrawGizmos()
    {
        foreach (var l in self_avoiding_walk)
        {
            Gizmos.DrawLine(l.from.vector, l.to.vector);
            Gizmos.DrawCube(l.from.vector, Vector3.one);
        }

    }
}
