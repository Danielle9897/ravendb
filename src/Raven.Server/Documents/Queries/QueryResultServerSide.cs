﻿using System;
using Raven.Client.Data.Queries;

namespace Raven.Server.Documents.Queries
{
    public abstract class QueryResultServerSide<T> : QueryResult<T>
    {
        public abstract void AddResult(T result);

        public abstract void HandleException(Exception e);

        public abstract bool SupportsExceptionHandling { get; }

        public abstract bool SupportsInclude { get; }

        public bool NotModified { get; protected set; }
    }

    public abstract class QueryResultServerSide : QueryResultServerSide<Document>
    {
    }
}