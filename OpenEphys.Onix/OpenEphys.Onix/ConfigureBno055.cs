﻿using System;
using System.ComponentModel;

namespace OpenEphys.Onix
{
    [Editor("OpenEphys.Onix.Design.Bno055Editor, OpenEphys.Onix.Design", typeof(ComponentEditor))]
    public class ConfigureBno055 : SingleDeviceFactory
    {
        public ConfigureBno055()
            : base(typeof(Bno055))
        {
        }

        public ConfigureBno055(ConfigureBno055 configureBno055)
            : base(typeof(Bno055))
        {
            Enable = configureBno055.Enable;
        }

        [Category(ConfigurationCategory)]
        [Description("Specifies whether the BNO055 device is enabled.")]
        public bool Enable { get; set; } = true;

        public override IObservable<ContextTask> Process(IObservable<ContextTask> source)
        {
            var deviceName = DeviceName;
            var deviceAddress = DeviceAddress;
            return source.ConfigureDevice(context =>
            {
                var device = context.GetDeviceContext(deviceAddress, Bno055.ID);
                device.WriteRegister(Bno055.ENABLE, Enable ? 1u : 0);
                return DeviceManager.RegisterDevice(deviceName, device, DeviceType);
            });
        }
    }

    static class Bno055
    {
        public const int ID = 9;

        // constants
        public const float EulerAngleScale = 1f / 16; // 1 degree = 16 LSB
        public const float QuaternionScale = 1f / (1 << 14); // 1 = 2^14 LSB
        public const float AccelerationScale = 1f / 100; // 1m / s^2 = 100 LSB

        // managed registers
        public const uint ENABLE = 0x0; // Enable or disable the data output stream

        internal class NameConverter : DeviceNameConverter
        {
            public NameConverter()
                : base(typeof(Bno055))
            {
            }
        }
    }
}
