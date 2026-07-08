using Game.Content.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game.IslandPreview.UI;

public sealed class GenerationParameterPanel
{
    public const int SidebarWidth = 380;
    private const int Padding = 10;
    private const int RowHeight = 22;
    private const int GroupHeaderHeight = 24;
    private const int ButtonHeight = 28;
    private const int SeedRowHeight = 24;
    private const int HeaderHeight = SeedRowHeight + ButtonHeight + Padding + 8;

    private readonly IslandDefinition _defaultIsland;
    private readonly BiomeRulesDefinition _defaultBiomeRules;
    private readonly IslandDefinition _island;
    private readonly BiomeRulesDefinition _biomeRules;
    private readonly Dictionary<string, NumericTextField> _fields = new(StringComparer.Ordinal);
    private readonly NumericTextField _seedField = new();
    private readonly List<(string Group, int Y)> _groupHeaders = [];

    private int _scrollOffset;
    private int _contentHeight;
    private bool _generatePressed;
    private bool _randomizeSeedPressed;
    private bool _resetPressed;
    private bool _isGenerating;
    private string _statusText = string.Empty;
    private NumericTextField? _focusedField;

    public ulong Seed { get; private set; }
    public bool GenerateRequested => _generatePressed;
    public bool IsGenerating => _isGenerating;

    public GenerationParameterPanel(IslandDefinition defaultIsland, BiomeRulesDefinition defaultBiomeRules, ulong? initialSeed)
    {
        _defaultIsland = ParameterFieldRegistry.CloneIsland(defaultIsland);
        _defaultBiomeRules = ParameterFieldRegistry.CloneBiomeRules(defaultBiomeRules);
        _island = ParameterFieldRegistry.CloneIsland(defaultIsland);
        _biomeRules = ParameterFieldRegistry.CloneBiomeRules(defaultBiomeRules);
        Seed = initialSeed ?? (ulong)Random.Shared.NextInt64(1, long.MaxValue);

        foreach (ParameterFieldDescriptor field in ParameterFieldRegistry.All)
        {
            object value = field.Source == ParameterFieldSource.Island
                ? field.Property.GetValue(_island)!
                : field.Property.GetValue(_biomeRules)!;
            var textField = new NumericTextField();
            textField.SetValue(FormatValue(field.Kind, value));
            _fields[field.Name] = textField;
        }

        _seedField.SetValue(Seed.ToString());
        RebuildLayout();
    }

    public IslandDefinition CloneIslandDefinition()
    {
        CommitFocusedField();
        return ParameterFieldRegistry.CloneIsland(_island);
    }

    public BiomeRulesDefinition CloneBiomeRules()
    {
        CommitFocusedField();
        return ParameterFieldRegistry.CloneBiomeRules(_biomeRules);
    }

    public void SetGenerating(bool generating, string statusText = "")
    {
        _isGenerating = generating;
        _statusText = statusText;
    }

    public void ClearGenerateRequest()
    {
        _generatePressed = false;
    }

    public void Update(
        MouseState mouse,
        MouseState previousMouse,
        KeyboardState keyboard,
        KeyboardState previousKeyboard,
        int viewportHeight)
    {
        if (_randomizeSeedPressed)
        {
            _randomizeSeedPressed = false;
            Seed = (ulong)Random.Shared.NextInt64(1, long.MaxValue);
            _seedField.SetValue(Seed.ToString());
        }

        if (_resetPressed)
        {
            _resetPressed = false;
            ResetToDefaults();
        }

        bool leftPressed = mouse.LeftButton == ButtonState.Pressed;
        bool leftClicked = leftPressed && previousMouse.LeftButton == ButtonState.Released;

        if (leftClicked)
        {
            HandleClick(mouse.X, mouse.Y, viewportHeight);
        }

        if (previousKeyboard.IsKeyDown(Keys.Back) && keyboard.IsKeyUp(Keys.Back))
        {
            _focusedField?.HandleBackspace();
            if (_focusedField == _seedField)
            {
                TryParseSeedFromField();
            }
        }

        if (previousKeyboard.IsKeyDown(Keys.Enter) && keyboard.IsKeyUp(Keys.Enter))
        {
            CommitFocusedField();
            _focusedField?.Blur();
            _focusedField = null;
        }

        int wheelDelta = mouse.ScrollWheelValue - previousMouse.ScrollWheelValue;
        if (wheelDelta != 0 && mouse.X < SidebarWidth)
        {
            _scrollOffset = Math.Clamp(_scrollOffset - wheelDelta / 120 * RowHeight, 0, Math.Max(0, _contentHeight - (viewportHeight - HeaderHeight)));
        }
    }

    public void HandleTextInput(char character)
    {
        _focusedField?.HandleTextInput(character);
        if (_focusedField == _seedField)
        {
            TryParseSeedFromField();
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, int viewportHeight)
    {
        var background = new Color(0x12, 0x12, 0x1E);
        var border = new Color(0x33, 0x33, 0x55);
        var labelColor = new Color(0xCC, 0xCC, 0xDD);
        var valueBackground = new Color(0x22, 0x22, 0x33);
        var focusedBackground = new Color(0x2E, 0x3A, 0x55);
        var headerColor = new Color(0x88, 0xAA, 0xFF);
        var buttonColor = new Color(0x2A, 0x4A, 0x7A);
        var buttonHover = new Color(0x3A, 0x5A, 0x9A);
        var disabledColor = new Color(0x44, 0x44, 0x55);

        spriteBatch.Draw(pixel, new Rectangle(0, 0, SidebarWidth, viewportHeight), background);
        spriteBatch.Draw(pixel, new Rectangle(SidebarWidth - 1, 0, 1, viewportHeight), border);

        var mouse = Mouse.GetState();
        int y = Padding;

        spriteBatch.DrawString(font, "Seed", new Vector2(Padding, y), labelColor);
        DrawField(spriteBatch, pixel, font, _seedField, Padding + 44, y, SidebarWidth - Padding * 2 - 44, valueBackground, focusedBackground);
        y += SeedRowHeight + 4;

        DrawButton(spriteBatch, pixel, font, "Randomize", Padding, y, 110, ButtonHeight, buttonColor, buttonHover, mouse, disabled: _isGenerating);
        DrawButton(spriteBatch, pixel, font, "Generate", Padding + 118, y, 110, ButtonHeight, buttonColor, buttonHover, mouse, disabled: _isGenerating);
        DrawButton(spriteBatch, pixel, font, "Reset", Padding + 236, y, 110, ButtonHeight, buttonColor, buttonHover, mouse, disabled: _isGenerating);
        y += ButtonHeight + 8;

        if (!string.IsNullOrEmpty(_statusText))
        {
            spriteBatch.DrawString(font, _statusText, new Vector2(Padding, y), Color.Gold);
            y += 18;
        }

        int listTop = HeaderHeight;
        int clipHeight = viewportHeight - listTop;
        string? currentGroup = null;

        foreach (ParameterFieldDescriptor field in ParameterFieldRegistry.All)
        {
            if (field.Group != currentGroup)
            {
                currentGroup = field.Group;
                int headerY = listTop + GetGroupOffset(currentGroup) - _scrollOffset;
                if (headerY + GroupHeaderHeight >= listTop && headerY <= viewportHeight)
                {
                    spriteBatch.DrawString(font, currentGroup, new Vector2(Padding, headerY), headerColor);
                }
            }

            int rowY = listTop + GetFieldOffset(field) - _scrollOffset;
            if (rowY + RowHeight < listTop || rowY > viewportHeight)
            {
                continue;
            }

            spriteBatch.DrawString(font, field.Label, new Vector2(Padding, rowY + 2), labelColor);
            int valueX = SidebarWidth - Padding - 110;
            DrawField(spriteBatch, pixel, font, _fields[field.Name], valueX, rowY, 110, valueBackground, focusedBackground);
        }
    }

    public bool IsPointInSidebar(int x, int y)
    {
        return x < SidebarWidth;
    }

    private void DrawField(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        NumericTextField field,
        int x,
        int y,
        int width,
        Color background,
        Color focusedBackground)
    {
        field.SetBounds(new Rectangle(x, y, width, RowHeight - 2));
        Color fill = field.IsFocused ? focusedBackground : background;
        spriteBatch.Draw(pixel, field.Bounds, fill);
        spriteBatch.DrawString(font, field.GetText(), new Vector2(x + 4, y + 2), Color.White);
    }

    private void DrawButton(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        string label,
        int x,
        int y,
        int width,
        int height,
        Color color,
        Color hoverColor,
        MouseState mouse,
        bool disabled)
    {
        var bounds = new Rectangle(x, y, width, height);
        bool hovered = !disabled && bounds.Contains(mouse.X, mouse.Y);
        spriteBatch.Draw(pixel, bounds, disabled ? new Color(0x33, 0x33, 0x44) : hovered ? hoverColor : color);
        Vector2 size = font.MeasureString(label);
        spriteBatch.DrawString(
            font,
            label,
            new Vector2(x + (width - size.X) / 2f, y + (height - size.Y) / 2f),
            disabled ? new Color(0x88, 0x88, 0x99) : Color.White);
    }

    private void HandleClick(int mouseX, int mouseY, int viewportHeight)
    {
        if (mouseX >= SidebarWidth)
        {
            return;
        }

        int buttonY = Padding + SeedRowHeight + 4;
        if (new Rectangle(Padding, buttonY, 110, ButtonHeight).Contains(mouseX, mouseY) && !_isGenerating)
        {
            _randomizeSeedPressed = true;
            return;
        }

        if (new Rectangle(Padding + 118, buttonY, 110, ButtonHeight).Contains(mouseX, mouseY) && !_isGenerating)
        {
            CommitFocusedField();
            _generatePressed = true;
            return;
        }

        if (new Rectangle(Padding + 236, buttonY, 110, ButtonHeight).Contains(mouseX, mouseY) && !_isGenerating)
        {
            _resetPressed = true;
            return;
        }

        int seedY = Padding;
        if (new Rectangle(Padding + 44, seedY, SidebarWidth - Padding * 2 - 44, RowHeight - 2).Contains(mouseX, mouseY))
        {
            FocusField(_seedField);
            return;
        }

        if (mouseY < HeaderHeight)
        {
            BlurField();
            return;
        }

        foreach (ParameterFieldDescriptor field in ParameterFieldRegistry.All)
        {
            int rowY = HeaderHeight + GetFieldOffset(field) - _scrollOffset;
            int valueX = SidebarWidth - Padding - 110;
            var bounds = new Rectangle(valueX, rowY, 110, RowHeight - 2);
            if (!bounds.Contains(mouseX, mouseY))
            {
                continue;
            }

            if (field.Kind == ParameterFieldKind.Bool)
            {
                ToggleBool(field);
                return;
            }

            FocusField(_fields[field.Name]);
            return;
        }

        BlurField();
    }

    private void FocusField(NumericTextField field)
    {
        _focusedField?.Blur();
        _focusedField = field;
        field.Focus();
    }

    private void BlurField()
    {
        CommitFocusedField();
        _focusedField?.Blur();
        _focusedField = null;
    }

    private void CommitFocusedField()
    {
        if (_focusedField is null)
        {
            return;
        }

        if (_focusedField == _seedField)
        {
            TryParseSeedFromField();
            return;
        }

        ParameterFieldDescriptor? descriptor = ParameterFieldRegistry.All
            .FirstOrDefault(field => _fields[field.Name] == _focusedField);
        if (descriptor is null)
        {
            return;
        }

        object target = descriptor.Source == ParameterFieldSource.Island ? _island : _biomeRules;
        if (descriptor.Kind == ParameterFieldKind.Int && _focusedField.TryParseInt(out int intValue))
        {
            descriptor.Property.SetValue(target, intValue);
            _focusedField.SetValue(intValue.ToString());
        }
        else if (descriptor.Kind == ParameterFieldKind.Float && _focusedField.TryParseFloat(out float floatValue))
        {
            descriptor.Property.SetValue(target, floatValue);
            _focusedField.SetValue(FormatValue(descriptor.Kind, floatValue));
        }
    }

    private void ToggleBool(ParameterFieldDescriptor field)
    {
        object target = field.Source == ParameterFieldSource.Island ? _island : _biomeRules;
        bool current = (bool)field.Property.GetValue(target)!;
        bool next = !current;
        field.Property.SetValue(target, next);
        _fields[field.Name].SetValue(FormatValue(field.Kind, next));
    }

    private void TryParseSeedFromField()
    {
        if (_seedField.TryParseULong(out ulong parsed) && parsed > 0)
        {
            Seed = parsed;
        }
    }

    private void ResetToDefaults()
    {
        CopyProperties(_defaultIsland, _island);
        CopyProperties(_defaultBiomeRules, _biomeRules);
        foreach (ParameterFieldDescriptor field in ParameterFieldRegistry.All)
        {
            object value = field.Source == ParameterFieldSource.Island
                ? field.Property.GetValue(_island)!
                : field.Property.GetValue(_biomeRules)!;
            _fields[field.Name].SetValue(FormatValue(field.Kind, value));
        }

        Seed = (ulong)Random.Shared.NextInt64(1, long.MaxValue);
        _seedField.SetValue(Seed.ToString());
    }

    private static void CopyProperties<T>(T source, T target)
    {
        foreach (var property in typeof(T).GetProperties())
        {
            if (property.CanRead && property.CanWrite)
            {
                property.SetValue(target, property.GetValue(source));
            }
        }
    }

    private void RebuildLayout()
    {
        _groupHeaders.Clear();
        string? currentGroup = null;
        int y = 0;
        foreach (ParameterFieldDescriptor field in ParameterFieldRegistry.All)
        {
            if (field.Group != currentGroup)
            {
                currentGroup = field.Group;
                _groupHeaders.Add((currentGroup, y));
                y += GroupHeaderHeight;
            }

            y += RowHeight;
        }

        _contentHeight = y;
    }

    private int GetGroupOffset(string group)
    {
        return _groupHeaders.First(header => header.Group == group).Y;
    }

    private int GetFieldOffset(ParameterFieldDescriptor field)
    {
        int offset = 0;
        string? currentGroup = null;
        foreach (ParameterFieldDescriptor candidate in ParameterFieldRegistry.All)
        {
            if (candidate.Group != currentGroup)
            {
                currentGroup = candidate.Group;
                offset += GroupHeaderHeight;
            }

            if (candidate.Name == field.Name)
            {
                return offset;
            }

            offset += RowHeight;
        }

        return offset;
    }

    private static string FormatValue(ParameterFieldKind kind, object value)
    {
        return kind switch
        {
            ParameterFieldKind.Bool => (bool)value ? "true" : "false",
            ParameterFieldKind.Float => ((float)value).ToString("0.####"),
            _ => value.ToString() ?? string.Empty,
        };
    }
}
