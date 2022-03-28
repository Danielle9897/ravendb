﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using Raven.Client;
using Raven.Client.Documents.Operations.Revisions;
using Raven.Server.Json;
using Raven.Server.NotificationCenter.Notifications.Details;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;

namespace Raven.Server.Documents.Handlers.Processors.Revisions
{
    internal class RevisionsHandlerProcessorForGetRevisions : AbstractRevisionsHandlerProcessorForGetRevisions<DatabaseRequestHandler, DocumentsOperationContext>
    {
        public RevisionsHandlerProcessorForGetRevisions([NotNull] DatabaseRequestHandler requestHandler) : base(requestHandler, requestHandler.ContextPool)
        {
        }

        protected override void AddPagingPerformanceHint(PagingOperationType operation, string action, string details, long numberOfResults, int pageSize, long duration,
            long totalDocumentsSizeInBytes)
        {
            RequestHandler.AddPagingPerformanceHint(operation, action, details, numberOfResults, pageSize, duration, totalDocumentsSizeInBytes);
        }

        protected override async ValueTask GetRevisionByChangeVectorAsync(StringValues changeVectors, bool metadataOnly, CancellationToken token)
        {
            using (ContextPool.AllocateOperationContext(out DocumentsOperationContext context))
            using (context.OpenReadTransaction())
            {
                var revisionsStorage = RequestHandler.Database.DocumentsStorage.RevisionsStorage;
                var sw = Stopwatch.StartNew();

                int total = 0;
                var revisions = new List<Document>(changeVectors.Count);

                foreach (var changeVector in changeVectors)
                {
                    var revision = revisionsStorage.GetRevision(context, changeVector);
                    if (revision == null && changeVectors.Count == 1)
                    {
                        HttpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }

                    if (revision != null)
                        total++;

                    revisions.Add(revision);
                }

                var actualEtag = ComputeHttpEtags.ComputeEtagForRevisions(revisions);
                CheckNotModified(actualEtag);

                long numberOfResults;
                long totalDocumentsSizeInBytes;
                var blittable = RequestHandler.GetBoolValueQueryString("blittable", required: false) ?? false;
                if (blittable)
                {
                    var revisionResult = new RevisionsResult()
                    {
                        Results = revisions.Select(x => x?.Data).ToArray(),
                        TotalResults = total
                    };

                    WriteRevisionsBlittable(context, revisionResult, out numberOfResults, out totalDocumentsSizeInBytes);
                }
                else
                {
                    var revisionResult = new RevisionsResult<Document>()
                    {
                        Results = revisions, 
                        TotalResults = total
                    };
                    (numberOfResults, totalDocumentsSizeInBytes) = await WriteRevisionsJsonAsync(context, metadataOnly, revisionResult, token);
                }

                //using this function's legacy name GetRevisionByChangeVector
                AddPagingPerformanceHint(PagingOperationType.Documents, "GetRevisionByChangeVector", HttpContext.Request.QueryString.Value, numberOfResults,
                    revisions.Count, sw.ElapsedMilliseconds, totalDocumentsSizeInBytes);
            }
        }

        protected override async ValueTask GetRevisionsAsync(bool metadataOnly, CancellationToken token)
        {
            using (ContextPool.AllocateOperationContext(out DocumentsOperationContext context))
            using (context.OpenReadTransaction())
            {
                var sw = Stopwatch.StartNew();

                var id = RequestHandler.GetQueryStringValueAndAssertIfSingleAndNotEmpty("id");
                var before = RequestHandler.GetDateTimeQueryString("before", required: false);
                var start = RequestHandler.GetStart();
                var pageSize = RequestHandler.GetPageSize();

                Document[] revisions = Array.Empty<Document>();
                long count = 0;
                if (before != null)
                {
                    var revision = RequestHandler.Database.DocumentsStorage.RevisionsStorage.GetRevisionBefore(context, id, before.Value);
                    if (revision != null)
                    {
                        count = 1;
                        revisions = new[] {revision};
                    }
                }
                else
                {
                    (revisions, count) = RequestHandler.Database.DocumentsStorage.RevisionsStorage.GetRevisions(context, id, start, pageSize);
                }

                var actualChangeVector = revisions.Length == 0 ? "" : revisions[0].ChangeVector;
                CheckNotModified(actualChangeVector);

                long loadedRevisionsCount;
                long totalDocumentsSizeInBytes;
                await using (var writer = new AsyncBlittableJsonTextWriter(context, RequestHandler.ResponseBodyStream()))
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName(nameof(RevisionsResult.Results));
                    (loadedRevisionsCount, totalDocumentsSizeInBytes) = await writer.WriteDocumentsAsync(context, revisions, metadataOnly, token);

                    writer.WriteComma();

                    writer.WritePropertyName(nameof(RevisionsResult.TotalResults));
                    writer.WriteInteger(count);
                    writer.WriteEndObject();
                }

                //using this function's legacy name GetRevisions
                AddPagingPerformanceHint(PagingOperationType.Revisions, "GetRevisions", HttpContext.Request.QueryString.Value, loadedRevisionsCount, pageSize,
                    sw.ElapsedMilliseconds, totalDocumentsSizeInBytes);
            }
        }

        protected override void CheckNotModified(string actualEtag)
        {
            var etag = RequestHandler.GetStringFromHeaders("If-None-Match");
            if (etag == actualEtag)
            {
                HttpContext.Response.StatusCode = (int)HttpStatusCode.NotModified;
                return;
            }

            HttpContext.Response.Headers[Constants.Headers.Etag] = "\"" + actualEtag + "\"";
        }

        protected async ValueTask<(long NumberOfResults, long TotalDocumentsSizeInBytes)> WriteRevisionsJsonAsync(JsonOperationContext context, bool metadataOnly, RevisionsResult<Document> revisionsResult, CancellationToken token)
        {
            long numberOfResults;
            long totalDocumentsSizeInBytes;
            await using (var writer = new AsyncBlittableJsonTextWriter(context, RequestHandler.ResponseBodyStream()))
            {
                writer.WriteStartObject();
                writer.WritePropertyName(nameof(revisionsResult.Results));
                (numberOfResults, totalDocumentsSizeInBytes) = await writer.WriteDocumentsAsync(context, revisionsResult.Results, metadataOnly, token);

                writer.WriteComma();

                writer.WritePropertyName(nameof(revisionsResult.TotalResults));
                writer.WriteInteger(revisionsResult.TotalResults);
                writer.WriteEndObject();
            }

            return (numberOfResults, totalDocumentsSizeInBytes);
        }
    }
}
