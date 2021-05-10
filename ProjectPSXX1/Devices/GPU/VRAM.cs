Using System.Runtime.CompilerServices;
Using System.Runtime.InteropServices;

Namespace ProjectPSXX1 {
    Public Class VRAM {
        Public int[] Bits { Get; Private Set; }
        Public int Height;
        Public int Width;

        Protected GCHandle BitsHandle { Get; Private Set; }

        Public VRAM(int width, int height) {
            Height = height;
            Width = width;
            Bits = New int[Width * Height];
            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Public void SetPixel(int x, int y, int color) {
            int index = x + (y * Width);
            Bits[index] = color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Public int GetPixelRGB888(int x, int y) {
            int index = x + (y * Width);
            Return Bits[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Public UShort GetPixelBGR555(int x, int y) {
            int index = x + (y * Width);
            int color = Bits[index];

            Byte m = (Byte)((color & 0xFF000000) >> 24);
            Byte r = (Byte)((color & 0x00FF0000) >> 16 + 3);
            Byte g = (Byte)((color & 0x0000FF00) >> 8 + 3);
            Byte b = (Byte)((color & 0x000000FF) >> 3);

            Return (UShort)(m << 15 | b << 10 | g << 5 | r);
        }

    }
}
