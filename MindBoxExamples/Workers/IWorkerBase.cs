using System.Threading.Tasks;
using MindBoxExamples.Dummy;
using Serilog;

namespace MindBoxExamples.Workers
{
    public interface IWorkerBase
    {
        bool IsInitialized { get; }
        PaymentSystem PaymentSystem { get; }
        void Dispose();
        Task Init();
        void Start();
        void Stop();

        /// <summary>
        /// Blocks the current thread until worker and subworkers are fully stopped.
        /// </summary>
        /// <returns></returns>
        Task WaitForCompletion();

        ILogger GetLogger();
    }
}