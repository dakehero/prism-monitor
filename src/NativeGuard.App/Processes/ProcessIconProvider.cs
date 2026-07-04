using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace NativeGuard_App.Processes;

internal sealed class ProcessIconProvider
{
    private readonly Dictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ImageSource GetIcon(string processName, string? executablePath)
    {
        string cacheKey = string.IsNullOrWhiteSpace(executablePath) ? processName : executablePath;
        if (_cache.TryGetValue(cacheKey, out ImageSource? cachedIcon))
        {
            return cachedIcon;
        }

        ImageSource icon = CreateDefaultIcon();
        _cache[cacheKey] = icon;
        return icon;
    }

    private static ImageSource CreateDefaultIcon()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        return new BitmapImage(new Uri(iconPath));
    }
}
