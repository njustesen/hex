using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace HexEngine;

public class DebugMenu
{
    public bool Visible { get; set; }

    private readonly List<MenuItem> _items = new();
    private int _selectedIndex;
    private KeyboardState _prevKeyState;

    public DebugMenu()
    {
        _items.Add(new FloatItem("Depth Multiplier", () => EngineConfig.DepthMultiplier, v => EngineConfig.DepthMultiplier = v, 0.05f, 0f, 2f));
        _items.Add(new FloatItem("Perspective", () => EngineConfig.PerspectiveFactor, v => EngineConfig.PerspectiveFactor = v, 0.05f, 0f, 1f));
        _items.Add(new BoolItem("Minimap Perspective", () => EngineConfig.MinimapPerspective, v => EngineConfig.MinimapPerspective = v));
        _items.Add(new FloatItem("Move Speed", () => EngineConfig.MoveSpeed, v => EngineConfig.MoveSpeed = v, 0.1f, 0.1f, 5f));
        _items.Add(new FloatItem("Zoom Speed", () => EngineConfig.ZoomSpeed, v => EngineConfig.ZoomSpeed = v, 0.5f, 0.5f, 10f));
        _items.Add(new ColorItem("Tile Top Color", () => EngineConfig.TileTopColor, v => EngineConfig.TileTopColor = v, 10));
    }

    public void Update()
    {
        var keyState = Keyboard.GetState();

        if (KeyPressed(keyState, Keys.F1))
            Visible = !Visible;

        if (!Visible)
        {
            _prevKeyState = keyState;
            return;
        }

        if (KeyPressed(keyState, Keys.Up))
            _selectedIndex = (_selectedIndex - 1 + _items.Count) % _items.Count;
        if (KeyPressed(keyState, Keys.Down))
            _selectedIndex = (_selectedIndex + 1) % _items.Count;
        if (KeyPressed(keyState, Keys.Left))
            _items[_selectedIndex].Decrease();
        if (KeyPressed(keyState, Keys.Right))
            _items[_selectedIndex].Increase();
        if (KeyPressed(keyState, Keys.Enter))
            _items[_selectedIndex].Toggle();

        _prevKeyState = keyState;
    }

    public bool ConsumesArrowKeys => Visible;

    private bool KeyPressed(KeyboardState current, Keys key)
        => current.IsKeyDown(key) && !_prevKeyState.IsKeyDown(key);

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        if (!Visible) return;

        float x = 10;
        float y = 10;
        float lineHeight = font.LineSpacing + 4;
        float padding = 8;

        // Background
        float maxWidth = 0;
        for (int i = 0; i < _items.Count; i++)
        {
            var text = FormatItem(i);
            var size = font.MeasureString(text);
            if (size.X > maxWidth) maxWidth = size.X;
        }

        float bgWidth = maxWidth + padding * 2;
        float bgHeight = _items.Count * lineHeight + padding * 2 + lineHeight;

        // Draw background using spriteBatch (1x1 pixel texture)
        var pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
        pixel.SetData(new[] { new Color(0, 0, 0, 180) });
        spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, (int)bgWidth, (int)bgHeight), Color.White);

        // Title
        spriteBatch.DrawString(font, "[F1] Debug Menu  (Up/Down/Left/Right/Enter)", new Vector2(x + padding, y + padding), Color.Yellow);
        y += lineHeight;

        // Items
        for (int i = 0; i < _items.Count; i++)
        {
            var text = FormatItem(i);
            var color = i == _selectedIndex ? Color.Cyan : Color.White;
            var prefix = i == _selectedIndex ? "> " : "  ";
            spriteBatch.DrawString(font, prefix + text, new Vector2(x + padding, y + padding + i * lineHeight), color);
        }

        pixel.Dispose();
    }

    private string FormatItem(int index)
    {
        return _items[index].Display();
    }

    // --- Menu item types ---

    private abstract class MenuItem
    {
        public string Name { get; }
        protected MenuItem(string name) => Name = name;
        public abstract string Display();
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

        public override string Display() => $"{Name}: {_get():F2}  [{_min:F1} .. {_max:F1}]";
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

        public override string Display() => $"{Name}: {(_get() ? "ON" : "OFF")}";
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

        public override string Display()
        {
            var c = _get();
            string comp = _component switch { 0 => "R", 1 => "G", _ => "B" };
            return $"{Name}: ({c.R},{c.G},{c.B})  [Enter: {comp}]";
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
