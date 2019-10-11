namespace MindBoxExamples.Dummy
{
    public class BackOfficeTransferRequest
    {
        public string TransferId { get; set; }
        public string AssetId { get; set; }
        public decimal Amount { get; set; }
        public string TargetAccount { get; set; }
    }
}