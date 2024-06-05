﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Bonsai;
using OpenCV.Net;

namespace OpenEphys.Onix
{
    public class NeuropixelsV1eData : Source<NeuropixelsV1eDataFrame>
    {
        [TypeConverter(typeof(NeuropixelsV1e.NameConverter))]
        public string DeviceName { get; set; }

        int bufferSize = 36;
        [Description("Number of super-frames (384 channels from spike band and 32 channels from " +
            "LFP band) to buffer before propogating data. Must be a mulitple of 12.")]
        public int BufferSize
        {
            get => bufferSize;
            set => bufferSize = (int)(Math.Ceiling((double)value / NeuropixelsV1.FramesPerRoundRobin) * NeuropixelsV1.FramesPerRoundRobin);
        }

        public unsafe override IObservable<NeuropixelsV1eDataFrame> Generate()
        {
            var spikeBufferSize = BufferSize;
            var lfpBufferSize = spikeBufferSize / NeuropixelsV1.FramesPerRoundRobin;

            var bufferSize = BufferSize;
            return Observable.Using(
                () => DeviceManager.ReserveDevice(DeviceName),
                disposable => disposable.Subject.SelectMany(deviceInfo =>
                    Observable.Create<NeuropixelsV1eDataFrame>(observer =>
                    {
                        var sampleIndex = 0;
                        var device = deviceInfo.GetDeviceContext(typeof(NeuropixelsV1e));
                        var spikeBuffer = new ushort[NeuropixelsV1.ChannelCount, spikeBufferSize];
                        var lfpBuffer = new ushort[NeuropixelsV1.ChannelCount, lfpBufferSize];
                        var frameCountBuffer = new int[spikeBufferSize * NeuropixelsV1.FramesPerSuperframe];
                        var hubClockBuffer = new ulong[spikeBufferSize];
                        var clockBuffer = new ulong[spikeBufferSize];

                        var frameObserver = Observer.Create<oni.Frame>(
                            frame =>
                            {
                                var payload = (NeuropixelsV1fPayload*)frame.Data.ToPointer();
                                NeuropixelsV1eDataFrame.CopyAmplifierBuffer(payload->AmplifierData, frameCountBuffer, spikeBuffer, lfpBuffer, sampleIndex);
                                hubClockBuffer[sampleIndex] = payload->HubClock;
                                clockBuffer[sampleIndex] = frame.Clock;

                                if (++sampleIndex >= spikeBufferSize)
                                {
                                    var spikeData = Mat.FromArray(spikeBuffer);
                                    var lfpData = Mat.FromArray(lfpBuffer);
                                    observer.OnNext(new NeuropixelsV1eDataFrame(clockBuffer, hubClockBuffer, frameCountBuffer, spikeData, lfpData));
                                    frameCountBuffer = new int[spikeBufferSize * NeuropixelsV1.FramesPerSuperframe];
                                    hubClockBuffer = new ulong[spikeBufferSize];
                                    clockBuffer = new ulong[spikeBufferSize];
                                    sampleIndex = 0;
                                }
                            },
                            observer.OnError,
                            observer.OnCompleted);
                        return deviceInfo.Context.FrameReceived
                            .Where(frame => frame.DeviceAddress == device.Address)
                            .SubscribeSafe(frameObserver);
                    })));
        }
    }
}
