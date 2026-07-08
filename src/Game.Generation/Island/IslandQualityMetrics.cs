using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island;

public sealed class IslandQualityReport
{
    public int BiomeSingletonCount { get; init; }
    public int TinyBiomePatchCount { get; init; }
    public int LandCellsInForbiddenEdgeBand { get; init; }
    public int CoastCellsInForbiddenEdgeBand { get; init; }
    public int MaxAxisAlignedCoastRun { get; init; }
    public int MinLandDistanceFromEdge { get; init; }
    public int MinCoastDistanceFromEdge { get; init; }
}

public static class IslandQualityMetrics
{
  private static readonly (int Dx, int Dy)[] EightWay =
  [
      (1, 0), (-1, 0), (0, 1), (0, -1),
      (1, 1), (-1, 1), (1, -1), (-1, -1),
  ];

  public static IslandQualityReport Analyze(
      IslandPlan plan,
      IReadOnlyDictionary<BiomeId, int>? minPatchSizes = null,
      int edgeLandForbiddenBand = 24,
      int edgeCoastForbiddenBand = 16,
      int edgeLinearityBand = 24,
      int maxAxisAlignedCoastRunThreshold = 20)
  {
      minPatchSizes ??= DefaultMinPatchSizes();

      int singletonCount = 0;
      int tinyPatchCount = 0;
      foreach (BiomeId biome in LandBiomes)
      {
          if (IsSpecialBiome(biome))
          {
              continue;
          }

          int minSize = minPatchSizes.TryGetValue(biome, out int configured) ? configured : 12;
          foreach (List<int> component in FindBiomeComponents(plan, biome))
          {
              if (component.Count == 1)
              {
                  singletonCount++;
              }

              if (component.Count < minSize)
              {
                  tinyPatchCount++;
              }
          }
      }

      int landInBand = 0;
      int coastInBand = 0;
      int minLandDist = int.MaxValue;
      int minCoastDist = int.MaxValue;

      for (int y = 0; y < plan.Height; y++)
      {
          for (int x = 0; x < plan.Width; x++)
          {
              int edgeDist = DistanceToMapEdge(x, y, plan.Width, plan.Height);
              ref IslandCellData cell = ref plan.GetCell(x, y);

              if (cell.IsLand)
              {
                  minLandDist = Math.Min(minLandDist, edgeDist);
                  if (edgeDist < edgeLandForbiddenBand)
                  {
                      landInBand++;
                  }
              }

              if (cell.IsCoast)
              {
                  minCoastDist = Math.Min(minCoastDist, edgeDist);
                  if (edgeDist < edgeCoastForbiddenBand)
                  {
                      coastInBand++;
                  }
              }
          }
      }

      if (minLandDist == int.MaxValue)
      {
          minLandDist = -1;
      }

      if (minCoastDist == int.MaxValue)
      {
          minCoastDist = -1;
      }

      int maxRun = Math.Max(
          MaxAxisAlignedCoastRun(plan, horizontal: true, edgeBand: edgeLinearityBand),
          MaxAxisAlignedCoastRun(plan, horizontal: false, edgeBand: edgeLinearityBand));

      return new IslandQualityReport
      {
          BiomeSingletonCount = singletonCount,
          TinyBiomePatchCount = tinyPatchCount,
          LandCellsInForbiddenEdgeBand = landInBand,
          CoastCellsInForbiddenEdgeBand = coastInBand,
          MaxAxisAlignedCoastRun = maxRun,
          MinLandDistanceFromEdge = minLandDist,
          MinCoastDistanceFromEdge = minCoastDist,
      };
  }

  public static int CountBiomeSingletons(IslandPlan plan, IReadOnlyDictionary<BiomeId, int>? minPatchSizes = null)
      => Analyze(plan, minPatchSizes).BiomeSingletonCount;

  public static int MaxAxisAlignedCoastRun(IslandPlan plan, bool horizontal, int edgeBand = int.MaxValue)
  {
      int maxRun = 0;

      if (horizontal)
      {
          for (int y = 0; y < plan.Height; y++)
          {
              int run = 0;
              for (int x = 0; x < plan.Width; x++)
              {
                  if (IsCoastInEdgeBand(plan, x, y, edgeBand))
                  {
                      run++;
                      maxRun = Math.Max(maxRun, run);
                  }
                  else
                  {
                      run = 0;
                  }
              }
          }
      }
      else
      {
          for (int x = 0; x < plan.Width; x++)
          {
              int run = 0;
              for (int y = 0; y < plan.Height; y++)
              {
                  if (IsCoastInEdgeBand(plan, x, y, edgeBand))
                  {
                      run++;
                      maxRun = Math.Max(maxRun, run);
                  }
                  else
                  {
                      run = 0;
                  }
              }
          }
      }

      return maxRun;
  }

  public static int MinLandDistanceFromEdge(IslandPlan plan)
  {
      int min = int.MaxValue;
      for (int y = 0; y < plan.Height; y++)
      {
          for (int x = 0; x < plan.Width; x++)
          {
              if (plan.IsLand(x, y))
              {
                  min = Math.Min(min, DistanceToMapEdge(x, y, plan.Width, plan.Height));
              }
          }
      }

      return min == int.MaxValue ? -1 : min;
  }

  public static int MinCoastDistanceFromEdge(IslandPlan plan)
  {
      int min = int.MaxValue;
      for (int y = 0; y < plan.Height; y++)
      {
          for (int x = 0; x < plan.Width; x++)
          {
              if (plan.GetCell(x, y).IsCoast)
              {
                  min = Math.Min(min, DistanceToMapEdge(x, y, plan.Width, plan.Height));
              }
          }
      }

      return min == int.MaxValue ? -1 : min;
  }

  public static float[] ComputeDistanceToMapEdgeField(IslandPlan plan)
  {
      var field = new float[plan.Width * plan.Height];
      for (int y = 0; y < plan.Height; y++)
      {
          for (int x = 0; x < plan.Width; x++)
          {
              field[y * plan.Width + x] = DistanceToMapEdge(x, y, plan.Width, plan.Height);
          }
      }

      return field;
  }

  public static float[] ComputeBiomeSingletonHeatmap(IslandPlan plan)
  {
      var field = new float[plan.Width * plan.Height];
      var componentSizeByCell = new int[plan.Width * plan.Height];

      foreach (BiomeId biome in LandBiomes)
      {
          foreach (List<int> component in FindBiomeComponents(plan, biome))
          {
              foreach (int index in component)
              {
                  componentSizeByCell[index] = component.Count;
              }
          }
      }

      for (int i = 0; i < field.Length; i++)
      {
          field[i] = componentSizeByCell[i] == 1 ? 1f : 0f;
      }

      return field;
  }

  public static float[] ComputeCoastLinearityHeatmap(IslandPlan plan, int edgeBand = 24)
  {
      var field = new float[plan.Width * plan.Height];
      int maxHorizontal = MaxAxisAlignedCoastRun(plan, horizontal: true, edgeBand);
      int maxVertical = MaxAxisAlignedCoastRun(plan, horizontal: false, edgeBand);
      float maxRun = Math.Max(1, Math.Max(maxHorizontal, maxVertical));

      for (int y = 0; y < plan.Height; y++)
      {
          for (int x = 0; x < plan.Width; x++)
          {
              if (!IsCoastInEdgeBand(plan, x, y, edgeBand))
              {
                  continue;
              }

              int hRun = CountRun(plan, x, y, horizontal: true);
              int vRun = CountRun(plan, x, y, horizontal: false);
              field[y * plan.Width + x] = Math.Max(hRun, vRun) / maxRun;
          }
      }

      return field;
  }

  public static float[] ComputeStageDiffHeatmap(
      IslandGenerationSnapshot? previous,
      IslandPlan current,
      StageDiffMode mode)
  {
      var field = new float[current.Width * current.Height];
      if (previous is null || previous.IsLand.Length != current.Cells.Length)
      {
          return field;
      }

      for (int i = 0; i < field.Length; i++)
      {
          ref IslandCellData cell = ref current.Cells[i];
          field[i] = mode switch
          {
              StageDiffMode.LandOcean => previous.IsLand[i] != cell.IsLand ? 1f : 0f,
              StageDiffMode.Biome => previous.Biomes[i] != cell.Biome ? 1f : 0f,
              StageDiffMode.Elevation => MathF.Abs(previous.Elevations[i] - cell.Elevation) > 0.05f ? 1f : 0f,
              _ => 0f,
          };
      }

      return field;
  }

  public static Dictionary<BiomeId, int> DefaultMinPatchSizes() => new()
  {
      [BiomeId.Beach] = 2,
      [BiomeId.Plains] = 16,
      [BiomeId.Forest] = 20,
      [BiomeId.Jungle] = 28,
      [BiomeId.Swamp] = 18,
      [BiomeId.Hills] = 14,
      [BiomeId.Mountains] = 10,
      [BiomeId.Volcanic] = 4,
  };

  private static readonly BiomeId[] LandBiomes =
  [
      BiomeId.Beach,
      BiomeId.Plains,
      BiomeId.Forest,
      BiomeId.Jungle,
      BiomeId.Swamp,
      BiomeId.Hills,
      BiomeId.Mountains,
      BiomeId.Volcanic,
  ];

  private static bool IsSpecialBiome(BiomeId biome)
      => biome is BiomeId.Beach or BiomeId.Volcanic;

  private static int DistanceToMapEdge(int x, int y, int width, int height)
      => Math.Min(Math.Min(x, y), Math.Min(width - 1 - x, height - 1 - y));

  private static bool IsCoastInEdgeBand(IslandPlan plan, int x, int y, int edgeBand)
  {
      if (!plan.GetCell(x, y).IsCoast)
      {
          return false;
      }

      return edgeBand >= int.MaxValue / 2
          || DistanceToMapEdge(x, y, plan.Width, plan.Height) <= edgeBand;
  }

  private static int CountRun(IslandPlan plan, int x, int y, bool horizontal)
  {
      if (!plan.GetCell(x, y).IsCoast)
      {
          return 0;
      }

      int run = 1;
      if (horizontal)
      {
          for (int dx = 1; x + dx < plan.Width && plan.GetCell(x + dx, y).IsCoast; dx++)
          {
              run++;
          }

          for (int dx = 1; x - dx >= 0 && plan.GetCell(x - dx, y).IsCoast; dx++)
          {
              run++;
          }
      }
      else
      {
          for (int dy = 1; y + dy < plan.Height && plan.GetCell(x, y + dy).IsCoast; dy++)
          {
              run++;
          }

          for (int dy = 1; y - dy >= 0 && plan.GetCell(x, y - dy).IsCoast; dy++)
          {
              run++;
          }
      }

      return run;
  }

  public static List<List<int>> FindBiomeComponents(IslandPlan plan, BiomeId biome)
  {
      var visited = new bool[plan.Width * plan.Height];
      var components = new List<List<int>>();

      for (int y = 0; y < plan.Height; y++)
      {
          for (int x = 0; x < plan.Width; x++)
          {
              int startIndex = y * plan.Width + x;
              ref IslandCellData cell = ref plan.GetCell(x, y);
              if (visited[startIndex] || !cell.IsLand || cell.Biome != biome)
              {
                  continue;
              }

              var component = new List<int>();
              var queue = new Queue<int>();
              queue.Enqueue(startIndex);
              visited[startIndex] = true;

              while (queue.Count > 0)
              {
                  int index = queue.Dequeue();
                  component.Add(index);

                  int cx = index % plan.Width;
                  int cy = index / plan.Width;
                  foreach ((int dx, int dy) in EightWay)
                  {
                      int nx = cx + dx;
                      int ny = cy + dy;
                      if (!plan.Contains(nx, ny))
                      {
                          continue;
                      }

                      int neighborIndex = ny * plan.Width + nx;
                      if (visited[neighborIndex])
                      {
                          continue;
                      }

                      ref IslandCellData neighbor = ref plan.GetCell(nx, ny);
                      if (!neighbor.IsLand || neighbor.Biome != biome)
                      {
                          continue;
                      }

                      visited[neighborIndex] = true;
                      queue.Enqueue(neighborIndex);
                  }
              }

              components.Add(component);
          }
      }

      return components;
  }
}

public enum StageDiffMode
{
    LandOcean,
    Biome,
    Elevation,
}
