Using System;
Using System.Runtime.CompilerServices;
Using ProjectPSXX1.Disassembler;

Namespace ProjectPSXX1 {
    internal unsafe Class CPU {  //MIPS R3000A-compatible 32-bit RISC CPU MIPS R3051 With 5 KB L1 cache, running at 33.8688 MHz // 33868800

       Private uint PC_Now; // PC On current execution As PC And PC Predictor go ahead after fetch. This Is handy On Branch Delay so it dosn't give erronious PC-4
       Private uint PC = 0xbfc0_0000; // Bios Entry Point
       Private uint PC_Predictor = 0xbfc0_0004; //Next op For branch delay slot emulation

       Private uint[] GPR = New uint[32];
       Private uint HI;
       Private uint LO;

       Private bool opcodeIsBranch;
       Private bool opcodeIsDelaySlot;

       Private bool opcodeTookBranch;
       Private bool opcodeInDelaySlotTookBranch;

       Private Static uint[] ExceptionAdress = New uint[] { 0x8000_0080, 0xBFC0_0180 };

       //CoPro Regs
       Private uint[] COP0_GPR = New uint[16];
       Private Const int SR = 12;
       Private Const int CAUSE = 13;
       Private Const int EPC = 14;
       Private Const int BADA = 8;
       Private Const int JUMPDEST = 6;

       Private bool dontIsolateCache;

       Private GTE gte;
       Private BUS bus;

       Private BIOS_Disassembler bios;
       Private MIPS_Disassembler mips;

       Private struct MEM {
           Public uint register;
           Public uint value;
       }
       Private MEM writeBack;
       Private MEM memoryLoad;
       Private MEM delayedMemoryLoad;

       Public struct Instr {
           Public uint value;                     //raw
           Public uint opcode => value >> 26;     //Instr opcode

           //I-Type
           Public uint rs => (value >> 21) & 0x1F;//Register Source
           Public uint rt => (value >> 16) & 0x1F;//Register Target
           Public uint imm => value & 0xFFFF;     //Immediate value
           Public uint imm_s => (uint)(Short)imm; //Immediate value sign extended

           //R-Type
           Public uint rd => (value >> 11) & 0x1F;
           Public uint sa => (value >> 6) & 0x1F;  //Shift Amount
           Public uint Function => value & 0x3F;   //Function

           //J-Type                                       
           Public uint addr => value & 0x3FFFFFF;  //Target Address

           //id / Cop
           Public uint id => opcode & 0x3; //This Is used mainly For coprocesor opcode id but its also used On opcodes that trigger exception
       }
       Private Instr instr;

       //Debug
       Private Long cycle; //current CPU cycle counter For debug
       Public bool debug = False;

       Public CPU(BUS bus) {
           this.bus = bus;
           bios = New BIOS_Disassembler(bus);
           mips = New MIPS_Disassembler(ref HI, ref LO, GPR, COP0_GPR);
           gte = New GTE();

           COP0_GPR[15] = 0x2; //PRID Processor ID

           initOpCodeTable();
       }
       Private Static Delegate*<CPU, void>[] opcodeMainTable;
       Private Static Delegate*<CPU, void>[] opcodeSpecialTable;

       Private void initOpCodeTable() {
           Static void SPECIAL(CPU cpu) => cpu.SPECIAL();
           Static void BCOND(CPU cpu) => cpu.BCOND();
           Static void J(CPU cpu) => cpu.J();
           Static void JAL(CPU cpu) => cpu.JAL();
           Static void BEQ(CPU cpu) => cpu.BEQ();
           Static void BNE(CPU cpu) => cpu.BNE();
           Static void BLEZ(CPU cpu) => cpu.BLEZ();
           Static void BGTZ(CPU cpu) => cpu.BGTZ();
           Static void ADDI(CPU cpu) => cpu.ADDI();
           Static void ADDIU(CPU cpu) => cpu.ADDIU();
           Static void SLTI(CPU cpu) => cpu.SLTI();
           Static void SLTIU(CPU cpu) => cpu.SLTIU();
           Static void ANDI(CPU cpu) => cpu.ANDI();
           Static void ORI(CPU cpu) => cpu.ORI();
           Static void XORI(CPU cpu) => cpu.XORI();
           Static void LUI(CPU cpu) => cpu.LUI();
           Static void COP0(CPU cpu) => cpu.COP0();
           Static void NOP(CPU cpu) => cpu.NOP();
           Static void COP2(CPU cpu) => cpu.COP2();
           Static void NA(CPU cpu) => cpu.NA();
           Static void LB(CPU cpu) => cpu.LB();
           Static void LH(CPU cpu) => cpu.LH();
           Static void LWL(CPU cpu) => cpu.LWL();
           Static void LW(CPU cpu) => cpu.LW();
           Static void LBU(CPU cpu) => cpu.LBU();
           Static void LHU(CPU cpu) => cpu.LHU();
           Static void LWR(CPU cpu) => cpu.LWR();
           Static void SB(CPU cpu) => cpu.SB();
           Static void SH(CPU cpu) => cpu.SH();
           Static void SWL(CPU cpu) => cpu.SWL();
           Static void SW(CPU cpu) => cpu.SW();
           Static void SWR(CPU cpu) => cpu.SWR();
           Static void LWC2(CPU cpu) => cpu.LWC2();
           Static void SWC2(CPU cpu) => cpu.SWC2();

            opcodeMainTable = New delegate*<CPU, void>[] {
                &SPECIAL,  &BCOND,  &J,      &JAL,    &BEQ,    &BNE,    &BLEZ,   &BGTZ,
                &ADDI,     &ADDIU,  &SLTI,   &SLTIU,  &ANDI,   &ORI,    &XORI,   &LUI,
                &COP0,     &NOP,    &COP2,   &NOP,    &NA,     &NA,     &NA,     &NA,
                &NA,       &NA,     &NA,     &NA,     &NA,     &NA,     &NA,     &NA,
                &LB,       &LH,     &LWL,    &LW,     &LBU,    &LHU,    &LWR,    &NA,
                &SB,       &SH,     &SWL,    &SW,     &NA,     &NA,     &SWR,    &NA,
                &NOP,      &NOP,    &LWC2,   &NOP,    &NA,     &NA,     &NA,     &NA,
                &NOP,      &NOP,    &SWC2,   &NOP,    &NA,     &NA,     &NA,     &NA,
            };

            Static void SLL(CPU cpu) => cpu.SLL();
            Static void SRL(CPU cpu) => cpu.SRL();
            Static void SRA(CPU cpu) => cpu.SRA();
            Static void SLLV(CPU cpu) => cpu.SLLV();
            Static void SRLV(CPU cpu) => cpu.SRLV();
            Static void SRAV(CPU cpu) => cpu.SRAV();
            Static void JR(CPU cpu) => cpu.JR();
            Static void SYSCALL(CPU cpu) => cpu.SYSCALL();
            Static void BREAK(CPU cpu) => cpu.BREAK();
            Static void JALR(CPU cpu) => cpu.JALR();
            Static void MFHI(CPU cpu) => cpu.MFHI();
            Static void MTHI(CPU cpu) => cpu.MTHI();
            Static void MFLO(CPU cpu) => cpu.MFLO();
            Static void MTLO(CPU cpu) => cpu.MTLO();
            Static void MULT(CPU cpu) => cpu.MULT();
            Static void MULTU(CPU cpu) => cpu.MULTU();
            Static void DIV(CPU cpu) => cpu.DIV();
            Static void DIVU(CPU cpu) => cpu.DIVU();
            Static void ADD(CPU cpu) => cpu.ADD();
            Static void ADDU(CPU cpu) => cpu.ADDU();
            Static void Sub(CPU cpu) => cpu.Sub();
            Static void SUBU(CPU cpu) => cpu.SUBU();
            Static void And(CPU cpu) => cpu.And();
            Static void Or(CPU cpu) => cpu.Or();
            Static void Xor(CPU cpu) => cpu.Xor();
            Static void NOR(CPU cpu) => cpu.NOR();
            Static void SLT(CPU cpu) => cpu.SLT();
            Static void SLTU(CPU cpu) => cpu.SLTU();

            opcodeSpecialTable = New delegate*<CPU, void>[] {
                &SLL,   &NA,    &SRL,   &SRA,   &SLLV,    &NA,     &SRLV, &SRAV,
                &JR,    &JALR,  &NA,    &NA,    &SYSCALL, &BREAK,  &NA,   &NA,
                &MFHI,  &MTHI,  &MFLO,  &MTLO,  &NA,      &NA,     &NA,   &NA,
                &MULT,  &MULTU, &DIV,   &DIVU,  &NA,      &NA,     &NA,   &NA,
                &ADD,   &ADDU,  &SUB,   &SUBU,  &And,     &OR,     &Xor,  &NOR,
                &NA,    &NA,    &SLT,   &SLTU,  &NA,      &NA,     &NA,   &NA,
                &NA,    &NA,    &NA,    &NA,    &NA,      &NA,     &NA,   &NA,
                &NA,    &NA,    &NA,    &NA,    &NA,      &NA,     &NA,   &NA,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Public void Run() {
            fetchDecode();
            If (instr.value!= 0) { //Skip Nops
                opcodeMainTable[instr.opcode](this); //Execute
            }
            MemAccess();
            WriteBack();

            //if (debug) {
            //  mips.PrintRegs();
            //  mips.disassemble(instr, PC_Now, PC_Predictor);
            //}

            //TTY();
            //bios.verbose(PC_Now, GPR);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Public void handleInterrupts() {
            //Executable address space Is limited to ram And bios on psx
            uint maskedPC = PC & 0x1FFF_FFFF;
            uint load;
            If (maskedPC < 0x1F00_0000) {
                load = bus.LoadFromRam(maskedPC);
            } else {
                load = bus.LoadFromBios(maskedPC);
            }

            //This Is actually the "next" opcode if it's a GTE one
            //just postpone the interrupt so it doesn't glitch out
            //Crash Bandicoot intro Is a good example for this
            uint instr = load >> 26;
            If (instr == 0x12) { //COP2 MTC2
                //Console.WriteLine("WARNING COP2 OPCODE ON INTERRUPT");
                Return;
            }

            If (bus.interruptController.interruptPending()) {
                COP0_GPR[CAUSE] |= 0x400;
            } else {
                COP0_GPR[CAUSE] &= ~(uint)0x400;
            }

            bool IEC = (COP0_GPR[SR] & 0x1) == 1;
            uint IM = (COP0_GPR[SR] >> 8) & 0xFF;
            uint IP = (COP0_GPR[CAUSE] >> 8) & 0xFF;

            If (IEC && (IM & IP) > 0) {
                EXCEPTION(EX.INTERRUPT);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Private void fetchDecode() {
            //Executable address space Is limited to ram And bios on psx
            uint maskedPC = PC & 0x1FFF_FFFF;
            uint load;
            If (maskedPC < 0x1F00_0000) {
                load = bus.LoadFromRam(maskedPC);
            } else {
                load = bus.LoadFromBios(maskedPC);
            }

            PC_Now = PC;
            PC = PC_Predictor;
            PC_Predictor += 4;

            opcodeIsDelaySlot = opcodeIsBranch;
            opcodeInDelaySlotTookBranch = opcodeTookBranch;
            opcodeIsBranch = false;
            opcodeTookBranch = false;

            If ((PC_Now & 0x3) != 0) {
                COP0_GPR[BADA] = PC_Now;
                EXCEPTION(EX.LOAD_ADRESS_ERROR);
                Return;
            }

            instr.value = load;
            //cycle++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Private void MemAccess() {
            If (delayedMemoryLoad.register!= memoryLoad.register) { //If loadDelay On same reg it Is lost/overwritten (amidog tests)
                GPR[memoryLoad.register] = memoryLoad.value;
            }
            memoryLoad = delayedMemoryLoad;
            delayedMemoryLoad.register = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Private void WriteBack() {
            GPR[writeBack.register] = writeBack.value;
            writeBack.register = 0;
            GPR[0] = 0;
        }

        // Non Implemented by the CPU Opcodes
        Private void NOP() { /*nop*/ }

        Private void NA() => EXCEPTION(EX.ILLEGAL_INSTR, instr.id);


        // Main Table Opcodes
        Private void SPECIAL() => opcodeSpecialTable[instr.Function](this);

        Private void BCOND() {
            opcodeIsBranch = true;
            uint op = Instr.rt;

            bool should_link = (op & 0x1E) == 0x10;
            bool should_branch = (int)(GPR[instr.rs] ^ (op << 31)) < 0;

            If (should_link) GPR[31] = PC_Predictor;
            If (should_branch) BRANCH();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Private void J() {
            opcodeIsBranch = true;
            opcodeTookBranch = true;
            PC_Predictor = (PC_Predictor & 0xF000_0000) | (instr.addr << 2);
        }

        Private void JAL() {
            setGPR(31, PC_Predictor);
            J();
        }

        Private void BEQ() {
            opcodeIsBranch = true;
            If (GPR[instr.rs] == GPR[instr.rt]) {
                BRANCH();
            }
        }

        Private void BNE() {
            opcodeIsBranch = true;
            If (GPR[instr.rs] != GPR[instr.rt]) {
                BRANCH();
            }
        }

        Private void BLEZ() {
            opcodeIsBranch = true;
            If (((int)GPR[instr.rs]) <= 0) {
                BRANCH();
            }
        }

        Private void BGTZ() {
            opcodeIsBranch = true;
            If (((int)GPR[instr.rs]) > 0) {
                BRANCH();
            }
        }

        private void ADDI() {
            int rs = (int)GPR[instr.rs];
            int imm_s = (int)instr.imm_s;
            try {
                uint addi = (uint)checked(rs + imm_s);
                setGPR(instr.rt, addi);
            }
            catch (OverflowException) {
                EXCEPTION(EX.OVERFLOW, instr.id);
            }
        }

        private void ADDIU() => setGPR(instr.rt, GPR[instr.rs] + instr.imm_s);

        private void SLTI() {
            bool condition = (int)GPR[instr.rs] < (int)instr.imm_s;
            setGPR(instr.rt, Unsafe.As<bool, uint>(ref condition));
        }

        private void SLTIU() {
            bool condition = GPR[instr.rs] < instr.imm_s;
            setGPR(instr.rt, Unsafe.As<bool, uint>(ref condition));
        }

        private void ANDI() => setGPR(instr.rt, GPR[instr.rs] & instr.imm);

        private void ORI() => setGPR(instr.rt, GPR[instr.rs] | instr.imm);

        private void XORI() => setGPR(instr.rt, GPR[instr.rs] ^ instr.imm);

        private void LUI() => setGPR(instr.rt, instr.imm << 16);

        private void COP0() {
            if (instr.rs == 0b0_0000) MFC0();
            else if (instr.rs == 0b0_0100) MTC0();
            else if (instr.rs == 0b1_0000) RFE();
            else EXCEPTION(EX.ILLEGAL_INSTR, instr.id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MFC0() {
            uint mfc = instr.rd;
            if (mfc == 3 || mfc >= 5 && mfc <= 9 || mfc >= 11 && mfc <= 15) {
                delayedLoad(instr.rt, COP0_GPR[mfc]);
            } else {
                EXCEPTION(EX.ILLEGAL_INSTR, instr.id);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MTC0() {
            uint value = GPR[instr.rt];
            uint register = instr.rd;

            if (register == CAUSE) { //only bits 8 and 9 are writable
                COP0_GPR[CAUSE] &= ~(uint)0x300;
                COP0_GPR[CAUSE] |= value & 0x300;
            } else if (register == SR) {
                //This can trigger soft interrupts
                dontIsolateCache = (value & 0x10000) == 0;
                bool prevIEC = (COP0_GPR[SR] & 0x1) == 1;
                bool currentIEC = (value & 0x1) == 1;

                COP0_GPR[SR] = value;

                uint IM = (value >> 8) & 0x3;
                uint IP = (COP0_GPR[CAUSE] >> 8) & 0x3;

                if (!prevIEC && currentIEC && (IM & IP) > 0) {
                    PC = PC_Predictor;
                    EXCEPTION(EX.INTERRUPT, instr.id);
                }

            } else {
                COP0_GPR[register] = value;
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RFE() {
            uint mode = COP0_GPR[SR] & 0x3F;
            COP0_GPR[SR] &= ~(uint)0xF;
            COP0_GPR[SR] |= mode >> 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EXCEPTION(EX cause, uint coprocessor = 0) {
            uint mode = COP0_GPR[SR] & 0x3F;
            COP0_GPR[SR] &= ~(uint)0x3F;
            COP0_GPR[SR] |= (mode << 2) & 0x3F;

            uint OldCause = COP0_GPR[CAUSE] & 0xff00;
            COP0_GPR[CAUSE] = (uint)cause << 2;
            COP0_GPR[CAUSE] |= OldCause;
            COP0_GPR[CAUSE] |= coprocessor << 28;

            if (cause == EX.INTERRUPT) {
                COP0_GPR[EPC] = PC;
                //hack: related to the delay of the ex interrupt
                opcodeIsDelaySlot = opcodeIsBranch;
                opcodeInDelaySlotTookBranch = opcodeTookBranch;
            } else {
                COP0_GPR[EPC] = PC_Now;
            }

            if (opcodeIsDelaySlot) {
                COP0_GPR[EPC] -= 4;
                COP0_GPR[CAUSE] |= (uint)1 << 31;
                COP0_GPR[JUMPDEST] = PC;

                if (opcodeInDelaySlotTookBranch) {
                    COP0_GPR[CAUSE] |= (1 << 30);
                }
            }

            PC = ExceptionAdress[COP0_GPR[SR] & 0x400000 >> 22];
            PC_Predictor = PC + 4;
        }

        private void COP2() {
            if ((instr.rs & 0x10) == 0) {
                switch (instr.rs) {
                    case 0b0_0000: MFC2(); break;
                    case 0b0_0010: CFC2(); break;
                    case 0b0_0100: MTC2(); break;
                    case 0b0_0110: CTC2(); break;
                    default: EXCEPTION(EX.ILLEGAL_INSTR, instr.id); break;
                }
            } else {
                gte.execute(instr.value);
            }
        }

        private void MFC2() => delayedLoad(instr.rt, gte.loadData(instr.rd));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CFC2() => delayedLoad(instr.rt, gte.loadControl(instr.rd));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MTC2() => gte.writeData(instr.rd, GPR[instr.rt]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CTC2() => gte.writeControl(instr.rd, GPR[instr.rt]);

        private void LWC2() { //TODO WARNING THIS SHOULD HAVE DELAY?
            uint addr = GPR[instr.rs] + instr.imm_s;

            if ((addr & 0x3) == 0) {
                uint value = bus.load32(addr);
                gte.writeData(instr.rt, value);
            } else {
                COP0_GPR[BADA] = addr;
                EXCEPTION(EX.LOAD_ADRESS_ERROR, instr.id);
            }
        }

        private void SWC2() { //TODO WARNING THIS SHOULD HAVE DELAY?
            uint addr = GPR[instr.rs] + instr.imm_s;

            if ((addr & 0x3) == 0) {
                bus.write32(addr, gte.loadData(instr.rt));
            } else {
                COP0_GPR[BADA] = addr;
                EXCEPTION(EX.LOAD_ADRESS_ERROR, instr.id);
            }
        }

        private void LB() { //todo redo this as it unnecesary load32
            if (dontIsolateCache) {
                uint value = (uint)(sbyte)bus.load32(GPR[instr.rs] + instr.imm_s);
                delayedLoad(instr.rt, value);
            } //else Console.WriteLine("IsolatedCache: Ignoring Load");
        }

        private void LBU() {
            if (dontIsolateCache) {
                uint value = (byte)bus.load32(GPR[instr.rs] + instr.imm_s);
                delayedLoad(instr.rt, value);
            } //else Console.WriteLine("IsolatedCache: Ignoring Load");
        }

        private void LH() {
            if (dontIsolateCache) {
                uint addr = GPR[instr.rs] + instr.imm_s;

                if ((addr & 0x1) == 0) {
                    uint value = (uint)(short)bus.load32(addr);
                    delayedLoad(instr.rt, value);
                } else {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.LOAD_ADRESS_ERROR, instr.id);
                }

            } //else Console.WriteLine("IsolatedCache: Ignoring Load");
        }

        private void LHU() {
            if (dontIsolateCache) {
                uint addr = GPR[instr.rs] + instr.imm_s;

                if ((addr & 0x1) == 0) {
                    uint value = (ushort)bus.load32(addr);
                    delayedLoad(instr.rt, value);
                } else {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.LOAD_ADRESS_ERROR, instr.id);
                }

            } //else Console.WriteLine("IsolatedCache: Ignoring Load");
        }

        private void LW() {
            if (dontIsolateCache) {
                uint addr = GPR[instr.rs] + instr.imm_s;

                if ((addr & 0x3) == 0) {
                    uint value = bus.load32(addr);
                    delayedLoad(instr.rt, value);
                } else {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.LOAD_ADRESS_ERROR, instr.id);
                }

            } //else Console.WriteLine("IsolatedCache: Ignoring Load");
        }

        private void LWL() {
            uint addr = GPR[instr.rs] + instr.imm_s;
            uint aligned_addr = addr & 0xFFFF_FFFC;
            uint aligned_load = bus.load32(aligned_addr);

            uint value = 0;
            uint LRValue = GPR[instr.rt];

            if (instr.rt == memoryLoad.register) {
                LRValue = memoryLoad.value;
            }

            switch (addr & 0b11) {
                case 0: value = (LRValue & 0x00FF_FFFF) | (aligned_load << 24); break;
                case 1: value = (LRValue & 0x0000_FFFF) | (aligned_load << 16); break;
                case 2: value = (LRValue & 0x0000_00FF) | (aligned_load << 8); break;
                case 3: value = aligned_load; break;
            }

            delayedLoad(instr.rt, value);
        }

        private void LWR() {
            uint addr = GPR[instr.rs] + instr.imm_s;
            uint aligned_addr = addr & 0xFFFF_FFFC;
            uint aligned_load = bus.load32(aligned_addr);

            uint value = 0;
            uint LRValue = GPR[instr.rt];

            if (instr.rt == memoryLoad.register) {
                LRValue = memoryLoad.value;
            }

            switch (addr & 0b11) {
                case 0: value = aligned_load; break;
                case 1: value = (LRValue & 0xFF00_0000) | (aligned_load >> 8); break;
                case 2: value = (LRValue & 0xFFFF_0000) | (aligned_load >> 16); break;
                case 3: value = (LRValue & 0xFFFF_FF00) | (aligned_load >> 24); break;
            }

            delayedLoad(instr.rt, value);
        }

        private void SB() {
            if (dontIsolateCache)
                bus.write8(GPR[instr.rs] + instr.imm_s, (byte)GPR[instr.rt]);
            //else Console.WriteLine("IsolatedCache: Ignoring Write");
        }

        private void SH() {
            if (dontIsolateCache) {
                uint addr = GPR[instr.rs] + instr.imm_s;

                if ((addr & 0x1) == 0) {
                    bus.write16(addr, (ushort)GPR[instr.rt]);
                } else {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.STORE_ADRESS_ERROR, instr.id);
                }
            } //else Console.WriteLine("IsolatedCache: Ignoring Write");
        }

        private void SW() {
            if (dontIsolateCache) {
                uint addr = GPR[instr.rs] + instr.imm_s;

                if ((addr & 0x3) == 0) {
                    bus.write32(addr, GPR[instr.rt]);
                } else {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.STORE_ADRESS_ERROR, instr.id);
                }
            } //else Console.WriteLine("IsolatedCache: Ignoring Write");
        }

        private void SWR() {
            uint addr = GPR[instr.rs] + instr.imm_s;
            uint aligned_addr = addr & 0xFFFF_FFFC;
            uint aligned_load = bus.load32(aligned_addr);

            uint value = 0;
            switch (addr & 0b11) {
                case 0: value = GPR[instr.rt]; break;
                case 1: value = (aligned_load & 0x0000_00FF) | (GPR[instr.rt] << 8); break;
                case 2: value = (aligned_load & 0x0000_FFFF) | (GPR[instr.rt] << 16); break;
                case 3: value = (aligned_load & 0x00FF_FFFF) | (GPR[instr.rt] << 24); break;
            }

            bus.write32(aligned_addr, value);
        }

        private void SWL() {
            uint addr = GPR[instr.rs] + instr.imm_s;
            uint aligned_addr = addr & 0xFFFF_FFFC;
            uint aligned_load = bus.load32(aligned_addr);

            uint value = 0;
            switch (addr & 0b11) {
                case 0: value = (aligned_load & 0xFFFF_FF00) | (GPR[instr.rt] >> 24); break;
                case 1: value = (aligned_load & 0xFFFF_0000) | (GPR[instr.rt] >> 16); break;
                case 2: value = (aligned_load & 0xFF00_0000) | (GPR[instr.rt] >> 8); break;
                case 3: value = GPR[instr.rt]; break;
            }

            bus.write32(aligned_addr, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BRANCH() {
            opcodeTookBranch = true;
            PC_Predictor = PC + (instr.imm_s << 2);
        }


        // Special Table Opcodes (Nested on Opcode 0x00 with additional function param)

        private void SLL() => setGPR(instr.rd, GPR[instr.rt] << (int)instr.sa);

        private void SRL() => setGPR(instr.rd, GPR[instr.rt] >> (int)instr.sa);

        private void SRA() => setGPR(instr.rd, (uint)((int)GPR[instr.rt] >> (int)instr.sa));

        private void SLLV() => setGPR(instr.rd, GPR[instr.rt] << (int)(GPR[instr.rs] & 0x1F));

        private void SRLV() => setGPR(instr.rd, GPR[instr.rt] >> (int)(GPR[instr.rs] & 0x1F));

        private void SRAV() => setGPR(instr.rd, (uint)((int)GPR[instr.rt] >> (int)(GPR[instr.rs] & 0x1F)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void JR() {
            opcodeIsBranch = true;
            opcodeTookBranch = true;
            PC_Predictor = GPR[instr.rs];
        }

        private void SYSCALL() => EXCEPTION(EX.SYSCALL, instr.id);

        private void BREAK() => EXCEPTION(EX.BREAK);

        private void JALR() {
            setGPR(instr.rd, PC_Predictor);
            JR();
        }

        private void MFHI() => setGPR(instr.rd, HI);

        private void MTHI() => HI = GPR[instr.rs];

        private void MFLO() => setGPR(instr.rd, LO);

        private void MTLO() => LO = GPR[instr.rs];

        private void MULT() {
            long value = (long)(int)GPR[instr.rs] * (long)(int)GPR[instr.rt]; //sign extend to pass amidog cpu test

            HI = (uint)(value >> 32);
            LO = (uint)value;
        }

        private void MULTU() {
            ulong value = (ulong)GPR[instr.rs] * (ulong)GPR[instr.rt]; //sign extend to pass amidog cpu test

            HI = (uint)(value >> 32);
            LO = (uint)value;
        }

        private void DIV() {
            int n = (int)GPR[instr.rs];
            int d = (int)GPR[instr.rt];

            if (d == 0) {
                HI = (uint)n;
                if (n >= 0) {
                    LO = 0xFFFF_FFFF;
                } else {
                    LO = 1;
                }
            } else if ((uint)n == 0x8000_0000 && d == -1) {
                HI = 0;
                LO = 0x8000_0000;
            } else {
                HI = (uint)(n % d);
                LO = (uint)(n / d);
            }
        }

        private void DIVU() {
            uint n = GPR[instr.rs];
            uint d = GPR[instr.rt];

            if (d == 0) {
                HI = n;
                LO = 0xFFFF_FFFF;
            } else {
                HI = n % d;
                LO = n / d;
            }
        }

        private void ADD() {
            int rs = (int)GPR[instr.rs];
            int rt = (int)GPR[instr.rt];
            try {
                uint add = (uint)checked(rs + rt);
                setGPR(instr.rd, add);
            } catch (OverflowException) {
                EXCEPTION(EX.OVERFLOW, instr.id);
            }
        }

        private void ADDU() => setGPR(instr.rd, GPR[instr.rs] + GPR[instr.rt]);

        private void SUB() {
            int rs = (int)GPR[instr.rs];
            int rt = (int)GPR[instr.rt];
            try {
                uint sub = (uint)checked(rs - rt);
                setGPR(instr.rd, sub);
            } catch (OverflowException) {
                EXCEPTION(EX.OVERFLOW, instr.id);
            }
        }

        private void SUBU() => setGPR(instr.rd, GPR[instr.rs] - GPR[instr.rt]);

        private void AND() => setGPR(instr.rd, GPR[instr.rs] & GPR[instr.rt]);

        private void OR() => setGPR(instr.rd, GPR[instr.rs] | GPR[instr.rt]);

        private void XOR() => setGPR(instr.rd, GPR[instr.rs] ^ GPR[instr.rt]);

        private void NOR() => setGPR(instr.rd, ~(GPR[instr.rs] | GPR[instr.rt]));

        private void SLT() {
            bool condition = (int)GPR[instr.rs] < (int)GPR[instr.rt];
            setGPR(instr.rd, Unsafe.As<bool, uint>(ref condition));
        }

        private void SLTU() {
            bool condition = GPR[instr.rs] < GPR[instr.rt];
            setGPR(instr.rd, Unsafe.As<bool, uint>(ref condition));
        }


        // Accesory methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void setGPR(uint regN, uint value) {
            writeBack.register = regN;
            writeBack.value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void delayedLoad(uint regN, uint value) {
            delayedMemoryLoad.register = regN;
            delayedMemoryLoad.value = value;
        }

        private void TTY() {
            if (PC == 0x00000B0 && GPR[9] == 0x3D || PC == 0x00000A0 && GPR[9] == 0x3C) {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.Write((char)GPR[4]);
                Console.ResetColor();
            }
        }

    }
}
