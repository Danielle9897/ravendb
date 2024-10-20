//-----------------------------------------------------------------------
// <copyright file="SerializationHelper.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Raven.Abstractions.Connection;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using Raven.Imports.Newtonsoft.Json;
using Raven.Abstractions;
using Raven.Abstractions.Data;
using Raven.Abstractions.Extensions;
using Raven.Client.Data;
using Raven.Client.Data.Queries;
using Raven.Imports.Newtonsoft.Json.Linq;
using Raven.Json.Linq;

namespace Raven.Client.Connection
{
    ///<summary>
    /// Helper method to do serialization from RavenJObject to JsonDocument
    ///</summary>
    public static class SerializationHelper
    {
        ///<summary>
        /// Translate a collection of RavenJObject to JsonDocuments
        ///</summary>
        public static IEnumerable<JsonDocument> RavenJObjectsToJsonDocuments(IEnumerable<RavenJObject> responses)
        {
            var list = new List<JsonDocument>();
            foreach (var doc in responses)
            {
                if (doc == null)
                {
                    list.Add(null);
                    continue;
                }
                var jsonDocument = RavenJObjectToJsonDocument(doc);
                list.Add(jsonDocument);
            }
            return list;
        }

        public static JsonDocument RavenJObjectToJsonDocument(RavenJObject doc)
        {
            var metadata = (RavenJObject) doc["@metadata"];
            doc.Remove("@metadata");
            var key = Extract(metadata, "@id", string.Empty);
            if (string.IsNullOrEmpty(key))
            {
                // projection, it seems.
                return new JsonDocument
                {
                    Key = string.Empty,
                    DataAsJson = doc,
                    LastModified = SystemTime.UtcNow,
                };
            }

            var lastModified = GetLastModified(metadata);
            metadata[Constants.Headers.LastModified] = lastModified;
            var etag = Extract(metadata, "@etag", 0, (string g) => HttpExtensions.EtagHeaderToEtag(g));

            var jsonDocument = new JsonDocument
            {
                Key = key,
                LastModified = lastModified,
                Etag = etag,
                TempIndexScore = metadata == null ? null : metadata.Value<float?>(Constants.Indexing.Fields.IndexFieldScoreName),
                Metadata = metadata.FilterHeadersToObject(),
                DataAsJson = doc
            };
            return jsonDocument;
        }

        private static DateTime GetLastModified(RavenJObject metadata)
        {
            if (metadata == null)
                return SystemTime.UtcNow;
            return metadata.ContainsKey(Constants.Headers.RavenLastModified) ?
                       Extract(metadata, Constants.Headers.RavenLastModified, SystemTime.UtcNow, (string d) => ConvertToUtcDate(d)) :
                       Extract(metadata, Constants.Headers.LastModified, SystemTime.UtcNow, (string d) => ConvertToUtcDate(d));
        }

        ///<summary>
        /// Translate a collection of RavenJObject to JsonDocuments
        ///</summary>
        public static IEnumerable<JsonDocument> ToJsonDocuments(this IEnumerable<RavenJObject> responses)
        {
            return RavenJObjectsToJsonDocuments(responses);
        }

        ///<summary>
        /// Translate a collection of RavenJObject to JsonDocuments
        ///</summary>
        public static JsonDocument ToJsonDocument(this RavenJObject response)
        {
            return RavenJObjectsToJsonDocuments(new[] { response }).First();
        }

        private static DateTime ConvertToUtcDate(string date)
        {
            return DateTime.SpecifyKind(DateTime.ParseExact(date, new[] { "o", "r" }, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), DateTimeKind.Utc);
        }

        private static T Extract<T>(RavenJObject metadata, string key, T defaultValue = default(T))
        {
            return Extract<T, T>(metadata, key, defaultValue, t => t);
        }

        private static TResult Extract<T, TResult>(RavenJObject metadata, string key, TResult defaultValue, Func<T, TResult> convert)
        {
            if (metadata == null) return defaultValue;
            if (!metadata.ContainsKey(key)) return defaultValue;
            if (metadata[key].Type == JTokenType.Array) return defaultValue;

            var value = metadata[key].Value<object>();

            if (value is TResult)
                return (TResult)value;

            return convert(metadata[key].Value<T>());
        }

        /// <summary>
        /// Translate a result for a query
        /// </summary>
        public static QueryResult ToQueryResult(RavenJObject json, long? etag, string tempRequestTime, long numberOfCharactersRead)
        {
            //TODO arek
            var result = new QueryResult
            {
                IsStale = json.Value<bool>(nameof(QueryResult.IsStale)),
                IndexTimestamp = json.Value<DateTime>(nameof(QueryResult.IndexTimestamp)),
                Includes = ((RavenJArray)json[nameof(QueryResult.Includes)]).Cast<RavenJObject>().ToList(),
                TotalResults = json.Value<int>(nameof(QueryResult.TotalResults)),
                IndexName = json.Value<string>(nameof(QueryResult.IndexName)),
                ResultEtag = json.Value<long>(nameof(QueryResult.SkippedResults)),
                SkippedResults = json.Value<int>(nameof(QueryResult.SkippedResults)),
                //Highlightings = (json.Value<RavenJObject>("Highlightings") ?? new RavenJObject())
                //    .JsonDeserialization<Dictionary<string, Dictionary<string, string[]>>>(),
                //ScoreExplanations = (json.Value<RavenJObject>("ScoreExplanations") ?? new RavenJObject())
                //.JsonDeserialization<Dictionary<string, string>>(),
                TimingsInMilliseconds = (json.Value<RavenJObject>(nameof(QueryResult.TimingsInMilliseconds)) ?? new RavenJObject()).JsonDeserialization<Dictionary<string, double>>(),
                LastQueryTime = json.Value<DateTime>(nameof(QueryResult.LastQueryTime))
            };

            result.ResultSize = numberOfCharactersRead;

            foreach (var r in ((RavenJArray)json[nameof(QueryResult.Results)]))
            {
                if (r.Type == JTokenType.Null)
                    result.Results.Add(null);
                else
                    result.Results.Add((RavenJObject)r);
            }

            if (json.ContainsKey("DurationMilliseconds"))
                result.DurationMilliseconds = json.Value<long>("DurationMilliseconds");

            //long totalTime;
            //if (TryParseTempRequestTime(tempRequestTime, out totalTime))
            //{
            //    result.DurationMilliseconds = totalTime;
            //}

            return result;
        }

        public static bool TryParseTempRequestTime(string tempRequestTime, out long result)
        {
            result = 0;
            if (string.IsNullOrEmpty(tempRequestTime) == false)
            {
                long l;
                if (long.TryParse(tempRequestTime, NumberStyles.Number, CultureInfo.InvariantCulture, out l))
                {
                    result = l;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Deserialize a request to a JsonDocument
        /// </summary>
        public static JsonDocument DeserializeJsonDocument(string key, RavenJToken requestJson,
                                                           HttpResponseHeaders headers,
                                                           HttpStatusCode statusCode)
        {
            var jsonData = (RavenJObject)requestJson;
            var metadata = headers.FilterHeadersToObject();

            var etag = headers.ETag.Tag;
            var lastModified = GetLastModifiedDate(headers);
            metadata[Constants.Headers.LastModified] = lastModified;

            return new JsonDocument
            {
                DataAsJson = jsonData,
                Key = key,
                Etag = HttpExtensions.EtagHeaderToEtag(etag),
                LastModified = lastModified,
                Metadata = metadata
            };
        }

        public static JsonDocument DeserializeJsonDocument(string key, RavenJToken requestJson,
                                                           NameValueCollection headers,
                                                           HttpStatusCode statusCode)
        {
            var jsonData = (RavenJObject)requestJson;
            var metadata = headers.FilterHeadersToObject();

            var etag = headers[Constants.MetadataEtagField];
            var lastModified = GetLastModifiedDate(headers);
            metadata[Constants.Headers.LastModified] = lastModified;

            return new JsonDocument
            {
                DataAsJson = jsonData,
                Key = key,
                Etag = HttpExtensions.EtagHeaderToEtag(etag),
                LastModified = lastModified,
                Metadata = metadata
            };
        }

        private static DateTime? GetLastModifiedDate(NameValueCollection headers)
        {
            var lastModified = headers.GetValues(Constants.Headers.RavenLastModified);
            if (lastModified == null || lastModified.Length != 1)
            {
                var dt = DateTime.ParseExact(headers[Constants.Headers.LastModified], new[] { "o", "r" }, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
            return DateTime.ParseExact(lastModified[0], new[] { "o", "r" }, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        private static DateTime? GetLastModifiedDate(HttpResponseHeaders headers)
        {
            var lastModified = headers.GetAllValues(Constants.Headers.RavenLastModified);
            if (lastModified == null || lastModified.Length != 1)
            {
                var dt = DateTime.ParseExact(headers.GetFirstValue(Constants.Headers.LastModified), new[] { "o", "r" }, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
            return DateTime.ParseExact(lastModified[0], new[] { "o", "r" }, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        /// <summary>
        /// Deserialize a request to a JsonDocument
        /// </summary>
        public static JsonDocumentMetadata DeserializeJsonDocumentMetadata(string key,
                                                                           HttpResponseHeaders headers,
                                                                           HttpStatusCode statusCode)
        {
            RavenJObject metadata = null;
            try
            {
                metadata = headers.FilterHeadersToObject();
            }
            catch (JsonReaderException jre)
            {
                throw new JsonReaderException("Invalid Json Response", jre);
            }
            var etag = headers.ETag.Tag;
            string lastModified = headers.GetFirstValue(Constants.Headers.RavenLastModified) ?? headers.GetFirstValue(Constants.Headers.LastModified);
            var dateTime = DateTime.ParseExact(lastModified, new[] { "o", "r" }, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var lastModifiedDate = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
            metadata[Constants.Headers.LastModified] = lastModified;

            return new JsonDocumentMetadata
            {
                Key = key,
                Etag = HttpExtensions.EtagHeaderToEtag(etag),
                LastModified = lastModifiedDate,
                Metadata = metadata
            };
        }

        /// <summary>
        /// Deserialize a request to a JsonDocument
        /// </summary>
        public static JsonDocumentMetadata DeserializeJsonDocumentMetadata(string key,
                                                                           NameValueCollection headers,
                                                                           HttpStatusCode statusCode)
        {
            RavenJObject metadata = null;
            try
            {
                metadata = headers.FilterHeadersToObject();
            }
            catch (JsonReaderException jre)
            {
                throw new JsonReaderException("Invalid Json Response", jre);
            }
            var etag = headers[Constants.MetadataEtagField];
            string lastModified = headers[Constants.Headers.RavenLastModified] ?? headers[Constants.Headers.LastModified];
            var dateTime = DateTime.ParseExact(lastModified, new[] { "o", "r" }, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var lastModifiedDate = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
            metadata[Constants.Headers.LastModified] = lastModified;

            return new JsonDocumentMetadata
            {
                Key = key,
                Etag = HttpExtensions.EtagHeaderToEtag(etag),
                LastModified = lastModifiedDate,
                Metadata = metadata
            };
        }
    }
}
