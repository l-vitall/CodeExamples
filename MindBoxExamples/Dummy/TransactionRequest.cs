namespace MindBoxExamples.Dummy
{
    public class TransactionRequest
    {
        public decimal Amount { get; set; }
        public string AssetId { get; set; }
        public string DestinationAddress { get; set; }
        public string TransferId { get; set; }
        public long AccountId { get; set; }
    }
}