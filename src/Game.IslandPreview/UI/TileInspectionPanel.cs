using Game.Simulation.World.Island;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.IslandPreview.UI;

public sealed class TileInspectionPanel
{
    private const int Padding = 10;

    private IslandPlan? _inspectedPlan;
    private int _inspectedX = -1;
    private int _inspectedY = -1;

    public (int X, int Y)? SelectedTile =>
        _inspectedPlan is not null && _inspectedX >= 0 && _inspectedY >= 0
            ? (_inspectedX, _inspectedY)
            : null;

    public void SetTileInspection(IslandPlan plan, int x, int y)
    {
        if (!plan.Contains(x, y))
        {
            return;
        }

        _inspectedPlan = plan;
        _inspectedX = x;
        _inspectedY = y;
    }

    public void ClearTileInspection()
    {
        _inspectedPlan = null;
        _inspectedX = -1;
        _inspectedY = -1;
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, int viewportWidth, int viewportHeight)
    {
        int panelX = viewportWidth - PreviewLayout.RightSidebarWidth;
        var background = new Color(0x12, 0x12, 0x1E);
        var border = new Color(0x33, 0x33, 0x55);
        var labelColor = new Color(0xCC, 0xCC, 0xDD);
        var headerColor = new Color(0x88, 0xAA, 0xFF);

        spriteBatch.Draw(pixel, new Rectangle(panelX, 0, PreviewLayout.RightSidebarWidth, viewportHeight), background);
        spriteBatch.Draw(pixel, new Rectangle(panelX, 0, 1, viewportHeight), border);
        spriteBatch.DrawString(font, "Tile Inspector", new Vector2(panelX + Padding, Padding + 4), headerColor);

        if (_inspectedPlan is null || _inspectedX < 0 || _inspectedY < 0)
        {
            spriteBatch.DrawString(
                font,
                "Click a map tile to inspect.",
                new Vector2(panelX + Padding, Padding + 30),
                labelColor);
            return;
        }

        ref IslandCellData cell = ref _inspectedPlan.GetCell(_inspectedX, _inspectedY);
        int index = _inspectedY * _inspectedPlan.Width + _inspectedX;
        float coastDistance = _inspectedPlan.CoastDistance.Length > index ? _inspectedPlan.CoastDistance[index] : 0f;
        float concavity = _inspectedPlan.Concavity.Length > index ? _inspectedPlan.Concavity[index] : 0f;
        float coastalVariation = _inspectedPlan.CoastalWidthVariation.Length > index
            ? _inspectedPlan.CoastalWidthVariation[index]
            : 0f;
        float beachWidth = _inspectedPlan.BeachWidth.Length > index ? _inspectedPlan.BeachWidth[index] : 0f;
        float shallowWidth = _inspectedPlan.ShallowWaterWidth.Length > index
            ? _inspectedPlan.ShallowWaterWidth[index]
            : 0f;
        float mask = _inspectedPlan.IslandMask.Length > index ? _inspectedPlan.IslandMask[index] : 0f;
        int regionId = _inspectedPlan.GetRegionId(_inspectedX, _inspectedY);

        string[] lines =
        [
            $"Tile ({_inspectedX}, {_inspectedY})",
            $"Biome: {cell.Biome}",
            $"Land: {cell.IsLand}  Coast: {cell.IsCoast}",
            $"Elev {cell.Elevation:0.###}",
            $"Moist {cell.Moisture:0.###}",
            $"Temp {cell.Temperature:0.###}",
            $"Volc {cell.VolcanicActivity:0.###}",
            $"CoastDist {coastDistance:0.###}",
            $"Concavity {concavity:0.###}",
            $"CoastVar {coastalVariation:0.###}",
            $"Beach/Shallow {beachWidth:0.###}/{shallowWidth:0.###}",
            $"Region {regionId}",
            $"Role {cell.Role}",
            $"Boundary {cell.BoundaryType}",
            $"Mask {mask:0.###}",
        ];

        int y = Padding + 28;
        foreach (string line in lines)
        {
            spriteBatch.DrawString(font, line, new Vector2(panelX + Padding, y), Color.White);
            y += 18;
        }
    }
}
