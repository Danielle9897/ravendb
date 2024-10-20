﻿using System.Net.Http;
using Raven.NewClient.Client.Http;
using Raven.NewClient.Client.Json;
using Sparrow.Json;

namespace Raven.NewClient.Client.Commands
{
    public class GetSubscriptionsCommand : RavenCommand<GetSubscriptionsResult>
    {
        private readonly int _start;
        private int _pageSize;

        public GetSubscriptionsCommand(int start, int pageSize)
        {
            _start = start;
            _pageSize = pageSize;
        }

        public override HttpRequestMessage CreateRequest(ServerNode node, out string url)
        {
            url = $"{node.Url}/databases/{node.Database}/subscriptions?start={_start}&pageSize={_pageSize}";
            IsReadRequest = false;

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
            };
            return request;
        }

        public override void SetResponse(BlittableJsonReaderObject response)
        {
            if (response == null)
            {
                Result = null;
                return;
            }
            Result = JsonDeserializationClient.GetSubscriptionsResult(response);
        }
    }
}