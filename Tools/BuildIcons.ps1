# IconSources altindaki orijinal gorsellerden oyunun kaynak ikonlarini uretir:
# seffaf boslugu kirpar, 128x128'e kucultur, koyu ikonlara acik kenar ekler.
# Kullanim: powershell -ExecutionPolicy Bypass -File Tools/BuildIcons.ps1

$ErrorActionPreference = 'Stop'

$Roots = @(
    @{ Source = 'IconSources\Economy'; Target = 'Assets\Art\Resources\Icons\Economy' },
    @{ Source = 'IconSources\Facilities'; Target = 'Assets\Art\Resources\Icons\Facilities' }
)
$IconSize = 128
$MarginPixels = 4
$RimPixels = 3
$RimColorHex = 'C4BEA6'
$RimOpacity = 0.9
# Koyu panelde kaybolan ikonlar: olcumde ortalama parlakligi dusuk cikanlar.
$RimKeys = 'coal', 'tire', 'oil', 'uranium', 'powerUnit', 'transformer', 'cable'

Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class IconBaker
{
    private const int Supersample = 4;
    private const byte VisibleAlpha = 32;
    private const byte SolidAlpha = 128;
    // Arka plan silme araci en dis piksellerde kalinti birakabilir; kirpma bunlari saymaz.
    private const int EdgeNoisePixels = 2;

    public static void Bake(string source, string target, int size, int margin, int rim, Color rimColor, double rimOpacity)
    {
        using (var original = new Bitmap(source))
        using (var work = new Bitmap(size * Supersample, size * Supersample, PixelFormat.Format32bppArgb))
        {
            Rectangle box = FindContent(original);
            int area = (size - margin * 2) * Supersample;
            double scale = (double)area / Math.Max(box.Width, box.Height);
            int width = (int)Math.Round(box.Width * scale);
            int height = (int)Math.Round(box.Height * scale);
            var destination = new Rectangle((work.Width - width) / 2, (work.Height - height) / 2, width, height);
            Draw(original, box, work, destination);

            if (rim > 0)
            {
                AddRim(work, rim * Supersample, rimColor, rimOpacity);
            }

            using (var result = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                Draw(work, new Rectangle(0, 0, work.Width, work.Height), result, new Rectangle(0, 0, size, size));
                result.Save(target, ImageFormat.Png);
            }
        }
    }

    private static void Draw(Bitmap from, Rectangle fromArea, Bitmap to, Rectangle toArea)
    {
        using (var graphics = Graphics.FromImage(to))
        using (var attributes = new ImageAttributes())
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            graphics.DrawImage(from, toArea, fromArea.X, fromArea.Y, fromArea.Width, fromArea.Height, GraphicsUnit.Pixel, attributes);
        }
    }

    private static Rectangle FindContent(Bitmap bitmap)
    {
        byte[] pixels = Read(bitmap);
        int minX = bitmap.Width, minY = bitmap.Height, maxX = -1, maxY = -1;
        for (int y = EdgeNoisePixels; y < bitmap.Height - EdgeNoisePixels; y++)
        {
            for (int x = EdgeNoisePixels; x < bitmap.Width - EdgeNoisePixels; x++)
            {
                if (pixels[(y * bitmap.Width + x) * 4 + 3] < VisibleAlpha)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < 0)
        {
            throw new InvalidOperationException("Gorselde gorunur piksel yok.");
        }

        return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    // Kenar, gorselin arkasina serilir: gorselin renkleri degismez, deliklerin ici de cizilir.
    private static void AddRim(Bitmap bitmap, int radius, Color color, double opacity)
    {
        int width = bitmap.Width, height = bitmap.Height;
        byte[] pixels = Read(bitmap);
        var mask = new bool[width * height];
        int radiusSquared = radius * radius;

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                int index = y * width + x;
                if (pixels[index * 4 + 3] < SolidAlpha)
                {
                    continue;
                }

                bool isEdge = pixels[(index - 1) * 4 + 3] < SolidAlpha || pixels[(index + 1) * 4 + 3] < SolidAlpha
                    || pixels[(index - width) * 4 + 3] < SolidAlpha || pixels[(index + width) * 4 + 3] < SolidAlpha;
                if (!isEdge)
                {
                    continue;
                }

                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int px = x + dx, py = y + dy;
                        if (dx * dx + dy * dy <= radiusSquared && px >= 0 && py >= 0 && px < width && py < height)
                        {
                            mask[py * width + px] = true;
                        }
                    }
                }
            }
        }

        for (int i = 0; i < mask.Length; i++)
        {
            if (!mask[i])
            {
                continue;
            }

            int p = i * 4;
            double front = pixels[p + 3] / 255.0;
            double back = opacity * (1.0 - front);
            double alpha = front + back;
            if (alpha <= 0.0)
            {
                continue;
            }

            pixels[p] = (byte)((pixels[p] * front + color.B * back) / alpha);
            pixels[p + 1] = (byte)((pixels[p + 1] * front + color.G * back) / alpha);
            pixels[p + 2] = (byte)((pixels[p + 2] * front + color.R * back) / alpha);
            pixels[p + 3] = (byte)(alpha * 255.0);
        }

        Write(bitmap, pixels);
    }

    private static byte[] Read(Bitmap bitmap)
    {
        var area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixels = new byte[bitmap.Width * bitmap.Height * 4];
        for (int y = 0; y < bitmap.Height; y++)
        {
            Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4);
        }

        bitmap.UnlockBits(data);
        return pixels;
    }

    private static void Write(Bitmap bitmap, byte[] pixels)
    {
        var area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(area, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < bitmap.Height; y++)
        {
            Marshal.Copy(pixels, y * bitmap.Width * 4, data.Scan0 + y * data.Stride, bitmap.Width * 4);
        }

        bitmap.UnlockBits(data);
    }
}
'@

$rimColor = [System.Drawing.ColorTranslator]::FromHtml('#' + $RimColorHex)
$count = 0
foreach ($root in $Roots) {
    New-Item -ItemType Directory -Force -Path $root.Target | Out-Null
    $sourceFull = (Resolve-Path $root.Source).Path
    $targetFull = (Resolve-Path $root.Target).Path
    foreach ($file in Get-ChildItem $root.Source -Recurse -File -Filter *.png) {
        $target = Join-Path $targetFull $file.FullName.Substring($sourceFull.Length + 1)
        $temporary = "$target.tmp"
        $rim = if ($RimKeys -contains $file.BaseName) { $RimPixels } else { 0 }
        [IconBaker]::Bake($file.FullName, $temporary, $IconSize, $MarginPixels, $rim, $rimColor, $RimOpacity)
        Move-Item -LiteralPath $temporary -Destination $target -Force
        $count++
    }
}

"$count ikon uretildi."
