Imports System.Diagnostics

Using ProjectPSXX1.Devices;
Using ProjectPSXX1.Devices.CdRom;
Using ProjectPSXX1.Devices.Input;

Namespace ProjectPSXX1{
    <DebuggerDisplay("{GetDebuggerDisplay(),nq}")>
    Public Class ProjectPSXX1{
        Const int PSX_MHZ = 33868800;
        Const int SYNC_CYCLES = 100;
        Const int MIPS_UNDERCLOCK = 3; //Testing: This compensates the ausence Of HALT instruction On MIPS Architecture, may broke some games.
        Const int CYCLES_PER_FRAME = PSX_MHZ / 60;
        Const int SYNC_LOOPS = (CYCLES_PER_FRAME / (SYNC_CYCLES * MIPS_UNDERCLOCK)) + 1;

        Private CPU cpu;
        Private BUS bus;
        Private CDROM cdrom;
        Private GPU gpu;
        Private SPU spu;
        Private JOYPAD joypad;
        Private TIMERS timers;
        Private MDEC mdec;
        Private Controller controller;
        Private MemoryCard memoryCard;
        Private CD cd;
        Private InterruptController interruptController;

        Public ProjectPSX(IHostWindow window, string diskFilename) {
            controller = New DigitalController();
            memoryCard = New MemoryCard();

            interruptController = New InterruptController();

            cd = New CD(diskFilename);
            spu = New SPU(window, interruptController);
            gpu = New GPU(window);
            cdrom = New CDROM(cd, spu);
            joypad = New JOYPAD(controller, memoryCard);
            timers = New TIMERS();
            mdec = New MDEC();
            bus = New BUS(gpu, cdrom, spu, joypad, timers, mdec, interruptController);
            cpu = New CPU(bus);

            bus.loadBios();
            If (diskFilename.EndsWith(".exe")) {
                bus.loadEXE(diskFilename);
            }
        }

        Public void RunFrame() {
            //A lame mainloop with a workaround to be able to underclock.
            For (int i = 0; i < SYNC_LOOPS; i++) {
                For (int j = 0; j < SYNC_CYCLES; j++) {
                    cpu.Run();
                    //cpu.handleInterrupts();
                }
                bus.tick(SYNC_CYCLES * MIPS_UNDERCLOCK);
                cpu.handleInterrupts();
            }
        }
      
  
        Public void JoyPadUp(GamepadInputsEnum button) => controller.handleJoyPadUp(button);
        Public void JoyPadDown(GamepadInputsEnum button) => controller.handleJoyPadDown(button);

        Public void toggleDebug() {
            cpu.debug = !cpu.debug;
            gpu.debug = !gpu.debug;
        }

    }
}
Private Function GetDebuggerDisplay() As String
            Return ToString()
        End Function
