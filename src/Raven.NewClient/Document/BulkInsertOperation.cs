using Raven.NewClient.Abstractions.Util;
using System;
using System.Threading;
using System.Threading.Tasks;
using Raven.NewClient.Client.Extensions;
using Raven.NewClient.Client.Http;

namespace Raven.NewClient.Client.Document
{
    public class BulkInsertOperation : IDisposable
    {
        private readonly IDocumentStore documentStore;
        private readonly GenerateEntityIdOnTheClient generateEntityIdOnTheClient;
        protected TcpBulkInsertOperation Operation { get; set; }

        /*public delegate void BeforeEntityInsert(string id, RavenJObject data, RavenJObject metadata);

        public event BeforeEntityInsert OnBeforeEntityInsert = delegate { };*/

        public void Abort()
        {
            Operation.Abort();
        }

        public event Action<string> Report
        {
            add { Operation.Report += value; }
            remove { Operation.Report -= value; }
        }

        public BulkInsertOperation(string database, IDocumentStore documentStore)
        {
            this.documentStore = documentStore;

            database = database ?? MultiDatabase.GetDatabaseName(documentStore.Url);

            generateEntityIdOnTheClient = new GenerateEntityIdOnTheClient(documentStore.Conventions, entity =>
                AsyncHelpers.RunSync(() => documentStore.Conventions.GenerateDocumentKeyAsync(database, entity)));

            // ReSharper disable once VirtualMemberCallInContructor
            Operation = GetBulkInsertOperation(database, documentStore.GetRequestExecuter(database));
        }

        protected virtual TcpBulkInsertOperation GetBulkInsertOperation(string database, RequestExecuter requestExecuter)
        {
            return new TcpBulkInsertOperation(database, documentStore, requestExecuter, default(CancellationTokenSource));
        }

        public async Task DisposeAsync()
        {
            await Operation.DisposeAsync().ConfigureAwait(false);
        }

        public void Dispose()
        {
            Operation.Dispose();
        }

        public string Store(object entity)
        {
            return AsyncHelpers.RunSync(() => StoreAsync(entity));
        }

        public async Task<string> StoreAsync(object entity)
        {
            var id = GetId(entity);
            await StoreAsync(entity, id).ConfigureAwait(false);
            return id;
        }

        public async Task StoreAsync(object entity, string id)
        {
             await Operation.WriteAsync(id, entity).ConfigureAwait(false);
        }

        private string GetId(object entity)
        {
            string id;
            if (generateEntityIdOnTheClient.TryGetIdFromInstance(entity, out id) == false)
            {
                id = generateEntityIdOnTheClient.GenerateDocumentKeyForStorage(entity);
                generateEntityIdOnTheClient.TrySetIdentity(entity,id); //set Id property if it was null
            }
            return id;
        }
    }
}
