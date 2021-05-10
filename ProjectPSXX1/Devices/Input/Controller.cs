Using System.Collections.Generic;
Using ProjectPSXX1.Devices.Input;

Namespace ProjectPSXX1 {
    Public abstract Class Controller {
        Protected IHostWindow window;

        Protected Queue<Byte> transferDataFifo = New Queue<Byte>();
        Protected UShort buttons = 0xFFFF;
        Public bool ack;

        Public abstract Byte process(Byte b);
        Public abstract void resetToIdle();

        Public void handleJoyPadDown(GamepadInputsEnum inputCode) {
            buttons &= (ushort)~(buttons & (ushort)inputCode);           
            //Console.WriteLine(buttons.ToString("x8"));
        }

        Public void handleJoyPadUp(GamepadInputsEnum inputCode) {
            buttons |= (ushort)inputCode;
            //Console.WriteLine(buttons.ToString("x8"));
        }

    }
}
End Namespace
