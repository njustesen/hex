using Microsoft.Xna.Framework.Input;

namespace HexEngine.Input;

public class InputManager
{
    private KeyboardState _prevKeyState;
    private MouseState _prevMouseState;

    public bool MouseDown { get; private set; }
    public bool MouseReleased { get; private set; }
    public bool RightMouseDown { get; private set; }
    public bool RightMouseReleased { get; private set; }
    public (int X, int Y) MousePos { get; private set; }
    public int ZoomDirection { get; private set; }
    public int DirectionX { get; private set; }
    public int DirectionY { get; private set; }
    public int MapModePressed { get; private set; }
    public bool F2Pressed { get; private set; }
    public bool CtrlS { get; private set; }
    public bool RPressed { get; private set; }
    public bool EPressed { get; private set; }
    public bool EnterPressed { get; private set; }
    public bool EscapePressed { get; private set; }
    public bool UpPressed { get; private set; }
    public bool DownPressed { get; private set; }
    public bool LeftPressed { get; private set; }
    public bool RightPressed { get; private set; }
    public bool UPressed { get; private set; }
    public bool TPressed { get; private set; }
    public bool BracketLeftPressed { get; private set; }
    public bool BracketRightPressed { get; private set; }
    public bool GPressed { get; private set; }

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

        // Left mouse button state
        bool currentMouseDown = mouseState.LeftButton == ButtonState.Pressed;
        MouseReleased = MouseDown && !currentMouseDown;
        MouseDown = currentMouseDown;

        // Right mouse button state
        bool currentRightDown = mouseState.RightButton == ButtonState.Pressed;
        RightMouseReleased = RightMouseDown && !currentRightDown;
        RightMouseDown = currentRightDown;

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

        // Editor keys
        F2Pressed = keyState.IsKeyDown(Keys.F2) && !_prevKeyState.IsKeyDown(Keys.F2);
        RPressed = keyState.IsKeyDown(Keys.R) && !_prevKeyState.IsKeyDown(Keys.R);
        EPressed = keyState.IsKeyDown(Keys.E) && !_prevKeyState.IsKeyDown(Keys.E);
        UPressed = keyState.IsKeyDown(Keys.U) && !_prevKeyState.IsKeyDown(Keys.U);

        // Ctrl+S
        CtrlS = (keyState.IsKeyDown(Keys.LeftControl) || keyState.IsKeyDown(Keys.RightControl))
                && keyState.IsKeyDown(Keys.S) && !_prevKeyState.IsKeyDown(Keys.S);

        // Menu navigation keys (press detection)
        EnterPressed = keyState.IsKeyDown(Keys.Enter) && !_prevKeyState.IsKeyDown(Keys.Enter);
        EscapePressed = keyState.IsKeyDown(Keys.Escape) && !_prevKeyState.IsKeyDown(Keys.Escape);
        UpPressed = keyState.IsKeyDown(Keys.Up) && !_prevKeyState.IsKeyDown(Keys.Up);
        DownPressed = keyState.IsKeyDown(Keys.Down) && !_prevKeyState.IsKeyDown(Keys.Down);
        LeftPressed = keyState.IsKeyDown(Keys.Left) && !_prevKeyState.IsKeyDown(Keys.Left);
        RightPressed = keyState.IsKeyDown(Keys.Right) && !_prevKeyState.IsKeyDown(Keys.Right);

        TPressed = keyState.IsKeyDown(Keys.T) && !_prevKeyState.IsKeyDown(Keys.T);
        BracketLeftPressed = keyState.IsKeyDown(Keys.OemOpenBrackets) && !_prevKeyState.IsKeyDown(Keys.OemOpenBrackets);
        BracketRightPressed = keyState.IsKeyDown(Keys.OemCloseBrackets) && !_prevKeyState.IsKeyDown(Keys.OemCloseBrackets);
        GPressed = keyState.IsKeyDown(Keys.G) && !_prevKeyState.IsKeyDown(Keys.G);

        _prevKeyState = keyState;
        _prevMouseState = mouseState;
    }
}
