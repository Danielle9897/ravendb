﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Raven.Client.Linq;
using Raven.Server.Utils;
using Sparrow.Json;

namespace Raven.Server.Documents.Indexes.Static
{
    public class DynamicArray : DynamicObject, IEnumerable<object>
    {
        private readonly IEnumerable<object> _inner;

        public DynamicArray(IEnumerable inner)
            : this(inner.Cast<object>())
        {
        }

        public DynamicArray(IEnumerable<object> inner)
        {
            _inner = inner;
        }

        public int Length => _inner.Count();

        public int Count => _inner.Count();

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            const string LengthName = "Length";
            const string CountName = "Count";
            result = null;
            if (string.CompareOrdinal(binder.Name, LengthName) == 0 ||
                string.CompareOrdinal(binder.Name, CountName) == 0)
            {
                result = Length;
                return true;
            }

            return false;
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            var i = (int)indexes[0];
            var resultObject = _inner.ElementAt(i);

            result = TypeConverter.ToDynamicType(resultObject);
            return true;
        }

        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            if (binder.ReturnType.IsArray)
            {
                var elementType = binder.ReturnType.GetElementType();
                var count = Count;
                var array = Array.CreateInstance(elementType, count);

                for (var i = 0; i < count; i++)
                {
                    var item = _inner.ElementAt(i);
                    if (elementType == typeof(string) && (item is LazyStringValue || item is LazyCompressedStringValue))
                        array.SetValue(item.ToString(), i);
                    else
                        array.SetValue(Convert.ChangeType(item, elementType), i);
                }

                result = array;

                return true;
            }

            return base.TryConvert(binder, out result);
        }

        public IEnumerator<object> GetEnumerator()
        {
            return new DynamicArrayIterator(_inner);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Contains(object item)
        {
            return Enumerable.Contains(this, item);
        }

        public int Sum(Func<dynamic, int> selector)
        {
            return Enumerable.Sum(this, selector);
        }

        public int? Sum(Func<dynamic, int?> selector)
        {
            return Enumerable.Sum(this, selector) ?? DynamicNullObject.Null;
        }

        public long Sum(Func<dynamic, long> selector)
        {
            return Enumerable.Sum(this, selector);
        }

        public long? Sum(Func<dynamic, long?> selector)
        {
            return Enumerable.Sum(this, selector) ?? DynamicNullObject.Null;
        }

        public float Sum(Func<dynamic, float> selector)
        {
            return Enumerable.Sum(this, selector);
        }

        public float? Sum(Func<dynamic, float?> selector)
        {
            return Enumerable.Sum(this, selector) ?? DynamicNullObject.Null;
        }

        public double Sum(Func<dynamic, double> selector)
        {
            return Enumerable.Sum(this, selector);
        }

        public double? Sum(Func<dynamic, double?> selector)
        {
            return Enumerable.Sum(this, selector) ?? DynamicNullObject.Null;
        }

        public decimal Sum(Func<dynamic, decimal> selector)
        {
            return Enumerable.Sum(this, selector);
        }

        public decimal? Sum(Func<dynamic, decimal?> selector)
        {
            return Enumerable.Sum(this, selector) ?? DynamicNullObject.Null;
        }

        public dynamic Min()
        {
            return Enumerable.Min(this) ?? DynamicNullObject.Null;
        }

        public dynamic Min<TResult>(Func<dynamic, TResult> selector)
        {
            var result = Enumerable.Min(this, selector);

            if (result == null)
                return DynamicNullObject.Null;

            return result;
        }

        public dynamic Max()
        {
            return Enumerable.Max(this) ?? DynamicNullObject.Null;
        }

        public dynamic Max<TResult>(Func<dynamic, TResult> selector)
        {
            var result = Enumerable.Max(this, selector);

            if (result == null)
                return DynamicNullObject.Null;

            return result;
        }

        public double Average(Func<dynamic, int> selector)
        {
            return Enumerable.Average(this, selector);
        }

        public double? Average(Func<dynamic, int?> selector)
        {
            return Enumerable.Average(this, selector) ?? DynamicNullObject.Null;
        }

        public double Average(Func<dynamic, long> selector)
        {
            return Enumerable.Average(this, selector);
        }

        public double? Average(Func<dynamic, long?> selector)
        {
            return Enumerable.Average(this, selector) ?? DynamicNullObject.Null;
        }

        public float Average(Func<dynamic, float> selector)
        {
            return Enumerable.Average(this, selector);
        }

        public float? Average(Func<dynamic, float?> selector)
        {
            return Enumerable.Average(this, selector) ?? DynamicNullObject.Null;
        }

        public double Average(Func<dynamic, double> selector)
        {
            return Enumerable.Average(this, selector);
        }

        public double? Average(Func<dynamic, double?> selector)
        {
            return Enumerable.Average(this, selector) ?? DynamicNullObject.Null;
        }

        public decimal Average(Func<dynamic, decimal> selector)
        {
            return Enumerable.Average(this, selector);
        }

        public decimal? Average(Func<dynamic, decimal?> selector)
        {
            return Enumerable.Average(this, selector) ?? DynamicNullObject.Null;
        }

        public IEnumerable<dynamic> OrderBy(Func<dynamic, dynamic> comparable)
        {
            return new DynamicArray(Enumerable.OrderBy(this, comparable));
        }

        public IEnumerable<dynamic> OrderByDescending(Func<dynamic, dynamic> comparable)
        {
            return new DynamicArray(Enumerable.OrderByDescending(this, comparable));
        }

        public dynamic GroupBy(Func<dynamic, dynamic> keySelector)
        {
            return new DynamicArray(Enumerable.GroupBy(this, keySelector).Select(x => new DynamicGrouping(x)));
        }

        public dynamic GroupBy(Func<dynamic, dynamic> keySelector, Func<dynamic, dynamic> selector)
        {
            return new DynamicArray(Enumerable.GroupBy(this, keySelector, selector).Select(x => new DynamicGrouping(x)));
        }

        public dynamic Last()
        {
            return Enumerable.Last(this);
        }

        public dynamic LastOrDefault()
        {
            return Enumerable.LastOrDefault(this) ?? DynamicNullObject.Null;
        }

        public dynamic Last(Func<dynamic, bool> predicate)
        {
            return Enumerable.Last(this, predicate);
        }

        public dynamic LastOrDefault(Func<dynamic, bool> predicate)
        {
            return Enumerable.LastOrDefault(this, predicate) ?? DynamicNullObject.Null;
        }

        public dynamic IndexOf(dynamic item)
        {
            var items = Enumerable.ToList(this);
            return items.IndexOf(item);
        }

        public dynamic IndexOf(dynamic item, int index)
        {
            var items = Enumerable.ToList(this);
            return items.IndexOf(item, index);
        }

        public dynamic IndexOf(dynamic item, int index, int count)
        {
            var items = Enumerable.ToList(this);
            return items.IndexOf(item, index, count);
        }

        public dynamic LastIndexOf(dynamic item)
        {
            var items = Enumerable.ToList(this);
            return items.LastIndexOf(item);
        }

        public dynamic LastIndexOf(dynamic item, int index)
        {
            var items = Enumerable.ToList(this);
            return items.LastIndexOf(item, index);
        }

        public dynamic LastIndexOf(dynamic item, int index, int count)
        {
            var items = Enumerable.ToList(this);
            return items.LastIndexOf(item, index, count);
        }

        public IEnumerable<dynamic> Take(int count)
        {
            return new DynamicArray(Enumerable.Take(this, count));
        }

        public IEnumerable<dynamic> Skip(int count)
        {
            return new DynamicArray(Enumerable.Skip(this, count));
        }

        public IEnumerable<object> Select(Func<object, object> func)
        {
            return new DynamicArray(Enumerable.Select(this, func));
        }

        public IEnumerable<object> Select(Func<IGrouping<object, object>, object> func)
        {
            return new DynamicArray(Enumerable.Select(this, o => func((IGrouping<object, object>)o)));
        }

        public IEnumerable<object> Select(Func<object, int, object> func)
        {
            return new DynamicArray(Enumerable.Select(this, func));
        }

        public IEnumerable<object> SelectMany(Func<object, IEnumerable<object>> func)
        {
            return new DynamicArray(Enumerable.SelectMany(this, func));
        }

        public IEnumerable<object> SelectMany(Func<object, IEnumerable<object>> func, Func<object, object, object> selector)
        {
            return new DynamicArray(Enumerable.SelectMany(this, func, selector));
        }

        public IEnumerable<object> SelectMany(Func<object, int, IEnumerable<object>> func)
        {
            return new DynamicArray(Enumerable.SelectMany(this, func));
        }

        public IEnumerable<object> Where(Func<object, bool> func)
        {
            return new DynamicArray(Enumerable.Where(this, func));
        }

        public IEnumerable<object> Where(Func<object, int, bool> func)
        {
            return new DynamicArray(Enumerable.Where(this, func));
        }

        public dynamic DefaultIfEmpty(object defaultValue = null)
        {
            return Enumerable.DefaultIfEmpty(this, defaultValue ?? DynamicNullObject.Null);
        }

        public IEnumerable<dynamic> Except(IEnumerable<dynamic> except)
        {
            return new DynamicArray(Enumerable.Except(this, except));
        }

        public IEnumerable<dynamic> Reverse()
        {
            return new DynamicArray(Enumerable.Reverse(this));
        }

        public bool SequenceEqual(IEnumerable<dynamic> second)
        {
            return Enumerable.SequenceEqual(this, second);
        }

        public IEnumerable<dynamic> AsEnumerable()
        {
            return this;
        }

        public dynamic[] ToArray()
        {
            return Enumerable.ToArray(this);
        }

        public List<dynamic> ToList()
        {
            return Enumerable.ToList(this);
        }

        public Dictionary<TKey, dynamic> ToDictionary<TKey>(Func<dynamic, TKey> keySelector, Func<dynamic, dynamic> elementSelector = null)
        {
            if (elementSelector == null)
                return Enumerable.ToDictionary(this, keySelector);

            return Enumerable.ToDictionary(this, keySelector, elementSelector);
        }

        public ILookup<TKey, dynamic> ToLookup<TKey>(Func<dynamic, TKey> keySelector, Func<dynamic, dynamic> elementSelector = null)
        {
            if (elementSelector == null)
                return Enumerable.ToLookup(this, keySelector);

            return Enumerable.ToLookup(this, keySelector, elementSelector);
        }

        public IEnumerable<dynamic> OfType<T>()
        {
            return new DynamicArray(Enumerable.OfType<T>(this));
        }

        public IEnumerable<dynamic> Cast<T>()
        {
            return new DynamicArray(Enumerable.Cast<T>(this));
        }

        public dynamic ElementAt(int index)
        {
            return Enumerable.ElementAt(this, index);
        }

        public dynamic ElementAtOrDefault(int index)
        {
            return Enumerable.ElementAtOrDefault(this, index) ?? DynamicNullObject.Null;
        }

        public long LongCount()
        {
            return Enumerable.LongCount(this);
        }

        public dynamic Aggregate(Func<dynamic, dynamic, dynamic> func)
        {
            return Enumerable.Aggregate(this, func);
        }

        public dynamic Aggregate(dynamic seed, Func<dynamic, dynamic, dynamic> func)
        {
            return Enumerable.Aggregate(this, (object)seed, func);
        }

        public dynamic Aggregate(dynamic seed, Func<dynamic, dynamic, dynamic> func, Func<dynamic, dynamic> resultSelector)
        {
            return Enumerable.Aggregate(this, (object)seed, func, resultSelector);
        }

        public IEnumerable<dynamic> TakeWhile(Func<dynamic, bool> predicate)
        {
            return new DynamicArray(Enumerable.TakeWhile(this, predicate));
        }

        public IEnumerable<dynamic> TakeWhile(Func<dynamic, int, bool> predicate)
        {
            return new DynamicArray(Enumerable.TakeWhile(this, predicate));
        }

        public IEnumerable<dynamic> SkipWhile(Func<dynamic, bool> predicate)
        {
            return new DynamicArray(Enumerable.SkipWhile(this, predicate));
        }

        public IEnumerable<dynamic> SkipWhile(Func<dynamic, int, bool> predicate)
        {
            return new DynamicArray(Enumerable.SkipWhile(this, predicate));
        }

        public IEnumerable<dynamic> Join(IEnumerable<dynamic> items, Func<dynamic, dynamic> outerKeySelector, Func<dynamic, dynamic> innerKeySelector,
                                            Func<dynamic, dynamic, dynamic> resultSelector)
        {
            return new DynamicArray(Enumerable.Join(this, items, outerKeySelector, innerKeySelector, resultSelector));
        }

        public IEnumerable<dynamic> GroupJoin(IEnumerable<dynamic> items, Func<dynamic, dynamic> outerKeySelector, Func<dynamic, dynamic> innerKeySelector,
                                            Func<dynamic, dynamic, dynamic> resultSelector)
        {
            return new DynamicArray(Enumerable.GroupJoin(this, items, outerKeySelector, innerKeySelector, resultSelector));
        }

        public IEnumerable<dynamic> Concat(IEnumerable second)
        {
            return new DynamicArray(Enumerable.Concat(this, second.Cast<object>()));
        }

        public IEnumerable<dynamic> Zip(IEnumerable second, Func<dynamic, dynamic, dynamic> resultSelector)
        {
            return new DynamicArray(Enumerable.Zip(this, second.Cast<object>(), resultSelector));
        }

        public IEnumerable<dynamic> Union(IEnumerable second)
        {
            return new DynamicArray(Enumerable.Union(this, second.Cast<object>()));
        }

        public IEnumerable<dynamic> Intersect(IEnumerable second)
        {
            return new DynamicArray(Enumerable.Intersect(this, second.Cast<object>()));
        }

        private class DynamicArrayIterator : IEnumerator<object>
        {
            private readonly IEnumerator<object> _inner;

            public DynamicArrayIterator(IEnumerable<object> items)
            {
                _inner = items.GetEnumerator();
            }

            public bool MoveNext()
            {
                if (_inner.MoveNext() == false)
                    return false;


                Current = TypeConverter.ToDynamicType(_inner.Current);
                return true;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }

            public object Current { get; private set; }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            var array = obj as DynamicArray;

            if (array != null)
                return Equals(_inner, array._inner);

            return Equals(_inner, obj);
        }

        public override int GetHashCode()
        {
            return _inner?.GetHashCode() ?? 0;
        }

        public class DynamicGrouping : DynamicArray, IGrouping<object, object>
        {
            private readonly IGrouping<dynamic, dynamic> _grouping;

            public DynamicGrouping(IGrouping<dynamic, dynamic> grouping)
                : base(grouping)
            {
                _grouping = grouping;
            }

            public dynamic Key => _grouping.Key;

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}