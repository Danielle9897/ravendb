using System;
using System.Collections.Generic;

using Raven.Abstractions.Data;
using Raven.Abstractions.Indexing;
using Raven.Client.Data.Indexes;
using Raven.Client.Extensions;

namespace Raven.Client.Connection
{
    public static class RavenUrlExtensions
    {
        public static string ForDatabase(this string url, string database)
        {
            if (!string.IsNullOrEmpty(database) && !url.Contains("/databases/"))
            {
                return url.EndsWith("/") ?
                    $"{url}databases/{database}" : $"{url}/databases/{database}";
            }

            return url;
        }

        public static string ForFilesystem(this string url, string filesystem)
        {
            return url.EndsWith("/") ?
                $"{url}fs/{filesystem}" : $"{url}/fs/{filesystem}";
        }

        public static string ForCounter(this string url, string counter)
        {
            return url.EndsWith("/") ?
                $"{url}cs/{counter}" : $"{url}/cs/{counter}";
        }

        public static string ForTimeSeries(this string url, string timeSeries)
        {
            return url.EndsWith("/") ?
                $"{url}ts/{timeSeries}" : $"{url}/ts/{timeSeries}";
        }

        public static string Indexes(this string url, string index)
        {
            return $"{url}/indexes?name={index}";
        }

        public static string SetIndexLock(this string url, string index, IndexLockMode mode)
        {
            return $"{url}/indexes/set-lock?name={index}&mode={mode}";
        }

        public static string SetIndexPriority(this string url, string index, IndexPriority priority)
        {
            return $"{url}/indexes/set-priority?name={index}&priority={priority}";
        }

        public static string IndexDefinition(this string url, string index, int? start = null, int? pageSize = null)
        {
            if (string.IsNullOrWhiteSpace(index) == false)
                return $"{url}/indexes?name={index}";

            return $"{url}/indexes?start={start}&pageSize={pageSize}";
        }

        public static string IndexPerformanceStats(this string url, string[] indexNames)
        {
            var result = $"{url}/indexes/performance";

            if (indexNames == null)
                return result;

            var first = true;
            foreach (var indexName in indexNames)
            {
                if (first)
                    result += "?";
                else
                    result += "&";

                first = false;
                result += "name=" + indexName;
            }

            return result;
        }

        public static string Transformer(this string url, string transformer)
        {
            return $"{url}/transformers?name={transformer}";
        }

        public static string IndexNames(this string url, int start, int pageSize)
        {
            return $"{url}/indexes?namesOnly=true&start={start}&pageSize={pageSize}";
        }

        public static string Stats(this string url)
        {
            return $"{url}/stats";
        }

        public static string IndexErrors(this string url, IEnumerable<string> indexNames)
        {
            var result = $"{url}/indexes/errors";
            if (indexNames == null)
                return result;

            var first = true;
            foreach (var indexName in indexNames)
            {
                if (first)
                    result += "?";
                else
                    result += "&";

                first = false;
                result += "name=" + indexName;
            }

            return result;
        }

        public static string IndexStatistics(this string url, string name)
        {
            return $"{url}/indexes/stats?name={name}";
        }

        public static string UserInfo(this string url)
        {
            return $"{url}/debug/user-info";
        }

        public static string UserPermission(this string url, string database, bool readOnly)
        {
            return $"{url}/debug/user-info?database={database}&method={(readOnly ? "GET" : "PUT")}";
        }

        public static string AdminStats(this string url)
        {
            return MultiDatabase.GetRootDatabaseUrl(url) + "/admin/stats";
        }

        public static string ReplicationInfo(this string url)
        {
            return $"{url}/replication/info";
        }

        public static string LastReplicatedEtagFor(this string destinationUrl, string sourceUrl, string sourceDbId)
        {
            return $"{destinationUrl}/replication/lastEtag?from={Uri.EscapeDataString(sourceUrl)}&dbid={sourceDbId}";
        }

        public static string Databases(this string url, int pageSize, int start)
        {
            var databases = $"{MultiDatabase.GetRootDatabaseUrl(url)}/databases?pageSize={pageSize}";
            return start > 0 ? $"{databases}&start={start}" : databases;
        }

        public static string Terms(this string url, string index, string field, string fromValue, int pageSize)
        {
            return $"{url}/indexes/terms?name={index}&field={field}&fromValue={fromValue}&pageSize={pageSize}";
        }

        public static string Doc(this string url, string key)
        {
            return $"{url}/docs?id={key}";
        }

        public static string Docs(this string url, int start, int pageSize)
        {
            return $"{url}/docs?start={start}&pageSize={pageSize}";
        }

        public static string CollectionsDocs(this string url, string collectionName, int start, int pageSize)
        {
            return $"{url}/collections/docs?name={collectionName}&start={start}&pageSize={pageSize}";
        }

        public static string CollectionsDocs(this string url, string collectionName)
        {
            return $"{url}/collections/docs?name={collectionName}";
        }

        public static Uri ToUri(this string url)
        {
            return new Uri(url);
        }
    }
}
