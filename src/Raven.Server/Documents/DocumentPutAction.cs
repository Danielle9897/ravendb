using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Raven.Client;
using Raven.Client.Documents.Changes;
using Raven.Client.Documents.Replication.Messages;
using Raven.Server.Documents.Versioning;
using Raven.Server.ServerWide.Context;
using Raven.Server.Utils;
using Sparrow.Binary;
using Sparrow.Json;
using Sparrow.Json.Parsing;
using Voron;
using Voron.Data.Tables;
using Voron.Exceptions;

namespace Raven.Server.Documents
{
    public unsafe class DocumentPutAction
    {
        private readonly DocumentsStorage _documentsStorage;
        private readonly DocumentDatabase _documentDatabase;

        private readonly StringBuilder _keyBuilder = new StringBuilder();

        public DocumentPutAction(DocumentsStorage documentsStorage, DocumentDatabase documentDatabase)
        {
            _documentsStorage = documentsStorage;
            _documentDatabase = documentDatabase;
        }

        public DocumentsStorage.PutOperationResults PutDocument(DocumentsOperationContext context, string key, long? expectedEtag,
            BlittableJsonReaderObject document,
            long? lastModifiedTicks = null,
            ChangeVectorEntry[] changeVector = null,
            DocumentFlags flags = DocumentFlags.None,
            NonPersistentDocumentFlags nonPersistentFlags = NonPersistentDocumentFlags.None)
        {
            if (context.Transaction == null)
                throw ThrowRequiresTransaction();

#if DEBUG
            var documentDebugHash = document.DebugHash;
            document.BlittableValidation();
#endif

            BlittableJsonReaderObject.AssertNoModifications(document, key, assertChildren: true);

            var collectionName = _documentsStorage.ExtractCollectionName(context, key, document);
            var newEtag = _documentsStorage.GenerateNextEtag();

            var modifiedTicks = lastModifiedTicks ?? _documentDatabase.Time.GetUtcNow().Ticks;

            var table = context.Transaction.InnerTransaction.OpenTable(DocumentsStorage.DocsSchema, collectionName.GetTableName(CollectionTableType.Documents));

            bool knownNewKey = false;
            if (string.IsNullOrWhiteSpace(key))
            {
                key = Guid.NewGuid().ToString();
                knownNewKey = true;
            }
            else
            {
                switch (key[key.Length - 1])
                {
                    case '/':
                        key = GetNextIdentityValueWithoutOverwritingOnExistingDocuments(key, table, context, out _);
                        knownNewKey = true;
                        break;
                    case '|':
                        key = AppendNumericValueToKey(key, newEtag);
                        knownNewKey = true;
                        break;
                }
            }

            DocumentKeyWorker.GetLowerKeySliceAndStorageKey(context, key, out Slice lowerKey, out Slice keyPtr);

            if (_documentsStorage.ConflictsStorage.ConflictsCount != 0)
            {
                // Since this document resolve the conflict we dont need to alter the change vector.
                // This way we avoid another replication back to the source
                if (expectedEtag.HasValue)
                {
                    _documentsStorage.ConflictsStorage.ThrowConcurrencyExceptionOnConflict(context, lowerKey.Content.Ptr, lowerKey.Size, expectedEtag);
                }

                if ((flags & DocumentFlags.FromReplication) == DocumentFlags.FromReplication)
                {
                    _documentsStorage.ConflictsStorage.DeleteConflictsFor(context, key);
                }
                else
                {
                    changeVector = _documentsStorage.ConflictsStorage.MergeConflictChangeVectorIfNeededAndDeleteConflicts(changeVector, context, key, newEtag);
                }
            }

            var oldValue = default(TableValueReader);
            if (knownNewKey == false)
            {
                // delete a tombstone if it exists, if it known that it is a new key, no need, so we can skip it
                DeleteTombstoneIfNeeded(context, collectionName, lowerKey.Content.Ptr, lowerKey.Size);

                table.ReadByKey(lowerKey, out oldValue);
            }

            BlittableJsonReaderObject oldDoc = null;
            if (oldValue.Pointer == null)
            {
                if (expectedEtag != null && expectedEtag != 0)
                {
                    ThrowConcurrentExceptionOnMissingDoc(key, expectedEtag.Value);
                }
            }
            else
            {
                if (expectedEtag != null)
                {
                    var oldEtag = DocumentsStorage.TableValueToEtag(1, ref oldValue);
                    if (oldEtag != expectedEtag)
                        ThrowConcurrentException(key, expectedEtag, oldEtag);
                }

                oldDoc = new BlittableJsonReaderObject(oldValue.Read((int)DocumentsStorage.DocumentsTable.Data, out int oldSize), oldSize, context);
                var oldCollectionName = _documentsStorage.ExtractCollectionName(context, key, oldDoc);
                if (oldCollectionName != collectionName)
                    ThrowInvalidCollectionNameChange(key, oldCollectionName, collectionName);

                var oldFlags = *(DocumentFlags*)oldValue.Read((int)DocumentsStorage.DocumentsTable.Flags, out int size);
                if ((oldFlags & DocumentFlags.HasAttachments) == DocumentFlags.HasAttachments ||
                    (nonPersistentFlags & NonPersistentDocumentFlags.ResolvedAttachmentConflict) == NonPersistentDocumentFlags.ResolvedAttachmentConflict)
                {
                    flags |= DocumentFlags.HasAttachments;
                }
            }

            if (changeVector == null)
            {
                var oldChangeVector = oldValue.Pointer != null ? DocumentsStorage.GetChangeVectorEntriesFromTableValueReader(ref oldValue, (int)DocumentsStorage.DocumentsTable.ChangeVector) : null;
                changeVector = SetDocumentChangeVectorForLocalChange(context, lowerKey, oldChangeVector, newEtag);
            }


            if (collectionName.IsSystem == false &&
                (flags & DocumentFlags.Artificial) != DocumentFlags.Artificial)
            {
                if(ShouldRecreateAttachment(context, lowerKey, oldDoc, document, flags, nonPersistentFlags))
                {
#if DEBUG
                    if (document.DebugHash != documentDebugHash)
                    {
                        throw new InvalidDataException("The incoming document " + key + " has changed _during_ the put process, " +
                                                       "this is likely because you are trying to save a document that is already stored and was moved");
                    }
#endif
                    document = context.ReadObject(document, key, BlittableJsonDocumentBuilder.UsageMode.ToDisk);
#if DEBUG
                    documentDebugHash = document.DebugHash;
                    document.BlittableValidation();
#endif
                }

                if (_documentDatabase.BundleLoader.VersioningStorage != null)
                {
                    VersioningConfigurationCollection configuration;
                    if (_documentDatabase.BundleLoader.VersioningStorage.ShouldVersionDocument(collectionName, nonPersistentFlags, oldDoc, document, ref flags, out configuration, context, key))
                    {
                        _documentDatabase.BundleLoader.VersioningStorage.PutFromDocument(context, key, document, flags, changeVector, modifiedTicks, configuration);
                    }
                }
            }

            fixed (ChangeVectorEntry* pChangeVector = changeVector)
            {
                using (table.Allocate(out TableValueBuilder tbv))
                {
                    tbv.Add(lowerKey);
                    tbv.Add(Bits.SwapBytes(newEtag));
                    tbv.Add(keyPtr);
                    tbv.Add(document.BasePointer, document.Size);
                    tbv.Add((byte*)pChangeVector, sizeof(ChangeVectorEntry) * changeVector.Length);
                    tbv.Add(modifiedTicks);
                    tbv.Add((int)flags);
                    tbv.Add(context.GetTransactionMarker());

                    if (oldValue.Pointer == null)
                    {
                        table.Insert(tbv);
                    }
                    else
                    {
                        table.Update(oldValue.Id, tbv);
                    }
                }
            }

            if (collectionName.IsSystem == false)
            {
                _documentDatabase.BundleLoader.ExpiredDocumentsCleaner?.Put(context, lowerKey, document);
            }

            _documentDatabase.Metrics.DocPutsPerSecond.MarkSingleThreaded(1);
            _documentDatabase.Metrics.BytesPutsPerSecond.MarkSingleThreaded(document.Size);


            context.Transaction.AddAfterCommitNotification(new DocumentChange
            {
                Etag = newEtag,
                CollectionName = collectionName.Name,
                Key = key,
                Type = DocumentChangeTypes.Put,
                IsSystemDocument = collectionName.IsSystem,
            });

#if DEBUG
            if (document.DebugHash != documentDebugHash)
            {
                throw new InvalidDataException("The incoming document " + key + " has changed _during_ the put process, " +
                                               "this is likely because you are trying to save a document that is already stored and was moved");
            }
#endif

            return new DocumentsStorage.PutOperationResults
            {
                Etag = newEtag,
                Key = key,
                Collection = collectionName,
                ChangeVector = changeVector
            };
        }

        private bool ShouldRecreateAttachment(DocumentsOperationContext context, Slice lowerKey, BlittableJsonReaderObject oldDoc, BlittableJsonReaderObject document, DocumentFlags flags, NonPersistentDocumentFlags nonPersistentFlags)
        {
            var shouldRecreateAttachment = false;
            BlittableJsonReaderObject metadata = null;
            if ((flags & DocumentFlags.HasAttachments) == DocumentFlags.HasAttachments &&
                (nonPersistentFlags & NonPersistentDocumentFlags.ByAttachmentUpdate) != NonPersistentDocumentFlags.ByAttachmentUpdate &&
                (nonPersistentFlags & NonPersistentDocumentFlags.FromReplication) != NonPersistentDocumentFlags.FromReplication)
            {
                Debug.Assert(oldDoc != null, "Can be null when it comes from replication, but we checked for this.");

                if (oldDoc.TryGet(Constants.Documents.Metadata.Key, out BlittableJsonReaderObject oldMetadata) &&
                    oldMetadata.TryGet(Constants.Documents.Metadata.Attachments, out BlittableJsonReaderArray oldAttachments))
                {
                    // Make sure the user did not changed the value of @attachments in the @metadata
                    // In most cases it won't be changed so we can use this value 
                    // instead of recreating the document's blitable from scratch
                    if (document.TryGet(Constants.Documents.Metadata.Key, out metadata) == false ||
                        metadata.TryGet(Constants.Documents.Metadata.Attachments, out BlittableJsonReaderArray attachments) == false ||
                        attachments.Equals(oldAttachments) == false)
                    {
                        shouldRecreateAttachment = true;
                    }
                }
            }

            if (shouldRecreateAttachment == false && 
                (nonPersistentFlags & NonPersistentDocumentFlags.ResolvedAttachmentConflict) != NonPersistentDocumentFlags.ResolvedAttachmentConflict)
                return false;

            if (shouldRecreateAttachment == false)
                document.TryGet(Constants.Documents.Metadata.Key, out metadata);

            var actualAttachments = _documentsStorage.AttachmentsStorage.GetAttachmentsMetadataForDocument(context, lowerKey);
            if (metadata == null)
            {
                document.Modifications = new DynamicJsonValue(document)
                {
                    [Constants.Documents.Metadata.Key] = new DynamicJsonValue
                    {
                        [Constants.Documents.Metadata.Attachments] = actualAttachments
                    }
                };
            }
            else
            {
                metadata.Modifications = new DynamicJsonValue(metadata)
                {
                    [Constants.Documents.Metadata.Attachments] = actualAttachments
                };
                document.Modifications = new DynamicJsonValue(document)
                {
                    [Constants.Documents.Metadata.Key] = metadata
                };
            }

            return true;
        }

        public static ArgumentException ThrowRequiresTransaction([CallerMemberName]string caller = null)
        {
            // ReSharper disable once NotResolvedInText
            return new ArgumentException("Context must be set with a valid transaction before calling " + caller, "context");
        }

        private static void ThrowConcurrentExceptionOnMissingDoc(string key, long expectedEtag)
        {
            throw new ConcurrencyException(
                $"Document {key} does not exist, but Put was called with etag {expectedEtag}. Optimistic concurrency violation, transaction will be aborted.")
            {
                ExpectedETag = expectedEtag
            };
        }

        private static void ThrowInvalidCollectionNameChange(string key, CollectionName oldCollectionName, CollectionName collectionName)
        {
            throw new InvalidOperationException(
                $"Changing '{key}' from '{oldCollectionName.Name}' to '{collectionName.Name}' via update is not supported.{System.Environment.NewLine}" +
                $"Delete it and recreate the document {key}.");
        }

        private static void ThrowConcurrentException(string key, long? expectedEtag, long oldEtag)
        {
            throw new ConcurrencyException(
                $"Document {key} has etag {oldEtag}, but Put was called with etag {expectedEtag}. Optimistic concurrency violation, transaction will be aborted.")
            {
                ActualETag = oldEtag,
                ExpectedETag = expectedEtag ?? -1
            };
        }

        public string GetNextIdentityValueWithoutOverwritingOnExistingDocuments(string key, Table table, DocumentsOperationContext context, out int tries)
        {
            var identities = context.Transaction.InnerTransaction.ReadTree(IdentitiesStorage.IdentitiesSlice);
            var nextIdentityValue = identities.Increment(key, 1);
            var finalKey = AppendIdentityValueToKey(key, nextIdentityValue);
            Slice finalKeySlice;
            tries = 1;

            using (DocumentKeyWorker.GetSliceFromKey(context, finalKey, out finalKeySlice))
            {
                TableValueReader reader;
                if (table.ReadByKey(finalKeySlice, out reader) == false)
                {
                    return finalKey;
                }
            }

            /* We get here if the user inserted a document with a specified id.
            e.g. your identity is 100
            but you forced a put with 101
            so you are trying to insert next document and it would overwrite the one with 101 */

            var lastKnownBusy = nextIdentityValue;
            var maybeFree = nextIdentityValue * 2;
            var lastKnownFree = long.MaxValue;
            while (true)
            {
                tries++;
                finalKey = AppendIdentityValueToKey(key, maybeFree);
                using (DocumentKeyWorker.GetSliceFromKey(context, finalKey, out finalKeySlice))
                {
                    TableValueReader reader;
                    if (table.ReadByKey(finalKeySlice, out reader) == false)
                    {
                        if (lastKnownBusy + 1 == maybeFree)
                        {
                            nextIdentityValue = identities.Increment(key, lastKnownBusy);
                            return key + nextIdentityValue;
                        }
                        lastKnownFree = maybeFree;
                        maybeFree = Math.Max(maybeFree - (maybeFree - lastKnownBusy) / 2, lastKnownBusy + 1);
                    }
                    else
                    {
                        lastKnownBusy = maybeFree;
                        maybeFree = Math.Min(lastKnownFree, maybeFree * 2);
                    }
                }
            }
        }

        private string AppendIdentityValueToKey(string key, long val)
        {
            _keyBuilder.Length = 0;
            _keyBuilder.Append(key);
            _keyBuilder.Append(val);
            return _keyBuilder.ToString();
        }


        private string AppendNumericValueToKey(string key, long val)
        {
            _keyBuilder.Length = 0;
            _keyBuilder.Append(key);
            _keyBuilder[_keyBuilder.Length - 1] = '/';
            _keyBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0:D19}", val);
            return _keyBuilder.ToString();
        }

        private static void DeleteTombstoneIfNeeded(DocumentsOperationContext context, CollectionName collectionName, byte* lowerKey, int lowerSize)
        {
            var tombstoneTable = context.Transaction.InnerTransaction.OpenTable(DocumentsStorage.TombstonesSchema, collectionName.GetTableName(CollectionTableType.Tombstones));
            using (Slice.From(context.Allocator, lowerKey, lowerSize, out Slice key))
            {
                tombstoneTable.DeleteByKey(key);
            }
        }

        private ChangeVectorEntry[] SetDocumentChangeVectorForLocalChange(
            DocumentsOperationContext context, Slice loweredKey,
            ChangeVectorEntry[] oldChangeVector, long newEtag)
        {
            if (oldChangeVector != null)
                return ReplicationUtils.UpdateChangeVectorWithNewEtag(_documentsStorage.Environment.DbId, newEtag, oldChangeVector);

            return _documentsStorage.ConflictsStorage.GetMergedConflictChangeVectorsAndDeleteConflicts(context, loweredKey, newEtag);
        }
    }
}