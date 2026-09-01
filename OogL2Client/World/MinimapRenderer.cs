using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace OogL2Client.World;

public sealed class MinimapRenderer
{
    private const int RegionOffsetX = 20;
    private const int RegionOffsetY = 18;

    private readonly string _mapsDirectory;
    private readonly int _tileWorldSize;
    private readonly Dictionary<string, double> _tileRichnessCache = new(StringComparer.OrdinalIgnoreCase);
    private MapTransform? _calibratedTransform;

    private sealed record MapTransform(int OffsetX, int OffsetY, int SignY, string Name);

    public MinimapRenderer(string mapsDirectory, int tileWorldSize = 32768)
    {
        _mapsDirectory = mapsDirectory;
        _tileWorldSize = Math.Max(1024, tileWorldSize);
    }

    public Bitmap Render(WorldState worldState, int mapX, int mapY, int viewportWidth = 300, int viewportHeight = 300, int radius = 2000, int targetObjectId = 0)
    {
        var canvas = new Bitmap(viewportWidth, viewportHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using var g = Graphics.FromImage(canvas);
        g.Clear(Color.Black);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        DrawMapTiles(g, mapX, mapY, viewportWidth, viewportHeight, radius);

        var self = worldState.Self;
        if (self is null)
        {
            return canvas;
        }

        var centerX = viewportWidth / 2;
        var centerY = viewportHeight / 2;
        int? targetPx = null;
        int? targetPy = null;

        foreach (var obj in worldState.Nearby(self.X, self.Y, radius))
        {
            if (obj.ObjectId == self.ObjectId)
            {
                continue;
            }

            var dx = obj.X - self.X;
            var dy = obj.Y - self.Y;
            var px = centerX + dx / 10;
            var py = centerY + dy / 10;

            if (px < 0 || px >= viewportWidth || py < 0 || py >= viewportHeight)
            {
                continue;
            }

            var color = obj.Type switch
            {
                WorldObjectType.Player => Color.Cyan,
                WorldObjectType.Monster => Color.Red,
                WorldObjectType.NPC => Color.Yellow,
                WorldObjectType.Item => Color.Lime,
                _ => Color.Gray
            };

            using var pen = new Pen(color, 2f);
            g.DrawEllipse(pen, (float)px - 2f, (float)py - 2f, 4f, 4f);

            if (targetObjectId > 0 && obj.ObjectId == targetObjectId)
            {
                targetPx = px;
                targetPy = py;
            }
        }

        WorldObject? target = null;
        if (targetObjectId > 0)
        {
            target = worldState.Get(targetObjectId);
            if (target is not null)
            {
                var tdx = target.X - self.X;
                var tdy = target.Y - self.Y;
                var tpx = centerX + tdx / 10;
                var tpy = centerY + tdy / 10;
                if (tpx >= 0 && tpx < viewportWidth && tpy >= 0 && tpy < viewportHeight)
                {
                    targetPx = tpx;
                    targetPy = tpy;
                }
            }
        }

        if (targetPx.HasValue && targetPy.HasValue)
        {
            using var targetPen = new Pen(Color.Fuchsia, 2.5f);
            g.DrawEllipse(targetPen, targetPx.Value - 8, targetPy.Value - 8, 16, 16);
            g.DrawLine(targetPen, targetPx.Value - 10, targetPy.Value, targetPx.Value + 10, targetPy.Value);
            g.DrawLine(targetPen, targetPx.Value, targetPy.Value - 10, targetPx.Value, targetPy.Value + 10);
        }

        if (targetObjectId > 0)
        {
            DrawTargetBadge(g, targetObjectId, target);
        }

        using (var selfPen = new Pen(Color.White, 2.5f))
        {
            g.DrawEllipse(selfPen, centerX - 6, centerY - 6, 12, 12);
            g.FillEllipse(Brushes.White, centerX - 3, centerY - 3, 6, 6);
        }

        return canvas;
    }

    private static void DrawTargetBadge(Graphics g, int targetObjectId, WorldObject? target)
    {
        var targetName = string.IsNullOrWhiteSpace(target?.Name) ? $"Object {targetObjectId}" : target!.Name;
        var hpText = target is null
            ? "-"
            : target.MaxHp > 0
                ? $"{target.Hp}/{target.MaxHp}"
                : target.Hp > 0 ? target.Hp.ToString(CultureInfo.InvariantCulture) : "-";
        var text = $"Target: {targetName} | HP {hpText}";

        using var font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Bold);
        var size = g.MeasureString(text, font);
        var badge = new RectangleF(8, 8, size.Width + 14, size.Height + 8);
        using var bg = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
        using var border = new Pen(Color.Fuchsia, 1.5f);
        using var textBrush = new SolidBrush(Color.White);
        g.FillRectangle(bg, badge);
        g.DrawRectangle(border, badge.X, badge.Y, badge.Width, badge.Height);
        g.DrawString(text, font, textBrush, badge.X + 7, badge.Y + 4);
    }

    private void DrawMapTiles(Graphics g, int mapX, int mapY, int viewportWidth, int viewportHeight, int radius)
    {
        var transform = PickTransform(mapX, mapY);
        var viewMinX = mapX - radius;
        var viewMaxX = mapX + radius;
        var viewMinY = mapY - radius;
        var viewMaxY = mapY + radius;

        var minRegionX = WorldToRegionX(viewMinX, transform);
        var maxRegionX = WorldToRegionX(viewMaxX, transform);
        var minRegionY = WorldToRegionY(viewMinY, transform);
        var maxRegionY = WorldToRegionY(viewMaxY, transform);

        if (minRegionX > maxRegionX)
        {
            (minRegionX, maxRegionX) = (maxRegionX, minRegionX);
        }

        if (minRegionY > maxRegionY)
        {
            (minRegionY, maxRegionY) = (maxRegionY, minRegionY);
        }

        var loadedAnyTile = false;
        string? firstTile = null;
        for (var regionY = minRegionY; regionY <= maxRegionY; regionY++)
        {
            for (var regionX = minRegionX; regionX <= maxRegionX; regionX++)
            {
                var tilePath = ResolveTilePath(regionX, regionY);
                if (tilePath is null)
                {
                    continue;
                }

                var tileWorldMinX = RegionToWorldMinX(regionX, transform);
                var tileWorldMaxX = tileWorldMinX + _tileWorldSize;
                var tileWorldMinY = RegionToWorldMinY(regionY, transform);
                var tileWorldMaxY = tileWorldMinY + (_tileWorldSize * transform.SignY);

                var left = ToPixelX(tileWorldMinX, viewMinX, viewMaxX, viewportWidth);
                var right = ToPixelX(tileWorldMaxX, viewMinX, viewMaxX, viewportWidth);
                var top = ToPixelY(tileWorldMinY, viewMinY, viewMaxY, viewportHeight);
                var bottom = ToPixelY(tileWorldMaxY, viewMinY, viewMaxY, viewportHeight);

                var drawX = (int)Math.Floor(Math.Min(left, right));
                var drawY = (int)Math.Floor(Math.Min(top, bottom));
                var drawWidth = (int)Math.Ceiling(Math.Abs(right - left));
                var drawHeight = (int)Math.Ceiling(Math.Abs(bottom - top));

                if (drawWidth <= 0 || drawHeight <= 0)
                {
                    continue;
                }

                using var tile = Image.FromFile(tilePath);
                g.DrawImage(tile, new Rectangle(drawX, drawY, drawWidth, drawHeight));
                loadedAnyTile = true;
                firstTile ??= Path.GetFileName(tilePath);
            }
        }

        if (!loadedAnyTile)
        {
            using var font = new Font(FontFamily.GenericSansSerif, 12f);
            g.DrawString("No map tiles for this location", font, Brushes.White, 10, 10);
            return;
        }

        DrawDebugOverlay(g, mapX, mapY, transform, firstTile ?? "(unknown)");
    }

    private MapTransform PickTransform(int mapX, int mapY)
    {
        if (_calibratedTransform is not null)
        {
            return _calibratedTransform;
        }

        var candidates = new[]
        {
            new MapTransform(RegionOffsetX, RegionOffsetY, 1, "normal-y"),
            new MapTransform(RegionOffsetX, RegionOffsetY, -1, "inverted-y")
        };

        var best = candidates[0];
        var bestScore = double.MinValue;

        foreach (var candidate in BuildCalibrationCandidates())
        {
            var regionX = WorldToRegionX(mapX, candidate);
            var regionY = WorldToRegionY(mapY, candidate);
            var tile = ResolveTilePath(regionX, regionY);
            if (tile is null)
            {
                continue;
            }

            var score = GetTileRichness(tile);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        foreach (var candidate in candidates)
        {
            var regionX = WorldToRegionX(mapX, candidate);
            var regionY = WorldToRegionY(mapY, candidate);
            var tile = ResolveTilePath(regionX, regionY);
            if (tile is null)
            {
                continue;
            }

            var score = GetTileRichness(tile);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        _calibratedTransform = best;
        return best;
    }

    private static IEnumerable<MapTransform> BuildCalibrationCandidates()
    {
        const int spread = 8;
        for (var signY = -1; signY <= 1; signY += 2)
        {
            for (var dx = -spread; dx <= spread; dx++)
            {
                for (var dy = -spread; dy <= spread; dy++)
                {
                    var offsetX = RegionOffsetX + dx;
                    var offsetY = RegionOffsetY + dy;
                    yield return new MapTransform(offsetX, offsetY, signY, $"auto({offsetX},{offsetY},{signY})");
                }
            }
        }
    }

    private void DrawDebugOverlay(Graphics g, int mapX, int mapY, MapTransform transform, string tileName)
    {
        var regionX = WorldToRegionX(mapX, transform);
        var regionY = WorldToRegionY(mapY, transform);
        var debug = $"Tile {regionX}_{regionY} [{transform.Name}]\n{tileName}";

        using var bg = new SolidBrush(Color.FromArgb(135, 0, 0, 0));
        using var fg = new SolidBrush(Color.White);
        using var font = new Font(FontFamily.GenericSansSerif, 8f, FontStyle.Bold);
        var rect = new RectangleF(6, 6, 260, 34);
        g.FillRectangle(bg, rect);
        g.DrawString(debug, font, fg, rect.Location);
    }

    private string? ResolveTilePath(int regionX, int regionY)
    {
        var candidates = new List<string>();
        var direct = Path.Combine(_mapsDirectory, $"{regionX}_{regionY}.jpg");
        if (File.Exists(direct))
        {
            candidates.Add(direct);
        }

        candidates.AddRange(Directory.GetFiles(_mapsDirectory, $"{regionX}_{regionY}_*.jpg"));
        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        // Prefer visually rich variants over flat/placeholder textures.
        return candidates
            .OrderByDescending(GetTileRichness)
            .ThenByDescending(GetVariantSuffix)
            .FirstOrDefault();
    }

    private double GetTileRichness(string path)
    {
        if (_tileRichnessCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        try
        {
            using var image = Image.FromFile(path);
            using var bitmap = new Bitmap(image);

            var stepX = Math.Max(1, bitmap.Width / 24);
            var stepY = Math.Max(1, bitmap.Height / 24);
            var sampleCount = 0;
            var sum = 0d;
            var sumSq = 0d;

            for (var y = 0; y < bitmap.Height; y += stepY)
            {
                for (var x = 0; x < bitmap.Width; x += stepX)
                {
                    var color = bitmap.GetPixel(x, y);
                    var luma = (0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B);
                    sum += luma;
                    sumSq += luma * luma;
                    sampleCount++;
                }
            }

            if (sampleCount == 0)
            {
                _tileRichnessCache[path] = 0;
                return 0;
            }

            var mean = sum / sampleCount;
            var variance = Math.Max(0, (sumSq / sampleCount) - (mean * mean));
            _tileRichnessCache[path] = variance;
            return variance;
        }
        catch
        {
            _tileRichnessCache[path] = 0;
            return 0;
        }
    }

    private static int GetVariantSuffix(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var parts = name.Split('_');
        if (parts.Length < 3)
        {
            return -1;
        }

        return int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : -1;
    }

    private static int FloorDiv(int value, int divisor)
    {
        if (value >= 0)
        {
            return value / divisor;
        }

        return -((-value + divisor - 1) / divisor);
    }

    private static int WorldToRegionX(int worldX, MapTransform transform)
    {
        return FloorDiv(worldX, 32768) + transform.OffsetX;
    }

    private static int WorldToRegionY(int worldY, MapTransform transform)
    {
        return (transform.SignY * FloorDiv(worldY, 32768)) + transform.OffsetY;
    }

    private static int RegionToWorldMinX(int regionX, MapTransform transform)
    {
        return (regionX - transform.OffsetX) * 32768;
    }

    private static int RegionToWorldMinY(int regionY, MapTransform transform)
    {
        return transform.SignY == 1
            ? (regionY - transform.OffsetY) * 32768
            : (transform.OffsetY - regionY) * 32768;
    }

    private static float ToPixelX(int worldX, int viewMinX, int viewMaxX, int viewportWidth)
    {
        var t = (float)(worldX - viewMinX) / (viewMaxX - viewMinX);
        return t * viewportWidth;
    }

    private static float ToPixelY(int worldY, int viewMinY, int viewMaxY, int viewportHeight)
    {
        var t = (float)(worldY - viewMinY) / (viewMaxY - viewMinY);
        return t * viewportHeight;
    }
}
