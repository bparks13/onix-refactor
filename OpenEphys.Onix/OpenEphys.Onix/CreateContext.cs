using Bonsai;
using System;
using System.ComponentModel;
using System.Reactive.Linq;

namespace OpenEphys.Onix
{
    /// <summary>
    /// Creates the context in which all devices are contained. The driver to be used,
    /// and the index, are available here.
    /// </summary>
    [Description("")]
    [Combinator(MethodName = nameof(Generate))]
    [WorkflowElementCategory(ElementCategory.Source)]
    public class CreateContext
    {
        /// <summary>
        /// Represents the driver that is used to communicate with hardware. Possible values are
        /// "riffa", "test", and "ft600"
        /// </summary>
        public string Driver { get; set; } = "riffa";

        /// <summary>
        /// Represents the index specifying the physical slot occupied by hardware being controlled
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Generates the <see cref="ContextTask"/> using the provided driver and index
        /// </summary>
        /// <returns></returns>
        public IObservable<ContextTask> Generate()
        {
            return Observable.Create<ContextTask>(observer =>
            {
                var driver = Driver;
                var index = Index;
                var context = new ContextTask(driver, index);
                try
                {
                    observer.OnNext(context);
                    return context;
                }
                catch
                {
                    context.Dispose();
                    throw;
                }
            });
        }
    }
}
