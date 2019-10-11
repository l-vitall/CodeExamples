using System.Threading;
using System.Threading.Tasks;

namespace MindBoxExamples.Dummy
{
    public class EthereumBlockchainManager
    {
        public EthereumBlockchainManager(EthereumOptions options, string optionsHotWalletPrivateKey, IEthereumDatabaseHelper databaseHelper)
        {
            throw new System.NotImplementedException();
        }

        public async Task<string> SendTransferAsync(EthereumTransfer currentTransfer, CancellationToken cancellationToken, bool failOnInsufficientGas, bool validateTargetAddress)
        {
            throw new System.NotImplementedException();
        }
    }
}