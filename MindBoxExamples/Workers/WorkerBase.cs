using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MindBoxExamples.Dummy;
using Serilog;

namespace MindBoxExamples.Workers
{
    public abstract class WorkerBase : IWorkerBase
    {
        protected string WorkerName { get; }
        public PaymentSystem PaymentSystem { get; }
        private readonly ILogger _logger;

        private Task _workerTask;
        private readonly List<WorkerBase> _subWorkers = new List<WorkerBase>();
        private CancellationTokenSource _cancellationTokenSource;

        public bool IsInitialized { get; protected set; }

        protected WorkerBase(PaymentSystem paymentSystem, string workerName = null)
        {
            WorkerName = workerName ?? GetType().Name;
            PaymentSystem = paymentSystem;
            _logger = Log.Logger.ForContext("WalletEx", $"{paymentSystem.ToString()}.{workerName}");
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            _workerTask?.Dispose();
            _subWorkers.ForEach(s => s.Dispose());
        }

        public async Task Init()
        {
            if (IsInitialized)
                throw new InvalidOperationException("Already initialized");

            LogInfo($"{WorkerName}: Initializing..");

            try
            {
                var initTasks = new List<Task>(){OnInit()};
                _subWorkers.ForEach(s=>initTasks.Add(s.Init()));
                await Task.WhenAll(initTasks);

                IsInitialized = true;
            }
            catch (Exception e)
            {
                LogError(e, "Initialization failed");
                throw;
            }

            LogInfo($"{WorkerName}: Initialized.");
        }

        public ILogger GetLogger()
        {
            return _logger;
        }

        protected virtual async Task OnInit()
        {
            await Task.Delay(0);
        }

        public void Start()
        {
            if (!IsInitialized)
                Init().GetAwaiter().GetResult();

            if (!IsInitialized)
                throw new InvalidOperationException($"Service is not initialized");

            _cancellationTokenSource = new CancellationTokenSource();

            if (_workerTask == null)
                LogInfo($"{WorkerName}: Starting..");
            else
                LogInfo($"{WorkerName}: Restarting..");

            OnStart();

            _workerTask = Task.Run(() => DoWork(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            _subWorkers.ForEach(s => s.Start());

            LogInfo($"{WorkerName}: Started.");
        }

        protected virtual void OnStart()
        {
        }

        public void Stop()
        {
            if (_workerTask == null)
                throw new InvalidOperationException("Workers are not started");

            LogInfo($"{WorkerName}: Signalling to stop..");
            _cancellationTokenSource.Cancel();

            OnStop();
            _subWorkers.ForEach(s => s.Stop());

            LogInfo($"{WorkerName}: Processed signal to stop.");
        }

        protected virtual void OnStop()
        {
        }

        public async Task WaitForCompletion()
        {
            LogInfo($"{WorkerName}: Waiting for service to stop..");
            try
            {
                var waitTasks = new List<Task>() { _workerTask };
                _subWorkers.ForEach(s => waitTasks.Add(s.WaitForCompletion()));
                await Task.WhenAll(waitTasks);

                LogInfo($"{WorkerName}: Stopped. Really.");
            }
            catch (AggregateException e)
            {
                LogInfo($"{WorkerName}:\nStopping failed: AggregateException thrown with the following inner exceptions:");
                // Display information about each exception.
                foreach (var v in e.InnerExceptions)
                {
                    if (v is TaskCanceledException tce)
                        LogInfo("   TaskCanceledException: Task " + tce.Task.Id);
                    else
                        LogInfo("   Exception: " + v.GetType().Name);
                }
            }
        }

        protected void AddSubWorker(string subworkerName, Func<ILogger, CancellationToken, Task> onDoWorkAction)
        {
            if (subworkerName == null)
                throw new ArgumentNullException(nameof(subworkerName));

            _subWorkers.Add(new BackgroundWorker($"{WorkerName}.{subworkerName}", PaymentSystem, onDoWorkAction));
        }

        protected int GetRemainRefreshDataIntervalMs(DateTime cycleBeginTime, int intervalMs)
        {
            var cycleMs = (int)(DateTime.UtcNow - cycleBeginTime).TotalMilliseconds;
            if (cycleMs < intervalMs)
                return intervalMs - cycleMs;

            return 0;
        }

        protected abstract Task OnDoWork(ILogger logger, CancellationToken cancellationToken);

        private async Task DoWork(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                LogInfo($"{WorkerName}: DoWork was cancelled before it got started.");
                cancellationToken.ThrowIfCancellationRequested();
            }

            try
            {
                await OnDoWork(_logger, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                LogInfo("OnDoWork terminated by TaskCanceledException.");
            }
            catch (Exception e)
            {
                LogError(e, "OnDoWork failed and terminated.");
            }

            LogInfo($"{WorkerName}: DoWork exited.");
        }

        public void LogDebug(string info, params object[] args)
        {
            _logger.Debug(info, args);
        }

        public void LogInfo(string info, params object[] args)
        {
            _logger.Information(info, args);
        }

        public void LogError(string info, params object[] args)
        {
            _logger.Error(info, args);
        }

        public void LogError(Exception ex, string info, params object[] args)
        {
            _logger.Error(ex, info, args);
        }
    }
}