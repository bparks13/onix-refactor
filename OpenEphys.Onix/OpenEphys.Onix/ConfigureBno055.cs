using System;
using System.ComponentModel;

namespace OpenEphys.Onix
{
    /// <summary>
    /// Sink node that configures a BNO055 device.
    /// </summary>
    public class ConfigureBno055 : SingleDeviceFactory
    {
        /// <summary>
        /// Default constructor for a BNO055
        /// </summary>
        public ConfigureBno055()
            : base(typeof(Bno055))
        {
        }

        /// <summary>
        /// Specifies if the BNO055 device is enabled. If it is set to true,
        /// this device is available to stream data while the workflow is running.
        /// </summary>
        [Category(ConfigurationCategory)]
        [Description("Specifies whether the BNO055 device is enabled.")]
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Main processing function that runs when a workflow is started. If the Enable property is true,
        /// the device will be enabled at this point.
        /// </summary>
        /// <param name="source">Observable that holds a <see cref="ContextTask"/></param>
        /// <returns></returns>
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
