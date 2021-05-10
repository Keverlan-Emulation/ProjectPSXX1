Using System.Runtime.CompilerServices;
Using System.Runtime.InteropServices;

Namespace ProjectPSXX1 {
    Public Class VRAM1555 {
        Public UShort[] Bits { Get; Private Set; }
        Public int Height;
        Public int Width;

        Protected GCHandle BitsHandle { Get; Private Set; }

        Public VRAM1555(int width, int height) {
            Height = height;
            Width = width;
            Bits = New ushort[Width * Height];
            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Public void SetPixel(int x, int y, UShort color) {
            int index = x + (y * Width);
            Bits[index] = color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Public UShort GetPixel(int x, int y) {
            int index = x + (y * Width);
            Return Bits[index];
        }

    }
}
