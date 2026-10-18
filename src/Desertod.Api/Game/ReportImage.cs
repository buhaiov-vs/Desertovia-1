namespace Desertod.Api.Game;

/// <summary>Bounded JPEG frame validation before storing an attachment; no external image fetches.</summary>
internal static class ReportImage
{
    public static bool Valid(byte[] image)
    {
        if (image.Length is < 12 or > 350000 || image[0] != 0xff || image[1] != 0xd8 || image[^2] != 0xff || image[^1] != 0xd9) return false;
        var position = 2;
        for (var segments = 0; segments < 256 && position + 3 < image.Length; segments++)
        {
            if (image[position++] != 0xff) return false;
            while (position < image.Length && image[position] == 0xff) position++;
            if (position + 2 >= image.Length) return false;
            var marker = image[position++];
            if (marker is 0xda or 0xd9 or 0x00) return false;
            var length = image[position] * 256 + image[position + 1];
            if (length < 2 || position + length > image.Length) return false;
            if (marker is >= 0xc0 and <= 0xcf && marker is not (0xc4 or 0xc8 or 0xcc))
            {
                if (length < 8) return false;
                var height = image[position + 3] * 256 + image[position + 4];
                var width = image[position + 5] * 256 + image[position + 6];
                return width is > 0 and <= 2048 && height is > 0 and <= 2048;
            }
            position += length;
        }
        return false;
    }
}
