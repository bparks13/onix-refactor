using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Bonsai;

namespace OpenEphys.Onix
{
    /// <summary>
    /// Combinator node that takes in a <see cref="ContextTask"/> and returns an 
    /// <see cref="oni.Frame"/> by starting acquisition of data during runtime.
    /// </summary>
    public class StartAcquisition : Combinator<ContextTask, oni.Frame>
    {
        /// <summary>
        /// Number of bytes read during each driver access to the high-bandwidth read channel. 
        /// This option allows control over the trade-off between closed-loop response time and 
        /// overall bandwidth. The minimum value will provide the lowest response latency. 
        /// Larger values will reduce frequency of reads, and may improve processing performance 
        /// for high-bandwidth data sources. This minimum size of this option is determined by 
        /// oni.Context.MaxReadFrameSize
        /// </summary>
        /// <inheritdoc/>
        [Description("Number of bytes allocated for reading data.")]
        public int ReadSize { get; set; } = 2048;

        /// <summary>
        /// Number of bytes pre-allocated for calls to write data. A larger size will reduce the
        /// average amount of dynamic memory allocation system calls but will increase the cost 
        /// of each of those calls. The minimum size of this option is determined by oni.Context.MaxWriteFrameSize.
        /// </summary>
        [Description("Number of bytes allocated for writing data.")]
        public int WriteSize { get; set; } = 2048;

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public override IObservable<oni.Frame> Process(IObservable<ContextTask> source)
        {
            return source.SelectMany(context =>
            {
                return Observable.Create<oni.Frame>(observer =>
                {
                    var disposable = context.FrameReceived.SubscribeSafe(observer);
                    try
                    {
                        context.Start(ReadSize, WriteSize);
                    }
                    catch
                    {
                        disposable.Dispose();
                        throw;
                    }
                    return Disposable.Create(() =>
                    {
                        context.Stop();
                        disposable.Dispose();
                    });
                });
            });
        }
    }
}
