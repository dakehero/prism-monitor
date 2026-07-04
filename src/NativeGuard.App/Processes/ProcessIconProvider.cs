using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace NativeGuard_App.Processes;

internal sealed class ProcessIconProvider
{
    private readonly Dictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ImageSource> GetIconAsync(string processName, string? executablePath)
    {
        string cacheKey = string.IsNullOrWhiteSpace(executablePath) ? processName : executablePath;
        if (_cache.TryGetValue(cacheKey, out ImageSource? cachedIcon))
        {
            return cachedIcon;
        }

        ImageSource icon = await TryCreateFileThumbnailAsync(executablePath).ConfigureAwait(true)
            ?? CreateDefaultIcon();
        _cache[cacheKey] = icon;
        return icon;
    }

    private static async Task<ImageSource?> TryCreateFileThumbnailAsync(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return null;
        }

        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(executablePath);
            using StorageItemThumbnail thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 32);
            BitmapImage image = new();
            await image.SetSourceAsync(thumbnail);
            return image;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static ImageSource CreateDefaultIcon()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        return new BitmapImage(new Uri(iconPath));
    }
}
