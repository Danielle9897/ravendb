//-----------------------------------------------------------------------
// <copyright file="MultiTypeHiLoKeyGenerator.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Raven.NewClient.Abstractions.Util;


namespace Raven.NewClient.Client.Document.Async
{
    /// <summary>
    /// Generate a hilo key for each given type
    /// </summary>
    public class AsyncMultiTypeHiLoKeyGenerator
    {
        //private readonly int capacity;
        private readonly object _generatorLock = new object();
        private readonly ConcurrentDictionary<string, AsyncHiLoKeyGenerator> _keyGeneratorsByTag = new ConcurrentDictionary<string, AsyncHiLoKeyGenerator>();
        private readonly DocumentStore _store;
        private readonly string _dbName;
        private readonly DocumentConvention _conventions;

        public AsyncMultiTypeHiLoKeyGenerator(DocumentStore store, string dbName, DocumentConvention conventions)
        {
            _store = store;
            _dbName = dbName;
            _conventions = conventions;
        }
        
        public Task<string> GenerateDocumentKeyAsync(object entity)
        {
            var typeTagName = _conventions.GetDynamicTagName(entity);
            if (string.IsNullOrEmpty(typeTagName)) //ignore empty tags
                return CompletedTask.With<string>(null);
            var tag = _conventions.TransformTypeTagNameToDocumentKeyPrefix(typeTagName);
            AsyncHiLoKeyGenerator value;
            if (_keyGeneratorsByTag.TryGetValue(tag, out value))
                return value.GenerateDocumentKeyAsync(entity);

            lock(_generatorLock)
            {
                if (_keyGeneratorsByTag.TryGetValue(tag, out value))
                    return value.GenerateDocumentKeyAsync(entity);

                value = new AsyncHiLoKeyGenerator(tag, _store, _dbName, _conventions.IdentityPartsSeparator);
                _keyGeneratorsByTag.TryAdd(tag, value);
            }

            return value.GenerateDocumentKeyAsync(entity);
        }

        public async Task ReturnUnusedRange()
        {
            foreach (var generator in _keyGeneratorsByTag)
            {
                await generator.Value.ReturnUnusedRangeAsync().ConfigureAwait(false);
            }
        }
    }
}
