//-----------------------------------------------------------------------
// <copyright file="IndexDefinition.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Raven.Abstractions.Extensions;
using Raven.Abstractions.Indexing;
using Raven.Abstractions.Util;
using Raven.Client.Data.Indexes;
using Raven.Imports.Newtonsoft.Json;

namespace Raven.Client.Indexing
{
    /// <summary>
    /// A definition of a RavenIndex
    /// </summary>
    public class IndexDefinition
    {
        public IndexDefinition()
        {
            _configuration = new IndexConfiguration();
        }

        /// <summary>
        /// Index identifier (internal).
        /// </summary>
        public int IndexId { get; set; }

        /// <summary>
        /// This is the means by which the outside world refers to this index definition
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Index lock mode:
        /// <para>- Unlock - all index definition changes acceptable</para>
        /// <para>- LockedIgnore - all index definition changes will be ignored, only log entry will be created</para>
        /// <para>- LockedError - all index definition changes will raise exception</para>
        /// </summary>
        public IndexLockMode LockMode { get; set; }

        /// <summary>
        /// All the map functions for this index
        /// </summary>
        public HashSet<string> Maps
        {
            get { return _maps ?? (_maps = new HashSet<string>()); }
            set { _maps = value; }
        }

        /// <summary>
        /// Index reduce function
        /// </summary>
        public string Reduce { get; set; }

        public Dictionary<string, IndexFieldOptions> Fields
        {
            get { return _fields ?? (_fields = new Dictionary<string, IndexFieldOptions>()); }
            set { _fields = value; }
        }

        public IndexConfiguration Configuration
        {
            get { return _configuration ?? (_configuration = new IndexConfiguration()); }
            set { _configuration = value; }
        }

        public IndexDefinitionCompareDifferences Compare(IndexDefinition other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            var result = IndexDefinitionCompareDifferences.None;

            if (ReferenceEquals(this, other))
                return result;

            if (Equals(IndexId, other.IndexId) == false)
                result |= IndexDefinitionCompareDifferences.IndexId;

            if (Maps.SequenceEqual(other.Maps) == false)
            {
                if (Maps.SequenceEqual(other.Maps, IndexPrettyPrinterEqualityComparer.Instance))
                    result |= IndexDefinitionCompareDifferences.MapsFormatting;
                else
                    result |= IndexDefinitionCompareDifferences.Maps;
            }

            if (Equals(Reduce, other.Reduce) == false)
            {
                if (IndexPrettyPrinterEqualityComparer.Instance.Equals(Reduce, other.Reduce))
                    result |= IndexDefinitionCompareDifferences.ReduceFormatting;
                else
                    result |= IndexDefinitionCompareDifferences.Reduce;
            }

            if (DictionaryExtensions.ContentEquals(other.Fields, Fields) == false)
                result |= IndexDefinitionCompareDifferences.Fields;

            bool configurationEquals;
            if (other._configuration == null && _configuration == null)
                configurationEquals = true;
            else if (other._configuration != null)
                configurationEquals = other._configuration.Equals(_configuration);
            else
                configurationEquals = _configuration.Equals(other._configuration);

            if (configurationEquals == false)
                result |= IndexDefinitionCompareDifferences.Configuration;

            return result;
        }

        /// <summary>
        /// Equals the specified other.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <param name="compareIndexIds">allow caller to choose whether to include the index Id in the comparison</param>
        /// <param name="ignoreFormatting">Comparision ignores formatting in both of the definitions</param>
        public bool Equals(IndexDefinition other, bool compareIndexIds = true, bool ignoreFormatting = false)
        {
            if (ReferenceEquals(null, other))
                return false;

            if (ReferenceEquals(this, other))
                return true;

            var result = Compare(other);

            if (result == IndexDefinitionCompareDifferences.None)
                return true;

            if (compareIndexIds && result.HasFlag(IndexDefinitionCompareDifferences.IndexId))
                return false;

            var mapsReduceEquals = ignoreFormatting
                ? result.HasFlag(IndexDefinitionCompareDifferences.MapsFormatting) == false && result.HasFlag(IndexDefinitionCompareDifferences.ReduceFormatting) == false
                : result.HasFlag(IndexDefinitionCompareDifferences.Maps) == false && result.HasFlag(IndexDefinitionCompareDifferences.Reduce) == false;

            var configurationEquals = result.HasFlag(IndexDefinitionCompareDifferences.Configuration) == false;
            var fieldsEquals = result.HasFlag(IndexDefinitionCompareDifferences.Fields) == false;

            return mapsReduceEquals
                   && configurationEquals
                   && fieldsEquals;
        }

        private static int DictionaryHashCode<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> x)
        {
            int result = 0;
            foreach (var kvp in x)
            {
                result = (result * 397) ^ kvp.Key.GetHashCode();
                result = (result * 397) ^ (!Equals(kvp.Value, default(TValue)) ? kvp.Value.GetHashCode() : 0);
            }
            return result;
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object"/> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object"/> to compare with this instance.</param>
        /// <returns>
        /// 	<c>true</c> if the specified <see cref="System.Object"/> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            return Equals(obj as IndexDefinition);
        }

        [JsonIgnore]
        private byte[] _cachedHashCodeAsBytes;
        [JsonIgnore]
        private HashSet<string> _maps;
        [JsonIgnore]
        private Dictionary<string, IndexFieldOptions> _fields;
        [JsonIgnore]
        private IndexConfiguration _configuration;

        /// <summary>
        /// Provide a cached version of the index hash code, which is used when generating
        /// the index etag. 
        /// It isn't really useful for anything else, in particular, we cache that because
        /// we want to avoid calculating the cost of doing this over and over again on each 
        /// query.
        /// </summary>
        public byte[] GetIndexHash()
        {
            if (_cachedHashCodeAsBytes != null)
                return _cachedHashCodeAsBytes;

            _cachedHashCodeAsBytes = BitConverter.GetBytes(GetHashCode());
            return _cachedHashCodeAsBytes;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int result = Maps.Where(x => x != null).Aggregate(0, (acc, val) => acc * 397 ^ val.GetHashCode());
                result = (result * 397) ^ Maps.Count;
                result = (result * 397) ^ (Reduce?.GetHashCode() ?? 0);
                result = (result * 397) ^ DictionaryHashCode(Fields);
                return result;
            }
        }

        private IndexType? _indexType;

        public IndexType Type
        {
            get
            {
                if (_indexType == null || _indexType.Value == IndexType.None)
                {
                    _indexType = DetectStaticIndexType();
                }

                return _indexType.Value;
            }
            internal set
            {
                _indexType = value;
            }
        }


        /// <summary>
        /// Remove the default values that we don't actually need
        /// </summary>
        public void RemoveDefaultValues()
        {
            var toRemove = new List<string>();
            foreach (var kvp in Fields)
            {
                var allDefault = true;
                var field = kvp.Value;

                if (field != null)
                {
                    if (field.Storage.HasValue)
                    {
                        if (field.Storage == FieldStorage.No)
                            field.Storage = null;
                        else
                            allDefault = false;
                    }

                    if (field.Indexing.HasValue)
                    {
                        if (field.Indexing == FieldIndexing.Default)
                            field.Indexing = null;
                        else
                            allDefault = false;
                    }

                    if (string.IsNullOrWhiteSpace(field.Analyzer))
                        field.Analyzer = null;
                    else
                        allDefault = false;

                    if (field.Sort.HasValue)
                    {
                        if (field.Sort == SortOptions.None)
                            field.Sort = null;
                        else
                            allDefault = false;
                    }

                    if (field.TermVector.HasValue)
                    {
                        if (field.TermVector == FieldTermVector.No)
                            field.TermVector = null;
                        else
                            allDefault = false;
                    }

                    if (field.Suggestions.HasValue)
                    {
                        if (field.Suggestions == false)
                            field.Suggestions = null;
                        else
                            allDefault = false;
                    }

                    allDefault = allDefault && (field.Spatial == null);
                }

                if (allDefault)
                    toRemove.Add(kvp.Key);
            }

            foreach (var key in toRemove)
                Fields.Remove(key);
        }

        private IndexType DetectStaticIndexType()
        {
            if (string.IsNullOrWhiteSpace(Reduce))
                return IndexType.Map;

            return IndexType.MapReduce;
        }

        /// <summary>
        /// Whatever this is a temporary test only index
        /// </summary>
        public bool IsTestIndex { get; set; }

        /// <summary>
        /// Whatever this is a side by side index
        /// </summary>
        public bool IsSideBySideIndex { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    [Flags]
    public enum IndexDefinitionCompareDifferences
    {
        None = 0,
        IndexId = 1 << 0,
        Maps = 1 << 1,
        MapsFormatting = 1 << 2,
        Reduce = 1 << 3,
        ReduceFormatting = 1 << 4,
        Fields = 1 << 5,
        Configuration = 1 << 6,

        All = IndexId | Maps | MapsFormatting | Reduce | ReduceFormatting | Fields | Configuration
    }
}
