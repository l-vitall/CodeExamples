using System;
using System.Threading;
using System.Threading.Tasks;
using MindBoxExamples.Dummy;
using Serilog;

namespace MindBoxExamples.Workers
{
    public class BackgroundWorker : WorkerBase
    {
        private readonly Func<ILogger, CancellationToken, Task> _callbackDelegate;

        public BackgroundWorker(string workerName, PaymentSystem paymentSystem, Func<ILogger, CancellationToken, Task> callbackDelegate) : base(paymentSystem, workerName)
        {
            _callbackDelegate = callbackDelegate;
        }

        protected override async Task OnDoWork(ILogger logger, CancellationToken ct)
        {
            await _callbackDelegate(logger, ct);
        }
    }
}