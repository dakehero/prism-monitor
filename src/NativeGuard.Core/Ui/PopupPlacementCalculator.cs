namespace NativeGuard.Core.Ui;

public readonly record struct ScreenPoint(int X, int Y);

public readonly record struct ScreenSize(int Width, int Height);

public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;
}

public static class PopupPlacementCalculator
{
    public static ScreenPoint Calculate(ScreenRect anchor, ScreenSize window, ScreenRect workArea)
    {
        int x = anchor.X + anchor.Width / 2 - window.Width / 2;
        int y = anchor.Y - window.Height;

        if (y < workArea.Y)
        {
            y = anchor.Bottom;
        }

        x = Clamp(x, workArea.X, workArea.Right - window.Width);
        y = Clamp(y, workArea.Y, workArea.Bottom - window.Height);

        return new ScreenPoint(x, y);
    }

    private static int Clamp(int value, int min, int max)
    {
        if (max < min)
        {
            return min;
        }

        return Math.Min(Math.Max(value, min), max);
    }
}
