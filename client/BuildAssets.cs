using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using YTMusicLite.Client;

internal static class BuildAssets
{
    private static void Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Output directory required.");
        Directory.CreateDirectory(args[0]);
        int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
        byte[][] frames = new byte[sizes.Length][];
        for (int index = 0; index < sizes.Length; index++)
        {
            using (Bitmap image = Branding.CreateBitmap(sizes[index]))
            using (MemoryStream stream = new MemoryStream())
            {
                image.Save(stream, ImageFormat.Png);
                frames[index] = stream.ToArray();
                image.Save(Path.Combine(args[0], "logo-" + sizes[index] + ".png"), ImageFormat.Png);
            }
        }
        using (BinaryWriter writer = new BinaryWriter(File.Create(Path.Combine(args[0], "YTMusicLite.ico"))))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset = 6 + sizes.Length * 16;
            for (int index = 0; index < sizes.Length; index++)
            {
                writer.Write((byte)(sizes[index] == 256 ? 0 : sizes[index]));
                writer.Write((byte)(sizes[index] == 256 ? 0 : sizes[index]));
                writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(frames[index].Length); writer.Write(offset); offset += frames[index].Length;
            }
            foreach (byte[] frame in frames) writer.Write(frame);
        }
    }
}
