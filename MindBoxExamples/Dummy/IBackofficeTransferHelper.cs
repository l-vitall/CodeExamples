namespace MindBoxExamples.Dummy
{
    public interface IBackofficeTransferHelper
    {
        BackOfficeTransferRequest CreateBackofficeTransferRequest(EthereumTransfer transfer, object confirmationsRequired);
    }
}