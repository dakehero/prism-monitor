namespace NativeGuard.Core.Ui;

public sealed class TrayWindowLifetime
{
    private bool _hasWindow;
    private bool _exitCloseRequested;

    public bool NeedsWindow => !_hasWindow;

    public void MarkWindowCreated()
    {
        _hasWindow = true;
        _exitCloseRequested = false;
    }

    public void MarkHiddenToTray()
    {
        _hasWindow = true;
    }

    public void RequestExitClose()
    {
        _exitCloseRequested = true;
    }

    public void MarkWindowClosed()
    {
        if (_exitCloseRequested)
        {
            _hasWindow = false;
        }
    }
}
