﻿using System;
using System.Collections;
using System.IO;
using System.Linq;
using Bonsai;

namespace OpenEphys.Onix
{
    class NeuropixelsV1eRegisterContext : I2CRegisterContext
    {
        public readonly double ApGainCorrection;
        public readonly double LfpGainCorrection;
        public ushort[] AdcThresholds { get; }
        public ushort[] AdcOffsets { get; }

        const int BaseConfigurationBitCount = 2448;
        const int BaseConfigurationConfigOffset = 576;
        const uint ShiftRegisterSuccess = 1 << 7;

        readonly NeuropixelsV1Adc[] Adcs = new NeuropixelsV1Adc[NeuropixelsV1e.AdcCount];
        readonly BitArray ShankConfig;
        readonly BitArray[] BaseConfigs = { new(BaseConfigurationBitCount, false),   // Ch 0, 2, 4, ...
                                            new(BaseConfigurationBitCount, false) }; // Ch 1, 3, 5, ...

        public NeuropixelsV1eRegisterContext(DeviceContext deviceContext, uint i2cAddress, 
            NeuropixelsV1Gain apGain, NeuropixelsV1Gain lfpGain, NeuropixelsV1ReferenceSource refSource, 
            bool apFilter, string gainCalibrationFile, string adcCalibrationFile, NeuropixelsV1eProbeGroup channelConfiguration)
            : base(deviceContext, i2cAddress)
        {
            if (gainCalibrationFile == null || adcCalibrationFile == null)
            {
                throw new ArgumentException("Calibration files must be specified.");
            }

            StreamReader gainFile = new(gainCalibrationFile);
            var sn = ulong.Parse(gainFile.ReadLine());

            StreamReader adcFile = new(adcCalibrationFile);
            if (sn != ulong.Parse(adcFile.ReadLine()))
                throw new ArgumentException("Calibration file serial numbers do not match.");

            // parse gain correction file
            NeuropixelsV1.ParseGainCalibrationFile(gainFile, apGain, lfpGain, ref ApGainCorrection, ref LfpGainCorrection);

            // parse ADC calibration file
            Adcs = NeuropixelsV1.ParseAdcCalibrationFile(adcFile);

            AdcThresholds = Adcs.ToList().Select(a => (ushort)a.Threshold).ToArray();
            AdcOffsets = Adcs.ToList().Select(a => (ushort)a.Offset).ToArray();

            // Update active channels
            ShankConfig = NeuropixelsV1.MakeShankBits(channelConfiguration, refSource);

            // create base shift-register bit arrays
            for (int i = 0; i < NeuropixelsV1e.ChannelCount; i++)
            {
                var configIdx = i % 2;

                // References
                var refIdx = configIdx == 0 ?
                    (382 - i) / 2 * 3 :
                    (383 - i) / 2 * 3;

                BaseConfigs[configIdx][refIdx + 0] = ((byte)refSource >> 0 & 0x1) == 1;
                BaseConfigs[configIdx][refIdx + 1] = ((byte)refSource >> 1 & 0x1) == 1;
                BaseConfigs[configIdx][refIdx + 2] = ((byte)refSource >> 2 & 0x1) == 1;

                var chanOptsIdx = BaseConfigurationConfigOffset + ((i - configIdx) * 4);

                // MSB [Full, standby, LFPGain(3 downto 0), APGain(3 downto0)] LSB

                BaseConfigs[configIdx][chanOptsIdx + 0] = ((byte)apGain >> 0 & 0x1) == 1;
                BaseConfigs[configIdx][chanOptsIdx + 1] = ((byte)apGain >> 1 & 0x1) == 1;
                BaseConfigs[configIdx][chanOptsIdx + 2] = ((byte)apGain >> 2 & 0x1) == 1;

                BaseConfigs[configIdx][chanOptsIdx + 3] = ((byte)lfpGain >> 0 & 0x1) == 1;
                BaseConfigs[configIdx][chanOptsIdx + 4] = ((byte)lfpGain >> 1 & 0x1) == 1;
                BaseConfigs[configIdx][chanOptsIdx + 5] = ((byte)lfpGain >> 2 & 0x1) == 1;

                BaseConfigs[configIdx][chanOptsIdx + 6] = false;
                BaseConfigs[configIdx][chanOptsIdx + 7] = !apFilter; // Full bandwidth = 1, filter on = 0

            }

            int k = 0;
            foreach (var adc in Adcs)
            {
                if (adc.CompP < 0 || adc.CompP > 0x1F)
                {
                    throw new ArgumentOutOfRangeException($"ADC calibration parameter CompP value of {adc.CompP} is invalid.");
                }

                if (adc.CompN < 0 || adc.CompN > 0x1F)
                {
                    throw new ArgumentOutOfRangeException($"ADC calibration parameter CompN value of {adc.CompN} is invalid.");
                }

                if (adc.Cfix < 0 || adc.Cfix > 0xF)
                {
                    throw new ArgumentOutOfRangeException($"ADC calibration parameter Cfix value of {adc.Cfix} is invalid.");
                }

                if (adc.Slope < 0 || adc.Slope > 0x7)
                {
                    throw new ArgumentOutOfRangeException($"ADC calibration parameter Slope value of {adc.Slope} is invalid.");
                }

                if (adc.Coarse < 0 || adc.Coarse > 0x3)
                {
                    throw new ArgumentOutOfRangeException($"ADC calibration parameter Coarse value of {adc.Coarse} is invalid.");
                }

                if (adc.Fine < 0 || adc.Fine > 0x3)
                {
                    throw new ArgumentOutOfRangeException($"ADC calibration parameter Fine value of {adc.Fine} is invalid.");
                }

                var configIdx = k % 2;
                int d = k++ / 2;

                int compOffset = 2406 - 42 * (d / 2) + (d % 2) * 10;
                int slopeOffset = compOffset + 20 + (d % 2);

                var compP = new BitArray(new byte[] { (byte)adc.CompP });
                var compN = new BitArray(new byte[] { (byte)adc.CompN });
                var cfix = new BitArray(new byte[] { (byte)adc.Cfix });
                var slope = new BitArray(new byte[] { (byte)adc.Slope });
                var coarse = (new BitArray(new byte[] { (byte)adc.Coarse }));
                var fine = new BitArray(new byte[] { (byte)adc.Fine });

                BaseConfigs[configIdx][compOffset + 0] = compP[0];
                BaseConfigs[configIdx][compOffset + 1] = compP[1];
                BaseConfigs[configIdx][compOffset + 2] = compP[2];
                BaseConfigs[configIdx][compOffset + 3] = compP[3];
                BaseConfigs[configIdx][compOffset + 4] = compP[4];

                BaseConfigs[configIdx][compOffset + 5] = compN[0];
                BaseConfigs[configIdx][compOffset + 6] = compN[1];
                BaseConfigs[configIdx][compOffset + 7] = compN[2];
                BaseConfigs[configIdx][compOffset + 8] = compN[3];
                BaseConfigs[configIdx][compOffset + 9] = compN[4];

                BaseConfigs[configIdx][slopeOffset + 0] = slope[0];
                BaseConfigs[configIdx][slopeOffset + 1] = slope[1];
                BaseConfigs[configIdx][slopeOffset + 2] = slope[2];

                BaseConfigs[configIdx][slopeOffset + 3] = fine[0];
                BaseConfigs[configIdx][slopeOffset + 4] = fine[1];

                BaseConfigs[configIdx][slopeOffset + 5] = coarse[0];
                BaseConfigs[configIdx][slopeOffset + 6] = coarse[1];

                BaseConfigs[configIdx][slopeOffset + 7] = cfix[0];
                BaseConfigs[configIdx][slopeOffset + 8] = cfix[1];
                BaseConfigs[configIdx][slopeOffset + 9] = cfix[2];
                BaseConfigs[configIdx][slopeOffset + 10] = cfix[3];

            }
        }

        public void InitializeProbe()
        {
            // get probe set up to receive configuration
            WriteByte(NeuropixelsV1e.CAL_MOD, (uint)NeuropixelsV1CalibrationRegisterValues.CAL_OFF);
            WriteByte(NeuropixelsV1e.TEST_CONFIG1, 0);
            WriteByte(NeuropixelsV1e.TEST_CONFIG2, 0);
            WriteByte(NeuropixelsV1e.TEST_CONFIG3, 0);
            WriteByte(NeuropixelsV1e.TEST_CONFIG4, 0);
            WriteByte(NeuropixelsV1e.TEST_CONFIG5, 0);
            WriteByte(NeuropixelsV1e.SYNC, 0);
            WriteByte(NeuropixelsV1e.REC_MOD, (uint)NeuropixelsV1RecordRegisterValues.ACTIVE);
            WriteByte(NeuropixelsV1e.OP_MODE, (uint)NeuropixelsV1OperationRegisterValues.RECORD);
        }

        // TODO: There is an issue getting these SR write sequences to complete correctly.
        // We have a suspicion it is due to the nature of the MCLK signal and that this
        // headstage needs either a different oscillator with even more drive strength or
        // a clock buffer (second might be easiest).
        public void WriteConfiguration()
        {
            // shank
            // NB: no read check because of ASIC bug
            var shankBytes = BitArrayToBytes(ShankConfig);

            WriteByte(NeuropixelsV1e.SR_LENGTH1, (uint)shankBytes.Length % 0x100);
            WriteByte(NeuropixelsV1e.SR_LENGTH2, (uint)shankBytes.Length / 0x100);

            foreach (var b in shankBytes)
            {
               WriteByte(NeuropixelsV1e.SR_CHAIN1, b);
            }

            // base
            for (int i = 0; i < BaseConfigs.Length; i++)
            {
                var srAddress = i == 0 ? NeuropixelsV1e.SR_CHAIN2 : NeuropixelsV1e.SR_CHAIN3;

                for (int j = 0; j < 2; j++)
                {
                    // TODO: HACK HACK HACK
                    // If we do not do this, the ShiftRegisterSuccess check below will always fail
                    // on whatever the second shift register write sequnece regardless of order or
                    // contents. Could be increased current draw during internal process causes MCLK
                    // to droop and mess up internal state. Or that MCLK is just not good enough to
                    // prevent metastability in some logic in the ASIC that is only entered in between
                    // SR accesses.
                    WriteByte(NeuropixelsV1e.SOFT_RESET, 0xFF);
                    WriteByte(NeuropixelsV1e.SOFT_RESET, 0x00);

                    var baseBytes = BitArrayToBytes(BaseConfigs[i]);

                    WriteByte(NeuropixelsV1e.SR_LENGTH1, (uint)baseBytes.Length % 0x100);
                    WriteByte(NeuropixelsV1e.SR_LENGTH2, (uint)baseBytes.Length / 0x100);

                    foreach (var b in baseBytes)
                    {
                        WriteByte(srAddress, b);
                    }
                }

                if (ReadByte(NeuropixelsV1e.STATUS) != ShiftRegisterSuccess)
                {
                    throw new WorkflowException($"Shift register {srAddress} status check failed.");
                }
            }
        }

        public void StartAcquisition()
        {
            // TODO: Hack inside settings.WriteShiftRegisters() above puts probe in reset set that needs to be
            // undone here
            WriteByte(NeuropixelsV1e.OP_MODE, (uint)NeuropixelsV1OperationRegisterValues.RECORD);
            WriteByte(NeuropixelsV1e.REC_MOD, (uint)NeuropixelsV1RecordRegisterValues.ACTIVE);
        }


        // Bits go into the shift registers MSB first
        // This creates a *bit-reversed* byte array from a bit array
        private static byte[] BitArrayToBytes(BitArray bits)
        {
            if (bits.Length == 0)
            {
                throw new ArgumentException("Shift register data is empty", nameof(bits));
            }

            var bytes = new byte[(bits.Length - 1) / 8 + 1];
            bits.CopyTo(bytes, 0);

            for (int i = 0; i < bytes.Length; i++)
            {
                // NB: http://graphics.stanford.edu/~seander/bithacks.html
                bytes[i] = (byte)((bytes[i] * 0x0202020202ul & 0x010884422010ul) % 1023);
            }

            return bytes;
        }
    }
}
