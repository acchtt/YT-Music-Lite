using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using YTMusicLite;

internal static class BuildBrandAssets
{
    static void Main(string[] args)
    {
        Directory.CreateDirectory(args[0]);
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        byte[][] frames = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++)
        {
            using (Bitmap bitmap = BrandArt.CreateBitmap(sizes[i]))
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                frames[i] = stream.ToArray();
                bitmap.Save(Path.Combine(args[0], "logo-" + sizes[i] + ".png"), ImageFormat.Png);
            }
        }
        using (BinaryWriter writer = new BinaryWriter(File.Create(Path.Combine(args[0], "YTMusicLite.ico"))))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset = 6 + sizes.Length * 16;
            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)0); writer.Write((byte)0);
                writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(frames[i].Length); writer.Write(offset);
                offset += frames[i].Length;
            }
            foreach (byte[] frame in frames) writer.Write(frame);
        }
    }
}
