﻿using System.Net.Http;
using Raven.NewClient.Abstractions.Data;
using Raven.NewClient.Client.Blittable;
using Raven.NewClient.Client.Http;
using Raven.NewClient.Client.Json;
using Sparrow.Json;

namespace Raven.NewClient.Client.Commands
{
    public class PutDocumentCommand : RavenCommand<PutResult>
    {
        public string Id;
        public long? Etag;
        public BlittableJsonReaderObject Document;
        public JsonOperationContext Context;

        public override HttpRequestMessage CreateRequest(ServerNode node, out string url)
        {
            EnsureIsNotNullOrEmpty(Id, nameof(Id));

            url = $"{node.Url}/databases/{node.Database}/docs?id={UrlEncode(Id)}";
            IsReadRequest = false;
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Put,
                Content = new BlittableJsonContent(stream =>
                {
                    Context.Write(stream, Document);
                }),
            };

            IsReadRequest = false;
            return request;
        }

        public override void SetResponse(BlittableJsonReaderObject response)
        {
            Result = JsonDeserializationClient.PutResult(response);
        }
    }
}