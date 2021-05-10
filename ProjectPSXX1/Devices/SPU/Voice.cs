Using System;

Namespace ProjectPSXX1.Devices.Spu {
    Public Class Voice {

        Private Static ReadOnlySpan<SByte> positiveXaAdpcmTable => New SByte[] { 0, 60, 115, 98, 122 };
        Private Static ReadOnlySpan<SByte> negativeXaAdpcmTable => New SByte[] { 0, 0, -52, -55, -60 };

        Public struct Volume {
            Public UShort register;
            Public bool isSweepMode => ((register >> 15) & 0x1) != 0;
            Public Short fixedVolume => (Short)(register << 1);
            Public bool isSweepExponential => ((register >> 14) & 0x1) != 0;
            Public bool isSweepDirectionDecrease => ((register >> 13) & 0x1) != 0;
            Public bool isSweepPhaseNegative => ((register >> 12) & 0x1) != 0;
            Public int sweepShift => (register >> 2) & 0x1F;
            Public int sweepStep => register & 0x3;
        }
        Public Volume volumeLeft;           //0
        Public Volume volumeRight;          //2

        Public UShort pitch;                //4
        Public UShort startAddress;         //6
        Public UShort currentAddress;       //6 Internal

        Public struct ADSR {
            Public UShort lo;               //8
            Public UShort hi;               //A
            Public bool isAttackModeExponential => ((lo >> 15) & 0x1) != 0;
            Public int attackShift => (lo >> 10) & 0x1F;
            Public int attackStep => (lo >> 8) & 0x3; //"+7,+6,+5,+4"
            Public int decayShift => (lo >> 4) & 0xF;
            Public int sustainLevel => lo & 0xF; //Level=(N+1)*800h

            Public bool isSustainModeExponential => ((hi >> 15) & 0x1) != 0;
            Public bool isSustainDirectionDecrease => ((hi >> 14) & 0x1) != 0;
            Public int sustainShift => (hi >> 8) & 0x1F;
            Public int sustainStep => (hi >> 6) & 0x3;
            Public bool isReleaseModeExponential => ((hi >> 5) & 0x1) != 0;
            Public int releaseShift => hi & 0x1F;
        }
        Public ADSR adsr;

        Public UShort adsrVolume;           //C
        Public UShort adpcmRepeatAddress;   //E

        Public struct Counter {            //internal
            Public uint register;
            Public uint currentSampleIndex {
                Get { Return (register >> 12) & 0x1F; }
                Set {
                    register = (ushort)(register &= 0xFFF);
                    register |= value << 12;
                }
            }

            Public uint interpolationIndex => (register >> 3) & 0xFF;
        }
        Public Counter counter;

        Public Phase adsrPhase;

        Public Short old;
        Public Short older;

        Public Short latest;

        Public bool hasSamples;

        Public bool readRamIrq;

        Public Voice() {
            adsrPhase = Phase.Off;
        }

        Public void keyOn() {
            hasSamples = false;
            old = 0;
            older = 0;
            currentAddress = startAddress;
            adsrCounter = 0;
            adsrVolume = 0;
            adsrPhase = Phase.Attack;
        }

        Public void keyOff() {
            adsrCounter = 0;
            adsrPhase = Phase.Release;
        }

        Public Enum Phase {
            Attack,
            Decay,
            Sustain,
            Release,
            Off,
        }

        Public Byte[] spuAdpcm = New Byte[16];
        Public Short[] decodedSamples = New Short[31]; //28 samples from current block + 3 To make room For interpolation
        internal void decodeSamples(Byte[] ram, UShort ramIrqAddress) {
            //save the last 3 samples from the last decoded block
            //this are needed for interpolation in case the voice.counter.currentSampleIndex Is 0 1 Or 2
            decodedSamples[2] = decodedSamples[decodedSamples.Length - 1];
            decodedSamples[1] = decodedSamples[decodedSamples.Length - 2];
            decodedSamples[0] = decodedSamples[decodedSamples.Length - 3];

            Array.Copy(ram, currentAddress * 8, spuAdpcm, 0, 16);

            //ramIrqAddress Is >> 8 so we only need to check for currentAddress And + 1
            readRamIrq |= currentAddress == ramIrqAddress || currentAddress + 1 == ramIrqAddress;           

            int shift = 12 - (spuAdpcm[0] & 0x0F);
            int filter = (spuAdpcm[0] & 0x70) >> 4; //filter on SPU adpcm Is 0-4 vs XA wich Is 0-3
            If (filter > 4) filter = 4; //Crash Bandicoot sets this To 7 at the End Of the first level And overflows the filter

            int f0 = positiveXaAdpcmTable[filter];
            int f1 = negativeXaAdpcmTable[filter];

            //Actual ADPCM decoding Is the same as on XA but the layout here Is sequencial by nibble where on XA in grouped by nibble line
            int position = 2; //skip shift And flags
            int nibble = 1;
            For (int i = 0; i < 28; i++) {
                nibble = (nibble + 1) & 0x1;

                int t = signed4bit((Byte)((spuAdpcm[position] >> (nibble * 4)) & 0x0F));
                int s = (t << shift) + ((old * f0 + older * f1 + 32) / 64);
                Short sample = (Short)Math.Clamp(s, -0x8000, 0x7FFF);

                decodedSamples[3 + i] = sample;

                older = old;
                old = sample;

                position += nibble;
            }
        }

        Public Static int signed4bit(Byte value) {
            Return (value << 28) >> 28;
        }

        internal short processVolume(Volume volume) {
            If (!volume.isSweepMode) {
                Return volume.fixedVolume;
            } else {
                Return 0; //todo handle sweep mode volume envelope
            }
        }

        int adsrCounter;
        internal void tickAdsr(int v) {
            If (adsrPhase == Phase.Off) {
                adsrVolume = 0;
                Return;
            }

            int adsrTarget;
            int adsrShift;
            int adsrStep;
            bool isDecreasing;
            bool isExponential;

            //Todo move out of tick the actual change of phase
            switch (adsrPhase) {
                Case Phase.Attack : 
                    adsrTarget = 0x7FFF;
                    adsrShift = adsr.attackShift;
                    adsrStep = 7 - adsr.attackStep; // reg Is 0-3 but values are "+7,+6,+5,+4"
                    isDecreasing = false; // Allways increase till 0x7FFF
                    isExponential = adsr.isAttackModeExponential;
                    break;
                Case Phase.Decay : 
                    adsrTarget = (adsr.sustainLevel + 1) * 0x800;
                    adsrShift = adsr.decayShift;
                    adsrStep = -8;
                    isDecreasing = true; // Allways decreases (till target)
                    isExponential = true; // Allways exponential
                    break;
                Case Phase.Sustain : 
                    adsrTarget = 0;
                    adsrShift = adsr.sustainShift;
                    adsrStep = adsr.isSustainDirectionDecrease? -8 + adsr.sustainStep: 7 - adsr.sustainStep;
                    isDecreasing = adsr.isSustainDirectionDecrease; //till keyoff
                    isExponential = adsr.isSustainModeExponential;
                    break;
                Case Phase.Release : 
                    adsrTarget = 0;
                    adsrShift = adsr.releaseShift;
                    adsrStep = -8;
                    isDecreasing = true; // Allways decrease till 0
                    isExponential = adsr.isReleaseModeExponential;
                    break;
                Default: 
                    adsrTarget = 0;
                    adsrShift = 0;
                    adsrStep = 0;
                    isDecreasing = false;
                    isExponential = false;
                    break;
            }

            //Envelope Operation depending on Shift/Step/Mode/Direction
            //AdsrCycles = 1 SHL Max(0, ShiftValue-11)
            //AdsrStep = StepValue SHL Max(0,11-ShiftValue)
            //IF exponential And increase And AdsrLevel>6000h THEN AdsrCycles=AdsrCycles*4    
            //IF exponential And decrease THEN AdsrStep = AdsrStep * AdsrLevel / 8000h
            //Wait(AdsrCycles); cycles counted at 44.1kHz clock
            //AdsrLevel=AdsrLevel+AdsrStep  ;saturated to 0..+7FFFh

            If (adsrCounter > 0) { adsrCounter--; Return; }

            int envelopeCycles = 1 << Math.Max(0, adsrShift - 11);
            int envelopeStep = adsrStep << Math.Max(0, 11 - adsrShift);
            If (isExponential && !isDecreasing && adsrVolume > 0x6000) { envelopeCycles *= 4; }
            If (isExponential && isDecreasing) { envelopeStep = (envelopeStep * adsrVolume) >> 15; }

            adsrVolume = (ushort)Math.Clamp(adsrVolume + envelopeStep, 0, 0x7FFF);
            adsrCounter = envelopeCycles;

            bool nextPhase = isDecreasing?(adsrVolume <= adsrTarget) :  (adsrVolume >= adsrTarget);
            If (nextPhase && adsrPhase!= Phase.Sustain) {
                adsrPhase++;
                adsrCounter = 0;
            };
        }
    }
}
