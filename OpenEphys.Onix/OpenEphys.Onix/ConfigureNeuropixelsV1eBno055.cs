﻿using System;
using System.ComponentModel;

namespace OpenEphys.Onix
{
    [Editor("OpenEphys.Onix.Design.NeuropixelsV1eBno055Editor, OpenEphys.Onix.Design", typeof(ComponentEditor))]
    public class ConfigureNeuropixelsV1eBno055 : SingleDeviceFactory
    {
        public ConfigureNeuropixelsV1eBno055()
            : base(typeof(NeuropixelsV1eBno055))
        {
        }

        public ConfigureNeuropixelsV1eBno055(ConfigureNeuropixelsV1eBno055 configureBno055)
            : base(typeof(NeuropixelsV1eBno055))
        {
            Enable = configureBno055.Enable;
        }

        [Category(ConfigurationCategory)]
        [Description("Specifies whether the BNO055 device is enabled.")]
        public bool Enable { get; set; } = true;

        public override IObservable<ContextTask> Process(IObservable<ContextTask> source)
        {
            var enable = Enable;
            var deviceName = DeviceName;
            var deviceAddress = DeviceAddress;
            return source.ConfigureDevice(context =>
            {
                // configure device via the DS90UB9x deserializer device
                var device = context.GetPassthroughDeviceContext(deviceAddress, DS90UB9x.ID);
                ConfigureDeserializer(device);
                ConfigureBno055(device);
                var deviceInfo = new DeviceInfo(context, DeviceType, deviceAddress);
                return DeviceManager.RegisterDevice(deviceName, deviceInfo);
            });
        }

        static void ConfigureDeserializer(DeviceContext device)
        {
            // configure deserializer I2C aliases
            var deserializer = new I2CRegisterContext(device, DS90UB9x.DES_ADDR);
            uint alias = NeuropixelsV1eBno055.BNO055Address << 1;
            deserializer.WriteByte((uint)DS90UB9xDeserializerI2CRegister.SlaveID4, alias);
            deserializer.WriteByte((uint)DS90UB9xDeserializerI2CRegister.SlaveAlias4, alias);
        }

        static void ConfigureBno055(DeviceContext device)
        {
            // setup BNO055 device
            var i2c = new I2CRegisterContext(device, NeuropixelsV1eBno055.BNO055Address);
            i2c.WriteByte(0x3E, 0x00); // Power mode normal
            i2c.WriteByte(0x07, 0x00); // Page ID address 0
            i2c.WriteByte(0x3F, 0x00); // Internal oscillator
            i2c.WriteByte(0x41, 0b00010010);  // Axis map config (configured to match hs64; X => -Z, Y => X, Z => -Y)
            i2c.WriteByte(0x42, 0b00000011); // Axis sign (negate Y and Z)
            i2c.WriteByte(0x3D, 8); // Operation mode is NOF
        }
    }

    static class NeuropixelsV1eBno055
    {
        public const int BNO055Address = 0x28;
        public const int DataAddress = 0x1A;

        internal class NameConverter : DeviceNameConverter
        {
            public NameConverter()
                : base(typeof(NeuropixelsV1eBno055))
            {
            }
        }
    }
}
