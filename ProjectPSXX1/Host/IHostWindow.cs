Namespace ProjectPSXX1 {
    Public Interface IHostWindow {
        void Render(int[] vram);
        void SetDisplayMode(int horizontalRes, int verticalRes, bool is24BitDepth);
        void SetHorizontalRange(UShort displayX1, ushort displayX2);
        void SetVRAMStart(UShort displayVRAMXStart, ushort displayVRAMYStart);
        void SetVerticalRange(UShort displayY1, ushort displayY2);
        void Play(Byte[] samples);
    }
}
