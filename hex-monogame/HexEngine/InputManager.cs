using Microsoft.Xna.Framework.Input;

namespace HexEngine;

public class InputManager
{
    private KeyboardState _prevKeyState;
    private MouseState _prevMouseState;

    public bool MouseDown { get; private set; }
    public bool MouseReleased { get; private set; }
    public (int X, int Y) MousePos { get; private set; }
    public int ZoomDirection { get; private set; }
    public int DirectionX { get; private set; }
    public int DirectionY { get; private set; }
    public int MapModePressed { get; private set; }

    public InputManager()
    {
        _prevKeyState = Keyboard.GetState();
        _prevMouseState = Mouse.GetState();
    }

    public void Update()
    {
        var keyState = Keyboard.GetState();
        var mouseState = Mouse.GetState();

        // Mouse position
        MousePos = (mouseState.X, mouseState.Y);

        // Zoom from scroll wheel
        int scrollDelta = mouseState.ScrollWheelValue - _prevMouseState.ScrollWheelValue;
        if (scrollDelta > 0) ZoomDirection = 1;
        else if (scrollDelta < 0) ZoomDirection = -1;
        else ZoomDirection = 0;

        // Mouse button state
        bool currentMouseDown = mouseState.LeftButton == ButtonState.Pressed;
        MouseReleased = MouseDown && !currentMouseDown;
        MouseDown = currentMouseDown;

        // Movement direction from keys
        DirectionX = 0;
        DirectionY = 0;
        if (keyState.IsKeyDown(Keys.Left) || keyState.IsKeyDown(Keys.A))
            DirectionX -= 1;
        if (keyState.IsKeyDown(Keys.Right) || keyState.IsKeyDown(Keys.D))
            DirectionX += 1;
        if (keyState.IsKeyDown(Keys.Up) || keyState.IsKeyDown(Keys.W))
            DirectionY -= 1;
        if (keyState.IsKeyDown(Keys.Down) || keyState.IsKeyDown(Keys.S))
            DirectionY += 1;

        // Map mode switching (1, 2, 3) - detect key press (not hold)
        MapModePressed = 0;
        if (keyState.IsKeyDown(Keys.D1) && !_prevKeyState.IsKeyDown(Keys.D1))
            MapModePressed = 1;
        else if (keyState.IsKeyDown(Keys.D2) && !_prevKeyState.IsKeyDown(Keys.D2))
            MapModePressed = 2;
        else if (keyState.IsKeyDown(Keys.D3) && !_prevKeyState.IsKeyDown(Keys.D3))
            MapModePressed = 3;

        _prevKeyState = keyState;
        _prevMouseState = mouseState;
    }
}
