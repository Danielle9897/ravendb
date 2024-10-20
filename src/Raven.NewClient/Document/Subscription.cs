// -----------------------------------------------------------------------
//  <copyright file="Subscription.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Raven.NewClient.Abstractions.Data;
using Raven.NewClient.Abstractions.Exceptions.Subscriptions;
using Raven.NewClient.Abstractions.Extensions;
using Raven.NewClient.Abstractions.Logging;
using Raven.NewClient.Abstractions.Util;
using Raven.NewClient.Client.Blittable;
using Raven.NewClient.Client.Commands;
using Raven.NewClient.Client.Extensions;
using Raven.NewClient.Client.Json;
using Raven.NewClient.Client.Util;
using Sparrow.Json;


namespace Raven.NewClient.Client.Document
{
    public delegate void BeforeBatch();

    public delegate void AfterBatch(int documentsProcessed);

    public delegate void BeforeAcknowledgment();

    public delegate void AfterAcknowledgment();

    public class Subscription<T> : IObservable<T>, IDisposableAsync, IDisposable where T : class
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Subscription<T>));
        private readonly IDocumentStore _store;
        private readonly DocumentConvention _conventions;
        private readonly string _dbName;
        private readonly CancellationTokenSource _proccessingCts = new CancellationTokenSource();
        private readonly GenerateEntityIdOnTheClient _generateEntityIdOnTheClient;
        private readonly SubscriptionConnectionOptions _options;
        private readonly List<IObserver<T>> _subscribers = new List<IObserver<T>>();
        private TcpClient _tcpClient;
        private bool _completed, _started;
        private bool _disposed;
        private Task _subscriptionTask;
        private NetworkStream _networkStream;
        private readonly TaskCompletionSource<object> _disposedTask = new TaskCompletionSource<object>();

        internal Subscription(SubscriptionConnectionOptions options, IDocumentStore documentStore, DocumentConvention conventions, string dbName)
        {
            _options = options;
            if (_options.SubscriptionId == 0)
                throw new ArgumentException("SubscriptionConnectionOptions must specify the SubscriptionId, but was set to zero.",
                    nameof(options));
            _store = documentStore;
            _conventions = conventions;
            _dbName = dbName;

            _generateEntityIdOnTheClient = new GenerateEntityIdOnTheClient(conventions,
                entity => { throw new InvalidOperationException("Shouldn't be generating new ids here"); });
        }

        ~Subscription()
        {
            if (_disposed) return;
            try
            {
                CloseTcpClient();
                Logger.Warn($"Subscription {_options.SubscriptionId} was not disposed properly");
                //write to log
            }
            catch
            {

            }
        }
        /// <summary>
        ///     It indicates if the subscription is in errored state because one of subscribers threw an exception.
        /// </summary>
        public bool IsErroredBecauseOfSubscriber { get; private set; }

        /// <summary>
        ///     The last exception thrown by one of subscribers.
        /// </summary>
        public Exception LastSubscriberException { get; private set; }

        /// <summary>
        ///     It determines if the subscription connection is closed.
        /// </summary>
        public bool IsConnectionClosed { get; private set; }

        public void Dispose()
        {
            if (_disposed)
                return;

            AsyncHelpers.RunSync(DisposeAsync);
        }

        public async Task DisposeAsync()
        {
            try
            {
                if (_disposed)
                    return;

                _disposed = true;
                _proccessingCts.Cancel();
                _disposedTask.TrySetResult(null); // notify the subscription task that we are done

                if (_subscriptionTask != null && Task.CurrentId != _subscriptionTask.Id)
                {
                    try
                    {
                        await _subscriptionTask;
                    }
                    catch (Exception)
                    {
                        // just need to wait for it to end
                    }
                }

                CloseTcpClient(); // we disconnect immediately, freeing the subscription task

                OnCompletedNotification();
            }
            catch (Exception ex)
            {
                Logger.Warn("Error during dispose of subscription", ex);
            }
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            if (_started)
                throw new InvalidOperationException(
                    "You can only add observers to a subscriptions before you started it");

            if (IsErroredBecauseOfSubscriber)
                throw new InvalidOperationException(
                    "Subscription encountered errors and stopped. Cannot add any subscriber.");
            _subscribers.Add(observer);

            // we cannot remove subscriptions dynamically, once we added, it is done
            return new DisposableAction(() => { });
        }

        public event BeforeBatch BeforeBatch = delegate { };
        public event AfterBatch AfterBatch = delegate { };
        public event BeforeAcknowledgment BeforeAcknowledgment = delegate { };
        /// <summary>
        /// allows the user to define stuff that happens after the confirm was recieved from the server (this way we know we won't
        /// get those documents again)
        /// </summary>
        public event AfterAcknowledgment AfterAcknowledgment = delegate { };

        public Task StartAsync()
        {
            if (_started)
                return Task.CompletedTask;

            if (_subscribers.Count == 0)
                throw new InvalidOperationException(
                    "No observers has been registered, did you forget to call Subscribe?");
            _started = true;
            var tcs = new TaskCompletionSource<object>();
            _subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    await RunSubscriptionAsync(tcs);
                }
                finally
                {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    if (_proccessingCts.IsCancellationRequested)
                    {
                        Task.Run(() => tcs.TrySetCanceled());
                    }
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
            });

            return tcs.Task;
        }

        private async Task<Stream> ConnectToServer()
        {
            var command = new GetTcpInfoCommand();

            JsonOperationContext context;
            var requestExecuter = _store.GetRequestExecuter(_dbName ?? _store.DefaultDatabase);
            requestExecuter.ContextPool.AllocateOperationContext(out context);

            await requestExecuter.ExecuteAsync(command, context).ConfigureAwait(false);
            var uri = new Uri(command.Result.Url);

            await _tcpClient.ConnectAsync(uri.Host, uri.Port).ConfigureAwait(false);

            _tcpClient.NoDelay = true;
            _tcpClient.SendBufferSize = 32 * 1024;
            _tcpClient.ReceiveBufferSize = 4096;
            _networkStream = _tcpClient.GetStream();

            var header = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new TcpConnectionHeaderMessage
            {
                Operation = TcpConnectionHeaderMessage.OperationTypes.Subscription,
                DatabaseName = _dbName ?? _store.DefaultDatabase
            }));

            var options = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_options));

            await _networkStream.WriteAsync(header, 0, header.Length);
            await _networkStream.WriteAsync(options, 0, options.Length);
            
            await _networkStream.FlushAsync();
            return _networkStream;
        }

        private void InformSubscribersOnError(Exception ex)
        {
            foreach (var subscriber in _subscribers)
            {
                try
                {
                    subscriber.OnError(ex);
                }
                catch (Exception e)
                {
                    Logger.WarnException(
                        string.Format(
                            "Subscription #{0}. Subscriber threw an exception while proccessing OnError " + e, _options.SubscriptionId), ex);
                }
            }
        }

        private void AssertConnectionState(SubscriptionConnectionServerMessage connectionStatus)
        {
            if (connectionStatus.Type != SubscriptionConnectionServerMessage.MessageType.CoonectionStatus)
                throw new Exception("Server returned illegal type message when excpecting connection status, was: " + connectionStatus.Type);

            switch (connectionStatus.Status)
            {
                case SubscriptionConnectionServerMessage.ConnectionStatus.Accepted:
                    break;
                case SubscriptionConnectionServerMessage.ConnectionStatus.InUse:
                    throw new SubscriptionInUseException(
                        $"Subscription With Id {this._options.SubscriptionId} cannot be opened, because it's in use and the connection strategy is {this._options.Strategy}");
                case SubscriptionConnectionServerMessage.ConnectionStatus.Closed:
                    throw new SubscriptionClosedException(
                        $"Subscription With Id {this._options.SubscriptionId} cannot be opened, because it was closed");
                case SubscriptionConnectionServerMessage.ConnectionStatus.NotFound:
                    throw new SubscriptionDoesNotExistException(
                        $"Subscription With Id {this._options.SubscriptionId} cannot be opened, because it does not exist");
                default:
                    throw new ArgumentException(
                        $"Subscription {this._options.SubscriptionId} could not be opened, reason: {connectionStatus.Status}");
            }
        }

        private async Task ProccessSubscription(TaskCompletionSource<object> successfullyConnected)
        {
            try
            {
                _proccessingCts.Token.ThrowIfCancellationRequested();
                using (var context = new JsonOperationContext(4096, 1024))
                {
                    using (var tcpStream = await ConnectToServer().ConfigureAwait(false))
                    using(var parser = context.ParseMultiFrom(tcpStream))
                    {
                        _proccessingCts.Token.ThrowIfCancellationRequested();
                        var readObjectTask = ReadNextObject(parser);
                        var done = await Task.WhenAny(readObjectTask, _disposedTask.Task).ConfigureAwait(false);
                        if (done == _disposedTask.Task)
                            return;
                        var connectionStatus = await readObjectTask.ConfigureAwait(false);

                        if (_proccessingCts.IsCancellationRequested)
                            return;

                        AssertConnectionState(connectionStatus);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        Task.Run(() => successfullyConnected.TrySetResult(null));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                        readObjectTask = ReadNextObject(parser);

                        if (_proccessingCts.IsCancellationRequested)
                            return;

                        var incomingBatch = new List<BlittableJsonReaderObject>();
                        long lastReceivedEtag = 0;

                        while (_proccessingCts.IsCancellationRequested == false)
                        {
                            BeforeBatch();
                            bool endOfBatch = false;
                            while (endOfBatch == false && _proccessingCts.IsCancellationRequested == false)
                            {
                                done = await Task.WhenAny(readObjectTask, _disposedTask.Task).ConfigureAwait(false);
                                if (done == _disposedTask.Task)
                                    break;
                                var receivedMessage = await readObjectTask.ConfigureAwait(false);
                                if (_proccessingCts.IsCancellationRequested)
                                    break;

                                readObjectTask = ReadNextObject(parser);

                                if (_proccessingCts.IsCancellationRequested)
                                    break;

                                switch (receivedMessage.Type)
                                {
                                    case SubscriptionConnectionServerMessage.MessageType.Data:
                                        incomingBatch.Add(receivedMessage.Data);
                                        break;
                                    case SubscriptionConnectionServerMessage.MessageType.EndOfBatch:
                                        endOfBatch = true;
                                        break;
                                    case SubscriptionConnectionServerMessage.MessageType.Confirm:
                                        AfterAcknowledgment();
                                        AfterBatch(incomingBatch.Count);
                                        incomingBatch.Clear();
                                        break;
                                    case SubscriptionConnectionServerMessage.MessageType.Error:
                                        switch (receivedMessage.Status)
                                        {
                                            case SubscriptionConnectionServerMessage.ConnectionStatus.Closed:
                                                throw new SubscriptionClosedException(receivedMessage.Exception ??
                                                                                      string.Empty);
                                            default:
                                                throw new Exception(
                                                    $"Connection terminated by server. Exception: {receivedMessage.Exception ?? "None"}");
                                        }

                                    default:
                                        throw new ArgumentException(
                                            $"Unrecognized message '{receivedMessage.Type}' type received from server");
                                }
                            }

                            foreach (var curDoc in incomingBatch)
                            {
                                NotifySubscribers(curDoc, out lastReceivedEtag);
                            }

                            SendAck(lastReceivedEtag, tcpStream);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                if (_proccessingCts.Token.IsCancellationRequested == false)
                    InformSubscribersOnError(ex);
                throw;
            }
        }

        private async Task<SubscriptionConnectionServerMessage> ReadNextObject(JsonOperationContext.MultiDocumentParser parser)
        {
            if (_proccessingCts.IsCancellationRequested || _tcpClient.Connected == false)
                    return null;
            var blittable = await parser.ParseToMemoryAsync("Subscription/next/object");
            
            return JsonDeserializationClient.SubscriptionNextObjectResult(blittable);
        }
        

        private void NotifySubscribers(BlittableJsonReaderObject curDoc, out long lastReceivedEtag)
        {
            BlittableJsonReaderObject metadata;
            string id;

            if (curDoc.TryGet(Constants.Metadata.Key, out metadata) == false)
                throw new InvalidOperationException("Document must have a metadata");
            if (metadata.TryGet(Constants.Metadata.Id, out id) == false)
                throw new InvalidOperationException("Document must have an id");
            if (metadata.TryGet(Constants.Metadata.Etag, out lastReceivedEtag) == false)
                throw new InvalidOperationException("Document must have an ETag");

            T instance;

            if (typeof(T) == typeof(BlittableJsonReaderObject))
            {
                instance = (T)(object)curDoc;
            }
            else
            {
                instance = (T)EntityToBlittable.ConvertToEntity(typeof(T), id, curDoc, _conventions);
            }

            if (string.IsNullOrEmpty(id) == false)
                _generateEntityIdOnTheClient.TrySetIdentity(instance, id);

            foreach (var subscriber in _subscribers)
            {
                _proccessingCts.Token.ThrowIfCancellationRequested();
                try
                {
                    subscriber.OnNext(instance);
                }
                catch (Exception ex)
                {
                    Logger.WarnException(
                        string.Format(
                            "Subscription #{0}. Subscriber threw an exception", _options.SubscriptionId), ex);

                    if (_options.IgnoreSubscribersErrors == false)
                    {
                        IsErroredBecauseOfSubscriber = true;
                        LastSubscriberException = ex;

                        try
                        {
                            subscriber.OnError(ex);
                        }
                        catch (Exception)
                        {
                            // can happen if a subscriber doesn't have an onError handler - just ignore it
                        }
                        break;
                    }
                }
            }
        }

        private void SendAck(long lastReceivedEtag, Stream networkStream)
        {
            BeforeAcknowledgment();

            var ack = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new SubscriptionConnectionClientMessage
            {
                Etag = lastReceivedEtag,
                Type = SubscriptionConnectionClientMessage.MessageType.Acknowledge
            }));

            networkStream.Write(ack, 0, ack.Length);
            networkStream.Flush();
        }

        private async Task RunSubscriptionAsync(TaskCompletionSource<object> firstConnectionCompleted)
        {
            while (_proccessingCts.Token.IsCancellationRequested == false)
            {
                try
                {
                    CloseTcpClient();
                    Logger.Debug(string.Format("Subscription #{0}. Connecting to server...", _options.SubscriptionId));

                    _tcpClient = new TcpClient();
                    await ProccessSubscription(firstConnectionCompleted);
                }
                catch (Exception ex)
                {
                    if (_proccessingCts.Token.IsCancellationRequested)
                    {
                        return;
                    }
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    Task.Run(() => firstConnectionCompleted.TrySetException(ex));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                    Logger.WarnException(
                        string.Format("Subscription #{0}. Pulling task threw the following exception", _options.SubscriptionId), ex);


                    if (await TryHandleRejectedConnection(ex, false).ConfigureAwait(false))
                    {
                        if (Logger.IsDebugEnabled)
                            Logger.Debug(string.Format("Subscription #{0}.", _options.SubscriptionId));
                        return;
                    }
                    await Task.Delay(_options.TimeToWaitBeforeConnectionRetryMilliseconds);
                }
            }
            if (_proccessingCts.Token.IsCancellationRequested)
                return;

            if (IsErroredBecauseOfSubscriber)
            {
                try
                {
                    // prevent from calling Wait() on this in Dispose because we are already inside this task
                    await DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Logger.WarnException(
                        string.Format(
                            "Subscription #{0}. Exception happened during an attempt to close subscription after it had become faulted",
                            _options.SubscriptionId), e);
                }
            }
        }

        private async Task<bool> TryHandleRejectedConnection(Exception ex, bool reopenTried)
        {
            if (ex is SubscriptionInUseException || // another client has connected to the subscription
                ex is SubscriptionDoesNotExistException || // subscription has been deleted meanwhile
                (ex is SubscriptionClosedException && reopenTried))
            // someone forced us to drop the connection by calling Subscriptions.Release
            {
                IsConnectionClosed = true;

                InformSubscribersOnError(ex);

                await DisposeAsync().ConfigureAwait(false);

                return true;
            }

            return false;
        }

        private void OnCompletedNotification()
        {
            if (_completed)
                return;

            foreach (var subscriber in _subscribers)
            {
                try
                {
                    subscriber.OnCompleted();
                }
                catch (Exception)
                {
                    // nothing to be done here
                }
            }

            _completed = true;
        }


        private void CloseTcpClient()
        {
            if (_networkStream != null)
            {
                try
                {
                    _networkStream.Dispose();
                    _networkStream = null;
                }
                catch (Exception)
                {
                }
            }
            if (_tcpClient != null)
            {
                try
                {
                    _tcpClient.Dispose();
                    _tcpClient = null;
                }
                catch (Exception)
                {

                }
            }
        }
    }
}
