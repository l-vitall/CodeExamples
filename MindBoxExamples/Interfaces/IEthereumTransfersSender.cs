using System;
using System.Threading.Tasks;
using MindBoxExamples.Dummy;
using MindBoxExamples.Workers;

namespace MindBoxExamples.Interfaces
{
    public interface IEthereumTransfersSender : IWorkerBase
    {
        Task EnqueueWithdrawal(TransactionRequest request);

        event Func<BackOfficeTransferRequest, TransferResult> TransferCallback;
    }
}