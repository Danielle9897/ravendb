﻿using System;
using System.Net.Http;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Session;
using Raven.Client.Http;
using Raven.Client.Json;
using Raven.Client.ServerWide;
using Sparrow.Json;

namespace Raven.Client.Documents.Operations.Configuration
{
    public class PutClientConfigurationOperation : IAdminOperation
    {
        private readonly ClientConfiguration _configuration;

        public PutClientConfigurationOperation(ClientConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public RavenCommand GetCommand(DocumentConventions conventions, JsonOperationContext context)
        {
            return new PutClientConfigurationCommand(conventions, context, _configuration);
        }

        private class PutClientConfigurationCommand : RavenCommand
        {
            private readonly JsonOperationContext _context;
            private readonly BlittableJsonReaderObject _configuration;

            public PutClientConfigurationCommand(DocumentConventions conventions, JsonOperationContext context, ClientConfiguration configuration)
            {
                if (conventions == null)
                    throw new ArgumentNullException(nameof(conventions));
                if (configuration == null)
                    throw new ArgumentNullException(nameof(configuration));

                _context = context ?? throw new ArgumentNullException(nameof(context));
                _configuration = EntityToBlittable.ConvertEntityToBlittable(configuration, conventions, context);
            }

            public override HttpRequestMessage CreateRequest(ServerNode node, out string url)
            {
                url = $"{node.Url}/databases/{node.Database}/admin/configuration/client";

                return new HttpRequestMessage
                {
                    Method = HttpMethod.Put,
                    Content = new BlittableJsonContent(stream =>
                    {
                        _context.Write(stream, _configuration);
                    })
                };
            }
        }
    }
}