using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MindBoxExamples.Dummy;
using MindBoxExamples.Interfaces;
using MindBoxExamples.Workers;
using Serilog;

namespace MindBoxExamples
{
    public class EthereumTransfersSender : WorkerBase, IEthereumTransfersSender
    {
        private const int InvokeTransferCallbackRetryIntervalMs = 3000;
        private const int NewWithdrawalsWaitIntervalMs = 5000;


        private readonly IEthereumDatabaseHelper _databaseHelper;
        private readonly EthereumOptions _options;
        private readonly IBackofficeTransferHelper _backofficeTransferHelper;
        private readonly IEthereumDepositAddressProvider _depositAddressProvider;

        private ConcurrentDictionary<long, EthereumTransfer> _transfersToSend = new ConcurrentDictionary<long, EthereumTransfer>();

        private EthereumBlockchainManager _hotWalletSender;

        public event Func<BackOfficeTransferRequest, TransferResult> TransferCallback;

        public EthereumTransfersSender(IEthereumDatabaseHelper databaseHelper, EthereumOptions options,
            IBackofficeTransferHelper backofficeTransferHelper,
            IEthereumDepositAddressProvider depositAddressProvider) : base(PaymentSystem.Ethereum)
        {
            _databaseHelper = databaseHelper;
            _options = options;
            _backofficeTransferHelper = backofficeTransferHelper;
            _depositAddressProvider = depositAddressProvider;

            _hotWalletSender = new EthereumBlockchainManager(options, _options.HotWalletPrivateKey, _databaseHelper);
        }

        protected override async Task OnInit()
        {
            LogInfo("Starting {@WorkerName}.OnInit()... ", WorkerName);

            var inProgressTransfers = await _databaseHelper.GetTransfers(TransferType.Withdrawal, null, null, EthereumTransferStatus.New, EthereumTransferStatus.SendingWithdrawal);
            foreach (var transfer in inProgressTransfers)
            {
                if (transfer.TransferType == TransferType.Withdrawal)
                {
                    if (transfer.Status == EthereumTransferStatus.New || transfer.Status == EthereumTransferStatus.SendingWithdrawal)
                    {
                        LogInfo(_transfersToSend.TryAdd(transfer.Id, transfer)
                                ? "Found and enqueued unprocessed withdrawal: {@Transfer}"
                                : "Found unprocessed withdrawal is already in queue: {@Transfer}", transfer);
                    }
                    else
                        throw new ArgumentException($"Broken withdrawal transfer found - Status cannot be other than New. TransferId: {transfer.TransferId}; status: {transfer.Status}");
                }
            }

            if (!_depositAddressProvider.IsInitialized)
            {
                LogInfo("Initializing _depositAddressProvider...");
                _depositAddressProvider.Initialize();
                LogInfo("_depositAddressProvider initialized.");
            }

            LogInfo("{@WorkerName}.OnInit() done.", WorkerName);
        }

        public async Task EnqueueWithdrawal(TransactionRequest request)
        {
            var transfer = new EthereumTransfer()
            {
                AmountConverted = request.Amount,
                AssetId = request.AssetId,
                DestinationAddress = request.DestinationAddress.ToLower(),
                TransferId = request.TransferId,
                TransferType = TransferType.Withdrawal,
                Status = EthereumTransferStatus.New,
                AccountId = request.AccountId
            };

            await _databaseHelper.AddTransferNoDuplicates(transfer);

            if (transfer.Id == 0)
                LogError("Withdrawal with transferId = {@TransferId} already exists in database and cannot be re-scheduled", request.TransferId);
            else
            {
                if (!_transfersToSend.TryAdd(transfer.Id, transfer))
                    LogError("Failed to enqueue transfer request for withdrawal: {@TransferForWithdrawal}", transfer);
                else
                    LogInfo("Enqueued transfer request for withdrawal: {@TransferForWithdrawal}", transfer);
            }
        }

        protected override async Task OnDoWork(ILogger logger, CancellationToken cancellationToken)
        {
            EthereumTransfer currentTransfer = null;
            do
            {
                foreach (var transferPair in _transfersToSend)
                {
                    currentTransfer = transferPair.Value;

                    if (currentTransfer.Status == EthereumTransferStatus.SendingWithdrawal)
                    {
                        if (!string.IsNullOrWhiteSpace(currentTransfer.TransactionHash))
                        {
                            LogInfo("Found SendingWithdrawal transferId = {@TransferId} with known txHashId = {@TxHashId}. Marking it as succeed even if it is not exist",
                                currentTransfer.TransferId, currentTransfer.TransactionHash);

                            currentTransfer.Status = EthereumTransferStatus.SentWithdrawal;
                            await _databaseHelper.UpdateTransfer(transferPair.Key, currentTransfer.Status);
                            _transfersToSend.TryRemove(transferPair.Key, out _);
                            continue;
                        };
                    }

                    if (!string.IsNullOrWhiteSpace(currentTransfer.TransactionHash))
                    {
                        LogError("Attempted to process withdrawal transfer that is already processed: {@Transfer}", currentTransfer);
                        _transfersToSend.TryRemove(transferPair.Key, out _);
                        continue;
                    }

                    await _databaseHelper.UpdateTransfer(transferPair.Key, EthereumTransferStatus.SendingWithdrawal); //to prevent withdrawal duplicating on database failure

                    currentTransfer.TransactionHash = await _hotWalletSender.SendTransferAsync(currentTransfer, cancellationToken, true, true);

                    if (!string.IsNullOrWhiteSpace(currentTransfer.TransactionHash)) //ok
                    {
                        currentTransfer.Status = EthereumTransferStatus.SentWithdrawal;
                        await _databaseHelper.UpdateTransfer(transferPair.Key, currentTransfer.Status, transactionHash: currentTransfer.TransactionHash);
                        _transfersToSend.TryRemove(transferPair.Key, out _);
                    }
                    else if (!cancellationToken.IsCancellationRequested) //not sent
                    {
                        await DequeueSendTransferAsync(currentTransfer, EthereumTransferStatus.Failed);

                        LogError("Failed withdrawal transfer: {@Transfer}", currentTransfer);

                        InvokeBackofficeCallback(currentTransfer, cancellationToken);
                    }
                }
            } while (!cancellationToken.WaitHandle.WaitOne(NewWithdrawalsWaitIntervalMs));
        }

        private async Task DequeueSendTransferAsync(EthereumTransfer transfer, EthereumTransferStatus newStatus, string errorText = null)
        {
            transfer.Status = newStatus;

            if (!string.IsNullOrWhiteSpace(errorText))
            {
                if (transfer.ErrorText == null || transfer.ErrorText.Length < 4000)
                {
                    transfer.ErrorText = (transfer.ErrorText ?? string.Empty) + errorText;
                    await _databaseHelper.UpdateTransfer(transfer.Id, transfer.Status, errorText: transfer.ErrorText);
                }
            }
            else
                await _databaseHelper.UpdateTransfer(transfer.Id, transfer.Status);

            _transfersToSend.TryRemove(transfer.Id, out _);
        }

        private void InvokeBackofficeCallback(EthereumTransfer transfer, CancellationToken cancellationToken)
        {
            TransferResult transferResult = null;
            do
            {
                try
                {
                    var backofficeRequest = _backofficeTransferHelper.CreateBackofficeTransferRequest(transfer, _options.ConfirmationsRequired);

                    transferResult = TransferCallback.Invoke(backofficeRequest);

                    if (transferResult.HasException)
                        throw transferResult.Exception;

                    LogInfo("Withdrawal transfer {@TransferId} with assetId = {@AssetId} and amountEth = {@Amount} to {@ToAddress} failed to be sent",
                        backofficeRequest.TransferId,
                        backofficeRequest.AssetId, backofficeRequest.Amount, backofficeRequest.TargetAccount);
                }
                catch (Exception ex)
                {
                    LogError(ex, "TransferCallback.Invoke failed");
                }
            } while (transferResult == null && !cancellationToken.WaitHandle.WaitOne(InvokeTransferCallbackRetryIntervalMs));
        }
    }
}