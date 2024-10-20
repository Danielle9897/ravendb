//-----------------------------------------------------------------------
// <copyright file="HiLoKeyGenerator.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Linq;
using System.Threading;
using Raven.Abstractions.Data;
using Raven.Abstractions.Exceptions;
using Raven.Abstractions.Util;
using Raven.Client.Connection;
using Raven.Client.Exceptions;
using Raven.Json.Linq;
using System.Threading.Tasks;
using Raven.Client.Connection.Async;
using Raven.Client.Data;

namespace Raven.Client.Document
{
    /// <summary>
    /// Generate hilo numbers against a RavenDB document
    /// </summary>
    public class AsyncHiLoKeyGenerator : HiLoKeyGeneratorBase
    {
        private SpinLock generatorLock = new SpinLock(enableThreadOwnerTracking: false); // Using a spin lock rather than Monitor.Enter, because it's not reentrant

        /// <summary>
        /// Initializes a new instance of the <see cref="HiLoKeyGenerator"/> class.
        /// </summary>
        public AsyncHiLoKeyGenerator(string tag, long capacity)
            : base(tag, capacity)
        {
        }

        /// <summary>
        /// Generates the document key.
        /// </summary>
        /// <param name="databaseCommands">The commands.</param>
        /// <param name="convention">The convention.</param>
        /// <param name="entity">The entity.</param>
        /// <returns></returns>
        public Task<string> GenerateDocumentKeyAsync(IAsyncDatabaseCommands databaseCommands, DocumentConvention convention, object entity)
        {
            return NextIdAsync(databaseCommands).ContinueWith(task => GetDocumentKeyFromId(convention, task.Result));
        }

        ///<summary>
        /// Create the next id (numeric)
        ///</summary>
        public Task<long> NextIdAsync(IAsyncDatabaseCommands databaseCommands)
        {
            var myRange = Range; // thread safe copy
            long incrementedCurrent = Interlocked.Increment(ref myRange.Current);
            if (incrementedCurrent <= myRange.Max)
            {
                return CompletedTask.With(incrementedCurrent);
            }

            bool lockTaken = false;
            try
            {
                generatorLock.Enter(ref lockTaken);
                if (Range != myRange)
                {
                    // Lock was contended, and the max has already been changed. Just get a new id as usual.
                    generatorLock.Exit();
                    return NextIdAsync(databaseCommands);
                }
                // Get a new max, and use the current value.
                return GetNextRangeAsync(databaseCommands)
                    .ContinueWith(task =>
                    {
                        try
                        {
                            Range = task.Result;
                        }
                        finally
                        {
                            generatorLock.Exit();
                        }

                        return NextIdAsync(databaseCommands);
                    }).Unwrap();
            }
            catch
            {
                // We only unlock in exceptional cases (and not in a finally clause) because non exceptional cases will either have already
                // unlocked or will have started a task that will unlock in the future.
                if (lockTaken)
                    generatorLock.Exit();
                throw;
            }
        }

        private Task<RangeValue> GetNextRangeAsync(IAsyncDatabaseCommands databaseCommands)
        {
            ModifyCapacityIfRequired();

            return GetNextMaxAsyncInner(databaseCommands);
        }

        private async Task<RangeValue> GetNextMaxAsyncInner(IAsyncDatabaseCommands databaseCommands)
        {
            var minNextMax = Range.Max;

            using (databaseCommands.ForceReadFromMaster())
                while (true)
                {
                    try
                    {
                        ConflictException ce = null;
                        JsonDocument document;
                        try
                        {
                            document = await GetDocumentAsync(databaseCommands).ConfigureAwait(false);
                        }
                        catch (ConflictException e)
                        {
                            ce = e;
                            document = null;
                        }
                        if (ce != null)
                            return await HandleConflictsAsync(databaseCommands, ce, minNextMax).ConfigureAwait(false);

                        long min, max;
                        if (document == null)
                        {
                            min = minNextMax + 1;
                            max = minNextMax + capacity;
                            document = new JsonDocument
                            {
                                Etag = 0,
                                // sending empty etag means - ensure the that the document does NOT exists
                                Metadata = new RavenJObject(),
                                DataAsJson = RavenJObject.FromObject(new { Max = max }),
                                Key = HiLoDocumentKey
                            };
                        }
                        else
                        {
                            var oldMax = GetMaxFromDocument(document, minNextMax);
                            min = oldMax + 1;
                            max = oldMax + capacity;

                            document.DataAsJson["Max"] = max;
                        }

                        await PutDocumentAsync(databaseCommands, document).ConfigureAwait(false);
                        return new RangeValue(min, max);
                    }
                    catch (ConcurrencyException)
                    {
                        //expected & ignored, will retry this
                    }
                }
        }

        private async Task<RangeValue> HandleConflictsAsync(IAsyncDatabaseCommands databaseCommands, ConflictException e, long minNextMax)
        {
            // resolving the conflict by selecting the highest number
            long highestMax = -1;
            if (e.ConflictedVersionIds.Length == 0)
                throw new InvalidOperationException("Got conflict exception, but no conflicted versions", e);
            foreach (var conflictedVersionId in e.ConflictedVersionIds)
            {
                var doc = await databaseCommands.GetAsync(conflictedVersionId).ConfigureAwait(false);
                highestMax = Math.Max(highestMax, GetMaxFromDocument(doc, minNextMax));
            }

            await PutDocumentAsync(databaseCommands, new JsonDocument
            {
                Etag = e.Etag,
                Metadata = new RavenJObject(),
                DataAsJson = RavenJObject.FromObject(new { Max = highestMax }),
                Key = HiLoDocumentKey
            }).ConfigureAwait(false);
            return await GetNextRangeAsync(databaseCommands).ConfigureAwait(false);
        }

        private Task PutDocumentAsync(IAsyncDatabaseCommands databaseCommands, JsonDocument document)
        {
            return databaseCommands.PutAsync(HiLoDocumentKey, document.Etag,
                                 document.DataAsJson,
                                 document.Metadata);
        }

        private async Task<JsonDocument> GetDocumentAsync(IAsyncDatabaseCommands databaseCommands)
        {
            var documents = await databaseCommands.GetAsync(new[] { HiLoDocumentKey, RavenKeyServerPrefix }, new string[0]).ConfigureAwait(false);
            if (documents.Results.Count == 2 && documents.Results[1] != null)
            {
                lastServerPrefix = documents.Results[1].Value<string>("ServerPrefix");
            }
            else
            {
                lastServerPrefix = string.Empty;
            }
            if (documents.Results.Count == 0 || documents.Results[0] == null)
                return null;

            var jsonDocument = documents.Results[0].ToJsonDocument();
            foreach (var key in jsonDocument.Metadata.Keys.Where(x => x.StartsWith("@")).ToArray())
            {
                jsonDocument.Metadata.Remove(key);
            }
            return jsonDocument;
        }
    }
}
