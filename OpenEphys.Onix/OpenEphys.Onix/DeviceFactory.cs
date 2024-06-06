using System;
using System.Collections.Generic;
using System.ComponentModel;
using Bonsai;

namespace OpenEphys.Onix
{
    /// <summary>
    /// Abstract class that implements a <see cref="Sink{ContextTask}"/> node.
    /// </summary>
    public abstract class DeviceFactory : Sink<ContextTask>
    {
        internal const string ConfigurationCategory = "Configuration";
        internal const string AcquisitionCategory = "Acquisition";

        internal abstract IEnumerable<IDeviceConfiguration> GetDevices();
    }

    /// <summary>
    /// Abstract class that implements a <see cref="DeviceFactory"/> for a single device.
    /// </summary>
    public abstract class SingleDeviceFactory : DeviceFactory, IDeviceConfiguration
    {
        internal SingleDeviceFactory(Type deviceType)
        {
            DeviceType = deviceType ?? throw new ArgumentNullException(nameof(deviceType));
        }

        /// <summary>
        /// Represents the name of the current device. Device names must be unique across the workflow,
        /// as they are used to link input/output nodes to a specific configured device.
        /// </summary>
        [Description("The name of the device. Must be unique.")]
        public string DeviceName { get; set; }

        /// <summary>
        /// Represents the address of the current device. Device addresses must be unique across the workflow.
        /// </summary>
        [Description("Address of the device in the device table.")]
        public uint DeviceAddress { get; set; }

        /// <summary>
        /// Represents the type of the current device.
        /// </summary>
        [Browsable(false)]
        public Type DeviceType { get; }

        internal override IEnumerable<IDeviceConfiguration> GetDevices()
        {
            yield return this;
        }
    }
}
