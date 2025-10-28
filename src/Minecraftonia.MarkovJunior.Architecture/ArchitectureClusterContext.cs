using System;
using Minecraftonia.WaveFunctionCollapse;

namespace Minecraftonia.MarkovJunior.Architecture;

public readonly struct ArchitectureClusterContext
{
    public ArchitectureClusterContext(SettlementCluster cluster, int tileSizeX, int tileSizeZ)
    {
        Cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        TileSizeX = tileSizeX;
        TileSizeZ = tileSizeZ;
        TileCountX = cluster.Width;
        TileCountZ = cluster.Depth;

        if (Cluster.Area == 0 || TileCountX == 0 || TileCountZ == 0)
        {
            LayoutWidth = 0;
            LayoutDepth = 0;
            OriginGridX = Cluster.MinX;
            OriginGridZ = Cluster.MinZ;
            TileMask = new bool[0, 0];
            return;
        }

        LayoutWidth = TileCountX * TileSizeX;
        LayoutDepth = TileCountZ * TileSizeZ;
        OriginGridX = Cluster.MinX;
        OriginGridZ = Cluster.MinZ;
        TileMask = new bool[TileCountX, TileCountZ];

        foreach (var (x, z) in cluster.Cells)
        {
            int localX = x - cluster.MinX;
            int localZ = z - cluster.MinZ;
            if (localX < 0 || localZ < 0 || localX >= TileCountX || localZ >= TileCountZ)
            {
                continue;
            }

            TileMask[localX, localZ] = true;
        }
    }

    public SettlementCluster Cluster { get; }
    public int TileSizeX { get; }
    public int TileSizeZ { get; }
    public int TileCountX { get; }
    public int TileCountZ { get; }
    public int LayoutWidth { get; }
    public int LayoutDepth { get; }
    public int OriginGridX { get; }
    public int OriginGridZ { get; }
    public bool[,] TileMask { get; }

    public bool IsTileOccupied(int tileX, int tileZ)
    {
        if (TileMask.GetLength(0) == 0 || TileMask.GetLength(1) == 0)
        {
            return false;
        }

        if (tileX < 0 || tileZ < 0 || tileX >= TileMask.GetLength(0) || tileZ >= TileMask.GetLength(1))
        {
            return false;
        }

        return TileMask[tileX, tileZ];
    }

    public bool IsInsideCluster(int localX, int localZ)
    {
        if (LayoutWidth == 0 || LayoutDepth == 0)
        {
            return false;
        }

        if (localX < 0 || localZ < 0 || localX >= LayoutWidth || localZ >= LayoutDepth)
        {
            return false;
        }

        int tileX = localX / TileSizeX;
        int tileZ = localZ / TileSizeZ;
        return IsTileOccupied(tileX, tileZ);
    }
}
