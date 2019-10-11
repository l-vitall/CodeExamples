using System;

namespace MindBoxExamples.Dummy
{
    public class EthereumTransfer
    {
        public TransferType TransferType { get; set; }
        public string TransactionHash { get; set; }
        public EthereumTransferStatus Status { get; set; }
        public string DestinationAddress { get; set; }
        public string AssetId { get; set; }
        public decimal AmountConverted { get; set; }
        public long Id { get; set; }
        public string TransferId { get; set; }
        public long AccountId { get; set; }
        public string ErrorText { get; set; }
    }
}