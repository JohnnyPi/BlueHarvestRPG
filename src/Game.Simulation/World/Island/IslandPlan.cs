namespace Game.Simulation.World.Island;

using Game.Simulation.Coordinates;

public sealed class IslandPlan
{
    public IslandPlan(int width, int height, ulong seed)
    {
        Width = width;
        Height = height;
        Seed = seed;
        Cells = new IslandCellData[width * height];
        RegionIds = new int[width * height];

        for (int i = 0; i < RegionIds.Length; i++)
        {
            RegionIds[i] = -1;
        }
    }

    public int Width { get; }
    public int Height { get; }
    public ulong Seed { get; }

    public IslandCellData[] Cells { get; }
    public int[] RegionIds { get; }
    public float[] IslandMask { get; set; } = [];
    public float[] CoastDistance { get; set; } = [];
    public float[] Concavity { get; set; } = [];
    public float[] CoastalWidthVariation { get; set; } = [];
    public float[] BeachWidth { get; set; } = [];
    public float[] ShallowWaterWidth { get; set; } = [];
    public bool[] ExteriorOcean { get; set; } = [];
    public float[] VoronoiF1 { get; set; } = [];
    public float[] VoronoiF2 { get; set; } = [];
    public float[] VoronoiEdge { get; set; } = [];
    public int[] VoronoiBlendRegionIds { get; set; } = [];
    public float[] VoronoiBlendWeights { get; set; } = [];
    public float[] Slope { get; set; } = [];
    public float[] Aspect { get; set; } = [];
    public float[] Curvature { get; set; } = [];
    public float[] Drainage { get; set; } = [];
    public float[] RiverInfluence { get; set; } = [];
    public float[] WaveExposure { get; set; } = [];
    public bool[] IsRiverCell { get; set; } = [];
    public float[] BiomeDepth { get; set; } = [];
    public CoastalLandform[] CoastalLandforms { get; set; } = [];
    public List<IslandGenerationSnapshot> GenerationSnapshots { get; set; } = [];
    public bool OceanFrameValidated { get; set; }
    public IslandGenerationDiagnostics GenerationDiagnostics { get; set; } = new();
    public List<IslandRegion> Regions { get; } = [];
    public List<StructurePlacement> Structures { get; } = [];
    public List<FenceRing> FenceRings { get; } = [];
    public TunnelGraph TunnelGraph { get; } = new();
    public List<RuinSite> RuinSites { get; } = [];
    public List<PlateBoundarySegment> PlateBoundaries { get; } = [];
    public List<VolcanicSite> VolcanicSites { get; } = [];
    public VolcanoExclusionModel VolcanoExclusion { get; } = new();
    public LavaFlowGraph LavaFlowGraph { get; } = new();
    public FacilityRoadGraph RoadGraph { get; } = new();
    public FacilityRiverGraph RiverGraph { get; } = new();

    public int VisitorCenterRegionId { get; set; } = -1;
    public WorldCoord VisitorCenterCell { get; set; } = new(-1, -1);

    public ref IslandCellData GetCell(int x, int y)
    {
        return ref Cells[y * Width + x];
    }

    public ref IslandCellData GetCell(WorldCoord coord)
    {
        return ref GetCell(coord.X, coord.Y);
    }

    public int GetRegionId(int x, int y)
    {
        return RegionIds[y * Width + x];
    }

    public bool IsLand(int x, int y)
    {
        return GetCell(x, y).IsLand;
    }

    public bool Contains(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }
}

public sealed class IslandGenerationDiagnostics
{
    public List<float> AttemptedShapeScales { get; set; } = [];
    public float SelectedShapeScale { get; set; }
    public int CropOffsetX { get; set; }
    public int CropOffsetY { get; set; }
    public float CroppedLandCoverage { get; set; }
    public int LandFrameViolations { get; set; }
    public int CoastFrameViolations { get; set; }
    public int MaxAxisAlignedCoastRun { get; set; }
    public bool OceanFramePassed { get; set; }
    public float MinObservedBeachWidth { get; set; }
    public float MaxObservedBeachWidth { get; set; }
    public float MinObservedShallowWaterWidth { get; set; }
    public float MaxObservedShallowWaterWidth { get; set; }
}
