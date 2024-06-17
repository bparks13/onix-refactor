﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Bonsai;

namespace OpenEphys.Onix
{
    public class NeuropixelsV1eBno055Data : Source<Bno055DataFrame>
    {
        [TypeConverter(typeof(NeuropixelsV1eBno055.NameConverter))]
        public string DeviceName { get; set; }

        public override IObservable<Bno055DataFrame> Generate()
        {
            // Max of 100 Hz, but limited by I2C bus
            var source = Observable.Interval(TimeSpan.FromSeconds(0.01));
            return Generate(source);
        }

        public unsafe IObservable<Bno055DataFrame> Generate<TSource>(IObservable<TSource> source)
        {
            return Observable.Using(
                () => DeviceManager.ReserveDevice(DeviceName),
                disposable => disposable.Subject.SelectMany(
                    deviceInfo => Observable.Create<Bno055DataFrame>(observer =>
                    {
                        var device = deviceInfo.GetDeviceContext(typeof(NeuropixelsV1eBno055));
                        var passthrough = device.GetPassthroughDeviceContext(DS90UB9x.ID);
                        var i2c = new I2CRegisterContext(passthrough, NeuropixelsV1eBno055.BNO055Address);

                        var pollingObserver = Observer.Create<TSource>(
                            _ =>
                            {
                                Bno055DataFrame frame = default;
                                device.Context.EnsureContext(() =>
                                {
                                    var data = i2c.ReadBytes(NeuropixelsV1eBno055.DataAddress, sizeof(Bno055DataPayload));
                                    ulong clock = passthrough.ReadRegister(DS90UB9x.LASTI2CL);
                                    clock += (ulong)passthrough.ReadRegister(DS90UB9x.LASTI2CH) << 32;
                                    fixed (byte* dataPtr = data)
                                    {
                                        frame = new Bno055DataFrame(clock, (Bno055DataPayload*)dataPtr);
                                    }
                                });

                                if (frame != null)
                                {
                                    observer.OnNext(frame);
                                }
                            },
                            observer.OnError,
                            observer.OnCompleted);
                        return source.SubscribeSafe(pollingObserver);
                    })));
        }
    }
}
