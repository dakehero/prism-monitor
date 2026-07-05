using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace PrismMonitor.App.Tray;

internal static class TrayIconBadgeFactory
{
    public static IntPtr CreateBadgedIcon(IntPtr baseIcon, int count)
    {
        if (baseIcon == IntPtr.Zero || count <= 0)
        {
            return baseIcon;
        }

        using Icon icon = Icon.FromHandle(baseIcon);
        using Bitmap bitmap = new(32, 32);
        using Graphics graphics = Graphics.FromImage(bitmap);

        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        graphics.DrawIcon(icon, new Rectangle(0, 0, 32, 32));

        string text = count > 9 ? "9+" : count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Rectangle badge = text.Length > 1
            ? new Rectangle(13, 15, 18, 15)
            : new Rectangle(16, 15, 14, 14);

        using Brush fill = new SolidBrush(Color.FromArgb(232, 43, 43));
        using Brush textBrush = new SolidBrush(Color.White);
        using Font font = new("Segoe UI", text.Length > 1 ? 7.5f : 8.5f, FontStyle.Bold, GraphicsUnit.Pixel);
        using StringFormat format = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        graphics.FillEllipse(fill, badge);
        graphics.DrawString(text, font, textBrush, badge, format);

        return bitmap.GetHicon();
    }
}
