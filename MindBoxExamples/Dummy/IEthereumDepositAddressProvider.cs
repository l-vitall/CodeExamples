namespace MindBoxExamples.Dummy
{
    public interface IEthereumDepositAddressProvider
    {
        bool IsInitialized { get; set; }
        void Initialize();
    }
}