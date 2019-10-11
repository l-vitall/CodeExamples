using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MindBoxExamples.Dummy
{
    public interface IEthereumDatabaseHelper
    {
        Task<IEnumerable<EthereumTransfer>> GetTransfers(TransferType transactionHash, object o, object o1, EthereumTransferStatus @new, EthereumTransferStatus sendingWithdrawal);
        Task AddTransferNoDuplicates(EthereumTransfer transfer);
        Task UpdateTransfer(long transferId, EthereumTransferStatus? status = null, string transactionHash = null, string errorText = null, long? blockNumber = null, DateTime? blockTime = null,
            string sweepTransactionHash = null, string erc20UnlockTransactionHash = null);
    }
}