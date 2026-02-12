using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using HexEngine.Input;
using HexEngine.Config;

namespace HexEngine.UI;

public class DebugMenu : Panel
{
    private readonly List<MenuItem> _items = new();
    private KeyboardState _prevKeyState;
    private string? _statusMessage;
    private float _statusTimer;

    private const float ButtonWidth = 28f;

    // Cached layout
    private float _menuX, _menuY, _menuWidth, _menuHeight;
    private readonly List<RowLayout> _rowLayouts = new();

    public DebugMenu()
    {
        _items.Add(new FloatItem("Depth Multiplier", () => EngineConfig.DepthMultiplier, v => EngineConfig.DepthMultiplier = v, 0.05f, 0f, 2f));
        _items.Add(new FloatItem("Perspective", () => EngineConfig.PerspectiveFactor, v => EngineConfig.PerspectiveFactor = v, 0.05f, 0f, 1f));
        _items.Add(new BoolItem("Minimap Perspective", () => EngineConfig.MinimapPerspective, v => EngineConfig.MinimapPerspective = v));
        _items.Add(new FloatItem("Move Speed", () => EngineConfig.MoveSpeed, v => EngineConfig.MoveSpeed = v, 0.1f, 0.1f, 5f));
        _items.Add(new FloatItem("Zoom Speed", () => EngineConfig.ZoomSpeed, v => EngineConfig.ZoomSpeed = v, 0.5f, 0.5f, 10f));
        _items.Add(new FloatItem("Fog Strength", () => EngineConfig.FogStrength, v => EngineConfig.FogStrength = v, 0.05f, 0f, 1f));
        _items.Add(new BoolItem("Show Grid", () => EngineConfig.ShowGrid, v => EngineConfig.ShowGrid = v));
        _items.Add(new BoolItem("Inner Shapes", () => EngineConfig.ShowInnerShapes, v => EngineConfig.ShowInnerShapes = v));
        _items.Add(new FloatItem("Inner Scale", () => EngineConfig.InnerShapeScale, v => EngineConfig.InnerShapeScale = v, 0.1f, 0f, 1f));
        _items.Add(new ColorItem("Tile Color", () => EngineConfig.TileTopColor, v => EngineConfig.TileTopColor = v, 10));
    }

    public void Update(InputManager input)
    {
        var keyState = Keyboard.GetState();
        ConsumesClick = false;

        if (keyState.IsKeyDown(Keys.F1) && !_prevKeyState.IsKeyDown(Keys.F1))
            Visible = !Visible;

        if (!Visible)
        {
            _prevKeyState = keyState;
            return;
        }

        // Handle mouse clicks on buttons
        if (input.MouseReleased)
        {
            float mx = input.MousePos.X;
            float my = input.MousePos.Y;

            if (ConsumeIfHit(mx, my, _menuX, _menuY, _menuWidth, _menuHeight))
            {

                foreach (var row in _rowLayouts)
                {
                    if (row.MinusBtn.HasValue && InRect(mx, my, row.MinusBtn.Value))
                    {
                        row.Item.Decrease();
                        break;
                    }
                    if (row.PlusBtn.HasValue && InRect(mx, my, row.PlusBtn.Value))
                    {
                        row.Item.Increase();
                        break;
                    }
                    if (row.ToggleBtn.HasValue && InRect(mx, my, row.ToggleBtn.Value))
                    {
                        row.Item.Toggle();
                        break;
                    }
                }

                if (_saveBtn.HasValue && InRect(mx, my, _saveBtn.Value))
                {
                    EngineConfig.Save();
                    _statusMessage = "Saved";
                    _statusTimer = 2f;
                }
                if (_loadBtn.HasValue && InRect(mx, my, _loadBtn.Value))
                {
                    EngineConfig.Load();
                    _statusMessage = "Loaded";
                    _statusTimer = 2f;
                }
            }
        }

        // Check hover for consuming clicks (prevent click-through)
        if (input.MouseDown)
            ConsumeIfHit(input.MousePos.X, input.MousePos.Y, _menuX, _menuY, _menuWidth, _menuHeight);

        if (_statusTimer > 0)
            _statusTimer -= 1f / 60f;

        _prevKeyState = keyState;
    }

    private Rectangle? _saveBtn;
    private Rectangle? _loadBtn;

    public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, float startY)
    {
        PanelBottom = startY;
        if (!Visible) return;

        float x = 10;
        float y = startY;
        float lineHeight = RowHeight;

        // Compute layout
        _rowLayouts.Clear();

        float maxLabelWidth = 0;
        foreach (var item in _items)
        {
            float w = font.MeasureString(item.DisplayLabel()).X;
            if (w > maxLabelWidth) maxLabelWidth = w;
        }

        float maxValueWidth = 0;
        foreach (var item in _items)
        {
            float w = font.MeasureString(item.DisplayValue()).X;
            if (w > maxValueWidth) maxValueWidth = w;
        }

        float buttonsWidth = ButtonWidth * 2 + BtnGap;
        float totalContentWidth = maxLabelWidth + BtnGap + buttonsWidth + BtnGap + maxValueWidth;

        float saveLoadWidth = font.MeasureString("Save").X + font.MeasureString("Load").X + BtnGap * 3 + Padding * 2;
        float titleWidth = font.MeasureString("Settings").X;
        float menuContentWidth = Math.Max(totalContentWidth, Math.Max(saveLoadWidth, titleWidth));

        float menuWidth = menuContentWidth + Padding * 2;
        int totalRows = 1 + _items.Count + 1;
        if (_statusTimer > 0) totalRows++;
        float menuHeight = totalRows * lineHeight + Padding * 2;

        _menuX = x;
        _menuY = y;
        _menuWidth = menuWidth;
        _menuHeight = menuHeight;
        PanelBottom = y + menuHeight;

        DrawBg(spriteBatch, pixel, x, y, menuWidth, menuHeight);

        float cy = y + Padding;

        // Title
        spriteBatch.DrawString(font, "Settings", new Vector2(x + Padding, cy), Color.Yellow);
        cy += lineHeight;

        // Items
        float btnAreaX = x + Padding + maxLabelWidth + BtnGap;
        float valueX = btnAreaX + ButtonWidth + BtnGap;
        float plusX = valueX + maxValueWidth + BtnGap;

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            float rowY = cy + i * lineHeight;

            spriteBatch.DrawString(font, item.DisplayLabel(), new Vector2(x + Padding, rowY), Color.White);

            var row = new RowLayout { Item = item };

            if (item is BoolItem)
            {
                var toggleRect = new Rectangle((int)btnAreaX, (int)rowY, (int)(ButtonWidth * 2 + BtnGap + maxValueWidth), (int)BtnHeight);
                row.ToggleBtn = toggleRect;
                DrawBtn(spriteBatch, pixel, font, toggleRect, item.DisplayValue(),
                    item.DisplayValue() == "ON" ? new Color(40, 120, 40, 200) : new Color(80, 40, 40, 200));
            }
            else if (item is ColorItem colorItem)
            {
                var minusRect = new Rectangle((int)btnAreaX, (int)rowY, (int)ButtonWidth, (int)BtnHeight);
                var plusRect = new Rectangle((int)plusX, (int)rowY, (int)ButtonWidth, (int)BtnHeight);
                row.MinusBtn = minusRect;
                row.PlusBtn = plusRect;

                DrawBtn(spriteBatch, pixel, font, minusRect, "-", new Color(60, 60, 60, 200));
                spriteBatch.DrawString(font, item.DisplayValue(), new Vector2(valueX, rowY), Color.White);
                DrawBtn(spriteBatch, pixel, font, plusRect, "+", new Color(60, 60, 60, 200));

                var toggleRect = new Rectangle((int)(plusX + ButtonWidth + BtnGap), (int)rowY, (int)ButtonWidth, (int)BtnHeight);
                row.ToggleBtn = toggleRect;
                DrawBtn(spriteBatch, pixel, font, toggleRect, colorItem.ComponentName(), new Color(80, 60, 40, 200));
            }
            else
            {
                var minusRect = new Rectangle((int)btnAreaX, (int)rowY, (int)ButtonWidth, (int)BtnHeight);
                var plusRect = new Rectangle((int)plusX, (int)rowY, (int)ButtonWidth, (int)BtnHeight);
                row.MinusBtn = minusRect;
                row.PlusBtn = plusRect;

                DrawBtn(spriteBatch, pixel, font, minusRect, "-", new Color(60, 60, 60, 200));
                spriteBatch.DrawString(font, item.DisplayValue(), new Vector2(valueX, rowY), Color.White);
                DrawBtn(spriteBatch, pixel, font, plusRect, "+", new Color(60, 60, 60, 200));
            }

            _rowLayouts.Add(row);
        }

        // Save / Load buttons
        float btnRowY = cy + _items.Count * lineHeight;
        float saveBtnW = font.MeasureString("Save").X + Padding;
        float loadBtnW = font.MeasureString("Load").X + Padding;
        _saveBtn = new Rectangle((int)(x + Padding), (int)btnRowY, (int)saveBtnW, (int)BtnHeight);
        _loadBtn = new Rectangle((int)(x + Padding + saveBtnW + BtnGap), (int)btnRowY, (int)loadBtnW, (int)BtnHeight);
        DrawBtn(spriteBatch, pixel, font, _saveBtn.Value, "Save", new Color(40, 80, 40, 200));
        DrawBtn(spriteBatch, pixel, font, _loadBtn.Value, "Load", new Color(40, 40, 80, 200));

        // Status
        if (_statusTimer > 0 && _statusMessage != null)
        {
            float statusY = btnRowY + lineHeight;
            spriteBatch.DrawString(font, _statusMessage, new Vector2(x + Padding, statusY), Color.LimeGreen);
        }
    }

    // --- Layout info ---
    private class RowLayout
    {
        public MenuItem Item = null!;
        public Rectangle? MinusBtn;
        public Rectangle? PlusBtn;
        public Rectangle? ToggleBtn;
    }

    // --- Menu item types ---

    private abstract class MenuItem
    {
        public string Name { get; }
        protected MenuItem(string name) => Name = name;
        public string DisplayLabel() => Name;
        public abstract string DisplayValue();
        public virtual void Increase() { }
        public virtual void Decrease() { }
        public virtual void Toggle() { }
    }

    private class FloatItem : MenuItem
    {
        private readonly Func<float> _get;
        private readonly Action<float> _set;
        private readonly float _step, _min, _max;

        public FloatItem(string name, Func<float> get, Action<float> set, float step, float min, float max)
            : base(name)
        {
            _get = get; _set = set; _step = step; _min = min; _max = max;
        }

        public override string DisplayValue() => $"{_get():F2}";
        public override void Increase() => _set(Math.Min(_max, _get() + _step));
        public override void Decrease() => _set(Math.Max(_min, _get() - _step));
    }

    private class BoolItem : MenuItem
    {
        private readonly Func<bool> _get;
        private readonly Action<bool> _set;

        public BoolItem(string name, Func<bool> get, Action<bool> set) : base(name)
        {
            _get = get; _set = set;
        }

        public override string DisplayValue() => _get() ? "ON" : "OFF";
        public override void Toggle() => _set(!_get());
        public override void Increase() => Toggle();
        public override void Decrease() => Toggle();
    }

    private class ColorItem : MenuItem
    {
        private readonly Func<Color> _get;
        private readonly Action<Color> _set;
        private readonly int _step;
        private int _component; // 0=R, 1=G, 2=B

        public ColorItem(string name, Func<Color> get, Action<Color> set, int step) : base(name)
        {
            _get = get; _set = set; _step = step;
        }

        public string ComponentName() => _component switch { 0 => "R", 1 => "G", _ => "B" };

        public override string DisplayValue()
        {
            var c = _get();
            return $"({c.R},{c.G},{c.B})";
        }

        public override void Toggle()
        {
            _component = (_component + 1) % 3;
        }

        public override void Increase()
        {
            var c = _get();
            int r = c.R, g = c.G, b = c.B;
            if (_component == 0) r = Math.Min(255, r + _step);
            else if (_component == 1) g = Math.Min(255, g + _step);
            else b = Math.Min(255, b + _step);
            _set(new Color(r, g, b));
        }

        public override void Decrease()
        {
            var c = _get();
            int r = c.R, g = c.G, b = c.B;
            if (_component == 0) r = Math.Max(0, r - _step);
            else if (_component == 1) g = Math.Max(0, g - _step);
            else b = Math.Max(0, b - _step);
            _set(new Color(r, g, b));
        }
    }
}
