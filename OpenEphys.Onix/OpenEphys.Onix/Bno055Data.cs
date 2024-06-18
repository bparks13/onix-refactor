﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using Bonsai;

namespace OpenEphys.Onix
{
    public class Bno055Data : Source<Bno055DataFrame>
    {
        [TypeConverter(typeof(Bno055.NameConverter))]
        public string DeviceName { get; set; }

        public override IObservable<Bno055DataFrame> Generate()
        {
            return Observable.Using(
                () => DeviceManager.ReserveDevice(DeviceName),
                disposable => disposable.Subject.SelectMany(deviceInfo =>
                {
                    var device = deviceInfo.GetDeviceContext(typeof(Bno055));
                    return deviceInfo.Context
                        .GetDeviceFrames(device.Address)
                        .Select(frame => new Bno055DataFrame(frame));
                }));
        }
    }
}
