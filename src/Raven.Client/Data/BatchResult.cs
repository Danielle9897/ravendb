//-----------------------------------------------------------------------
// <copyright file="BatchResult.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Raven.Client.Data;
using Raven.Json.Linq;

namespace Raven.Abstractions.Data
{
    /// <summary>
    /// The result of a single operation inside a batch
    /// </summary>
    public class BatchResult
    {
        /// <summary>
        /// long? generated by the operation (null if not applicable).
        /// </summary>
        public long? Etag { get; set; }

        /// <summary>
        /// Method used by the operation (PUT,DELETE,PATCH).
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Key of a document.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Updated metadata of a document.
        /// </summary>
        public RavenJObject Metadata { get; set; }

        /// <summary>
        /// Additional operation data.
        /// </summary>
        public RavenJObject AdditionalData { get; set; }

        /// <summary>
        /// Result of a PATCH operation.
        /// </summary>
        public PatchResult? PatchResult { get; set; }

        /// <summary>
        /// Indicates if the document was deleted (null if not DELETE operation)
        /// <para>Value:</para>
        /// <para>- <c>true</c> - if the document was deleted</para>
        /// <para>- <c>false</c> - if it did not exist.</para>
        /// </summary>
        public bool? Deleted { get; set; }
    }
}
