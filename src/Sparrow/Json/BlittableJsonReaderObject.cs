using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Sparrow.Binary;
using Sparrow.Json.Parsing;

namespace Sparrow.Json
{
    public unsafe class BlittableJsonReaderObject : BlittableJsonReaderBase, IDisposable
    {
        private UnmanagedWriteBuffer _buffer;
        private byte* _metadataPtr;
        private readonly int _size;
        private readonly int _propCount;
        private readonly long _currentOffsetSize;
        private readonly long _currentPropertyIdSize;
        private byte* _objStart;

        public DynamicJsonValue Modifications;
        internal LinkedListNode<BlittableJsonReaderObject> DisposeTrackingReference;

        private Dictionary<StringSegment, object> _objectsPathCache;
        private Dictionary<int, object> _objectsPathCacheByIndex;
        public string _allocation;

        public override string ToString()
        {
            var memoryStream = new MemoryStream();

            WriteJsonTo(memoryStream);

            memoryStream.Position = 0;

            return new StreamReader(memoryStream).ReadToEnd();
        }

        public void WriteJsonTo(Stream stream)
        {
            _context.Write(stream, this);
        }

        public BlittableJsonReaderObject(byte* mem, int size, JsonOperationContext context,
            UnmanagedWriteBuffer buffer = default(UnmanagedWriteBuffer))
        {
            if (size == 0)
                ThrowOnZeroSize(size);

            _buffer = buffer;
            _mem = mem; // get beginning of memory pointer
            _size = size; // get document size
            _context = context;

            byte offset;
            var propOffsetStart = _size - 2;
            var propsOffset = ReadVariableSizeIntInReverse(_mem, propOffsetStart, out offset);
            // init document level properties
            SetupPropertiesAccess(mem, propsOffset);

            // get pointer to property names array on document level

            // init root level object properties
            var objStartOffset = ReadVariableSizeIntInReverse(_mem, propOffsetStart - offset, out offset);
            // get offset of beginning of data of the main object
            byte propCountOffset;
            _propCount = ReadVariableSizeInt(objStartOffset, out propCountOffset); // get main object properties count
            _objStart = objStartOffset + mem;
            _metadataPtr = objStartOffset + mem + propCountOffset;
            // get pointer to current objects property tags metadata collection

            var currentType = (BlittableJsonToken)(*(mem + size - sizeof(byte)));
            // get current type byte flags

            // analyze main object type and it's offset and propertyIds flags
            _currentOffsetSize = ProcessTokenOffsetFlags(currentType);
            _currentPropertyIdSize = ProcessTokenPropertyFlags(currentType);
        }

        private static void ThrowOnZeroSize(int size)
        {
                //otherwise SetupPropertiesAccess will throw because of the memory garbage
                //(or won't throw, but this is actually worse!)
                throw new ArgumentException("BlittableJsonReaderObject does not support objects with zero size", nameof(size));
        }

        private void SetupPropertiesAccess(byte* mem, int propsOffset)
        {
            _propNames = (mem + propsOffset);
            var propNamesOffsetFlag = (BlittableJsonToken)(*_propNames);
            switch (propNamesOffsetFlag)
            {
                case BlittableJsonToken.OffsetSizeByte:
                    _propNamesDataOffsetSize = sizeof(byte);
                    break;
                case BlittableJsonToken.OffsetSizeShort:
                    _propNamesDataOffsetSize = sizeof(short);
                    break;
                case BlittableJsonToken.OffsetSizeInt:
                    _propNamesDataOffsetSize = sizeof(int);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        $"Property names offset flag should be either byte, short of int, instead of {propNamesOffsetFlag}");
            }
        }

        public BlittableJsonReaderObject(int pos, BlittableJsonReaderObject parent, BlittableJsonToken type)
        {
            _parent = parent;
            _context = parent._context;
            _mem = parent._mem;
            _size = parent._size;
            _propNames = parent._propNames;

            var propNamesOffsetFlag = (BlittableJsonToken)(*_propNames);
            switch (propNamesOffsetFlag)
            {
                case BlittableJsonToken.OffsetSizeByte:
                    _propNamesDataOffsetSize = sizeof(byte);
                    break;
                case BlittableJsonToken.OffsetSizeShort:
                    _propNamesDataOffsetSize = sizeof(short);
                    break;
                case BlittableJsonToken.OffsetSizeInt:
                    _propNamesDataOffsetSize = sizeof(int);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        $"Property names offset flag should be either byte, short of int, instead of {propNamesOffsetFlag}");
            }

            _objStart = _mem + pos;
            byte propCountOffset;
            _propCount = ReadVariableSizeInt(pos, out propCountOffset);
            _metadataPtr = _objStart + propCountOffset;

            // analyze main object type and it's offset and propertyIds flags
            _currentOffsetSize = ProcessTokenOffsetFlags(type);
            _currentPropertyIdSize = ProcessTokenPropertyFlags(type);
        }

        private static void ThrowObjectDisposed()
        {
            throw new ObjectDisposedException("blittalbe object has been disposed");
        }

        public int Size => _size;

        public int Count => _propCount;
        public byte* BasePointer => _mem;


        /// <summary>
        /// Returns an array of property names, ordered in the order it was stored 
        /// </summary>
        /// <returns></returns>
        public string[] GetPropertyNames()
        {
            var offsets = new int[_propCount];
            var propertyNames = new string[_propCount];

            var metadataSize = (_currentOffsetSize + _currentPropertyIdSize + sizeof(byte));

            for (int i = 0; i < _propCount; i++)
            {
                BlittableJsonToken token;
                int position;
                int id;
                GetPropertyTypeAndPosition(i, metadataSize,out token, out position, out id);
                offsets[i] = position;
                propertyNames[i] = GetPropertyName(id);
            }

            // sort according to offsets
            Array.Sort(offsets, propertyNames, NumericDescendingComparer.Instance);

            return propertyNames;
        }

        private LazyStringValue GetPropertyName(int propertyId)
        {
            var propertyNameOffsetPtr = _propNames + sizeof(byte) + propertyId*_propNamesDataOffsetSize;
            var propertyNameOffset = ReadNumber(propertyNameOffsetPtr, _propNamesDataOffsetSize);

            // Get the relative "In Document" position of the property Name
            var propRelativePos = _propNames - propertyNameOffset - _mem;

            var propertyName = ReadStringLazily((int) propRelativePos);
            return propertyName;
        }


        public object this[string name]
        {
            get
            {
                object result = null;
                if (TryGetMember(name, out result) == false)
                    throw new ArgumentException($"Member named {name} does not exist");
                return result;
            }
        }

        public bool TryGet<T>(string name, out T obj)
        {
            return TryGet(new StringSegment(name, 0, name.Length), out obj);
        }

        public bool TryGet<T>(StringSegment name, out T obj)
        {
            object result;
            if (TryGetMember(name, out result) == false)
            {
                obj = default(T);
                return false;
            }
            ConvertType(result, out obj);
            return true;
        }

        internal static void ConvertType<T>(object result, out T obj)
        {
            if (result == null)
            {
                obj = default(T);
            }
            else if (result is T)
            {
                obj = (T)result;
            }
            else
            {
                var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                try
                {
                    if (type.GetTypeInfo().IsEnum)
                    {
                        obj = (T)Enum.Parse(type, result.ToString());
                    }
                    else if(type == typeof(DateTime))
                    {
                        string dateTimeString;
                        if (ChangeTypeToString(result, out dateTimeString) == false)
                            throw new FormatException($"Could not convert {result.GetType().FullName} ('{result}') to string");
                        DateTime time;
                        if (DateTime.TryParseExact(dateTimeString, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out time) == false)
                            throw new FormatException($"Could not convert {result.GetType().FullName} ('{result}') to DateTime");
                        obj = (T)(object)time;
                    }
                    else if (type == typeof (Guid))
                    {
                        string guidString;
                        if (ChangeTypeToString(result, out guidString) == false)
                            throw new FormatException($"Could not convert {result.GetType().FullName} ('{result}') to string");
                        Guid guid;
                        if (Guid.TryParse(guidString, out guid) == false)
                            throw new FormatException($"Could not convert {result.GetType().FullName} ('{result}') to Guid");
                        obj = (T)(object)guid;
                    }
                    else
                    {
                        var lazyStringValue = result as LazyStringValue;
                        if (lazyStringValue != null)
                        {
                            obj = (T)Convert.ChangeType(lazyStringValue.ToString(), type);
                        }
                        else
                        {
                            obj = (T)Convert.ChangeType(result, type);
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new FormatException($"Could not convert {result.GetType().FullName} to {type.FullName}", e);
                }
            }
        }

        public bool TryGet(string name, out double dbl)
        {
            return TryGet(new StringSegment(name, 0, name.Length), out dbl);
        }

        public bool TryGet(StringSegment name, out double dbl)
        {
            object result;
            if (TryGetMember(name, out result) == false)
            {
                dbl = 0;
                return false;
            }

            var lazyDouble = result as LazyDoubleValue;
            if (lazyDouble != null)
            {
                dbl = lazyDouble;
                return true;
            }

            dbl = 0;
            return false;
        }

        public bool TryGet(string name, out string str)
        {
            return TryGet(new StringSegment(name, 0, name.Length), out str);
        }

        public bool TryGet(StringSegment name, out string str)
        {
            object result;
            if (TryGetMember(name, out result) == false)
            {
                str = null;
                return false;
            }
            return ChangeTypeToString(result, out str);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ChangeTypeToString(object result, out string str)
        {
            var lazyCompressedStringValue = result as LazyCompressedStringValue;
            if (lazyCompressedStringValue != null)
            {
                str = lazyCompressedStringValue;
                return true;
            }

            var lazyStringValue = result as LazyStringValue;
            if (lazyStringValue != (LazyStringValue)null)
            {
                str = (string)lazyStringValue;
                return true;
            }

            str = null;
            return false;
        }

        public bool TryGetMember(string name, out object result)
        {
            return TryGetMember(new StringSegment(name, 0, name.Length), out result);
        }


        public bool TryGetMember(StringSegment name, out object result)
        {
            if (_mem == null)
                ThrowObjectDisposed();
            // try get value from cache, works only with Blittable types, other objects are not stored for now
            if (_objectsPathCache != null && _objectsPathCache.TryGetValue(name, out result))
            {
                return true;
            }
            var index = GetPropertyIndex(name);
            if (index == -1)
            {
                result = null;
                return false;
            }
            var metadataSize = (_currentOffsetSize + _currentPropertyIdSize + sizeof(byte));
            BlittableJsonToken token;
            int position;
            int propertyId;
            GetPropertyTypeAndPosition(index, metadataSize, out token, out position,out propertyId);
            result = GetObject(token, (int)(_objStart - _mem - position));
            if (result is BlittableJsonReaderBase)
            {
                AddToCache(name, result, index);
            }
            return true;
        }

        private void AddToCache(StringSegment name, object result, int index)
        {
            if (_objectsPathCache == null)
            {
                _objectsPathCache = new Dictionary<StringSegment, object>();
                _objectsPathCacheByIndex = new Dictionary<int, object>();
            }
            _objectsPathCache[name] = result;
            _objectsPathCacheByIndex[index] = result;
        }


        private void GetPropertyTypeAndPosition(int index, long metadataSize, out BlittableJsonToken token, out int position, out int propertyId)
        {
            var propPos = _metadataPtr + index * metadataSize;
            position  = ReadNumber(propPos, _currentOffsetSize);
            propertyId = ReadNumber(propPos + _currentOffsetSize, _currentPropertyIdSize);
            token = (BlittableJsonToken) (*(propPos + _currentOffsetSize + _currentPropertyIdSize));
        }


        public struct PropertyDetails
        {
            public LazyStringValue Name;
            public object Value;
            public BlittableJsonToken Token;
        }

        public void GetPropertyByIndex(int index, ref PropertyDetails prop, bool addObjectToCache = false)
        {
            if (index < 0 || index >= _propCount)
                ThrowOutOfRangeException();

            var metadataSize = (_currentOffsetSize + _currentPropertyIdSize + sizeof(byte));
            BlittableJsonToken token;
            int position;
            int propertyId;
            GetPropertyTypeAndPosition(index, metadataSize, out token, out position,out propertyId);

            var stringValue = GetPropertyName(propertyId);

            prop.Token = token;
            prop.Name = stringValue;
            object result;
            if (_objectsPathCacheByIndex != null && _objectsPathCacheByIndex.TryGetValue(index, out result))
            {
                prop.Value = result;
                return;
            }

            var value = GetObject(token, (int)(_objStart - _mem - position));

            if (addObjectToCache)
            {
                AddToCache(stringValue.ToString(), value, index);

            }

            prop.Value = value;
        }

        private static void ThrowOutOfRangeException()
        {
            // ReSharper disable once NotResolvedInText
            throw new ArgumentOutOfRangeException("index");
        }

        public int GetPropertyIndex(string name)
        {
            return GetPropertyIndex(new StringSegment(name, 0, name.Length));
        }


        public int GetPropertyIndex(StringSegment name)
        {
            if (_propCount == 0)
                return -1;

            int min = 0, max = _propCount - 1;
            var comparer = _context.GetLazyStringForFieldWithCaching(name.Value);

            int mid = comparer.LastFoundAt ?? (min + max) / 2;
            if (mid > max)
                mid = max;
            do
            {
                var metadataSize = (_currentOffsetSize + _currentPropertyIdSize + sizeof(byte));
                var propertyIntPtr = (long)_metadataPtr + (mid) * metadataSize;

                var propertyId = ReadNumber((byte*)propertyIntPtr + _currentOffsetSize, _currentPropertyIdSize);


                var cmpResult = ComparePropertyName(propertyId, comparer);
                if (cmpResult == 0)
                {
                    return mid;
                }
                if (cmpResult > 0)
                {
                    min = mid + 1;
                }
                else
                {
                    max = mid - 1;
                }

                mid = (min + max) / 2;

            } while (min <= max);
            return -1;
        }

        /// <summary>
        /// Compares property names between received StringToByteComparer and the string stored in the document's property names storage
        /// </summary>
        /// <param name="propertyId">Position of the string in the property ids storage</param>
        /// <param name="comparer">Comparer of a specific string value</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe int ComparePropertyName(int propertyId, LazyStringValue comparer)
        {
            // Get the offset of the property name from the _proprNames position
            var propertyNameOffsetPtr = _propNames + 1 + propertyId * _propNamesDataOffsetSize;
            var propertyNameOffset = ReadNumber(propertyNameOffsetPtr, _propNamesDataOffsetSize);

            // Get the relative "In Document" position of the property Name
            var propertyNameRelativePosition = _propNames - propertyNameOffset;
            var position = propertyNameRelativePosition - _mem;

            byte propertyNameLengthDataLength;

            // Get the property name size
            var size = ReadVariableSizeInt((int)position, out propertyNameLengthDataLength);

            // Return result of comparison between property name and received comparer
            return comparer.Compare(propertyNameRelativePosition + propertyNameLengthDataLength, size);
        }

        public class PropertiesInsertionBuffer
        {
            public int[] Properties;
            public int[] Offsets;
        }

        public int GetPropertiesByInsertionOrder(PropertiesInsertionBuffer buffers)
        {
            if (buffers.Properties == null ||
                buffers.Properties.Length < _propCount)
            {
                var size = Bits.NextPowerOf2(_propCount);
                buffers.Properties = new int[size];
                buffers.Offsets = new int[size];
            }
            var metadataSize = _currentOffsetSize + _currentPropertyIdSize + sizeof(byte);
            for (int i = 0; i < _propCount; i++)
            {
                var propertyIntPtr = _metadataPtr + i * metadataSize;
                buffers.Offsets[i] = ReadNumber(propertyIntPtr, _currentOffsetSize);
                buffers.Properties[i] = i;
            }
            Array.Sort(buffers.Offsets, buffers.Properties, 0, _propCount, NumericDescendingComparer.Instance);
            return _propCount;
        }

        public int[] GetPropertiesByInsertionOrder()
        {
            //TODO: Move all callers to use the other overload
            var props = new int[_propCount];
            var offsets = new int[_propCount];
            var metadataSize = _currentOffsetSize + _currentPropertyIdSize + sizeof(byte);
            for (int i = 0; i < props.Length; i++)
            {
                var propertyIntPtr = _metadataPtr + i * metadataSize;
                offsets[i] = ReadNumber(propertyIntPtr, _currentOffsetSize);
                props[i] = i;
            }
            Array.Sort(offsets, props, NumericDescendingComparer.Instance);
            return props;
        }


        internal object GetObject(BlittableJsonToken type, int position)
        {
            if (_mem == null)
                ThrowObjectDisposed();
            switch (type & TypesMask)
            {
                case BlittableJsonToken.EmbeddedBlittable:
                    return ReadNestedObject(position);
                case BlittableJsonToken.StartObject:
                    return new BlittableJsonReaderObject(position, _parent ?? this, type);
                case BlittableJsonToken.StartArray:
                    return new BlittableJsonReaderArray(position, _parent ?? this, type);
                case BlittableJsonToken.Integer:
                    return ReadVariableSizeLong(position);
                case BlittableJsonToken.String:
                    return ReadStringLazily(position);
                case BlittableJsonToken.CompressedString:
                    return ReadCompressStringLazily(position);
                case BlittableJsonToken.Boolean:
                    return ReadNumber(_mem + position, 1) == 1;
                case BlittableJsonToken.Null:
                    return null;
                case BlittableJsonToken.Float:
                    return new LazyDoubleValue(ReadStringLazily(position));
                default:
                    throw new ArgumentOutOfRangeException((type).ToString());
            }
        }

        public void Dispose()
        {
            _mem = null;
            _metadataPtr = null;
            _objStart = null;
            if (_objectsPathCache != null)
            {
                foreach (var property in _objectsPathCache)
                {
                    var disposable = property.Value as IDisposable;
                    disposable?.Dispose();
                }
            }

            _buffer.Dispose();
            if (DisposeTrackingReference != null)
                _context.ReaderDisposed(DisposeTrackingReference);
        }

        public void CopyTo(byte* ptr)
        {
            Memory.Copy(ptr, _mem, _size);
        }

        public void BlittableValidation()
        {
            byte offset;
            var currentSize = Size - 1;
            int rootPropOffsetSize;
            int rootPropIdSize;

            if (currentSize < 1)
                throw new InvalidDataException("Illegal data");
            var rootToken = TokenValidation(*(_mem + currentSize), out rootPropOffsetSize, out rootPropIdSize);
            if (rootToken != BlittableJsonToken.StartObject)
                throw new InvalidDataException("Illegal root object");
            currentSize--;

            var propsOffsetList = ReadVariableSizeIntInReverse(_mem, currentSize, out offset);
            if (offset > currentSize)
                throw new InvalidDataException("Properties names offset not valid");
            currentSize -= offset;

            var rootMetadataOffset = ReadVariableSizeIntInReverse(_mem, currentSize, out offset);
            if (offset > currentSize)
                throw new InvalidDataException("Root metadata offset not valid");
            currentSize -= offset;

            if ((propsOffsetList > currentSize) || (propsOffsetList <= 0))
                throw new InvalidDataException("Properties names offset not valid");

            int propNamesOffsetSize;
            var token = (BlittableJsonToken)(*(_mem + propsOffsetList));
            propNamesOffsetSize = ProcessTokenOffsetFlags(token);

            if (((token & (BlittableJsonToken)0xC0) != 0) || ((TypesMask & token) != 0x00))
                throw new InvalidDataException("Properties names token not valid");

            var numberOfProps = (currentSize - propsOffsetList) / propNamesOffsetSize;
            currentSize = PropertiesNamesValidation(numberOfProps, propsOffsetList,
                propNamesOffsetSize, propsOffsetList);

            if ((rootMetadataOffset > currentSize) || (rootMetadataOffset < 0))
                throw new InvalidDataException("Root metadata offset not valid");
            var current = PropertiesValidation(rootToken, rootPropOffsetSize, rootPropIdSize,
                rootMetadataOffset, numberOfProps);

            if (current != currentSize)
                throw new InvalidDataException("Root metadata not valid");
        }

        private int PropertiesNamesValidation(int numberOfProps, int propsOffsetList, int propsNamesOffsetSize,
            int currentSize)
        {
            var blittableSize = currentSize;
            var offsetCounter = 0;
            for (var i = numberOfProps - 1; i >= 0; i--)
            {
                int stringLength;
                var nameOffset = 0;
                nameOffset = ReadNumber((_mem + propsOffsetList + 1 + i * propsNamesOffsetSize),
                    propsNamesOffsetSize);
                if ((blittableSize < nameOffset ) || (nameOffset < 0))
                    throw new InvalidDataException("Properties names offset not valid");
                stringLength = StringValidation(propsOffsetList - nameOffset);
                if (offsetCounter + stringLength != nameOffset)
                    throw new InvalidDataException("Properties names offset not valid");
                offsetCounter = nameOffset;
                currentSize -= stringLength;
            }
            return currentSize;
        }

        private int StringValidation(int stringOffset)
        {
            byte lenOffset;
            byte escOffset;
            int stringLength;
            stringLength = ReadVariableSizeInt(stringOffset, out lenOffset);
            if (stringLength < 0)
                throw new InvalidDataException("String not valid");
            var str = stringOffset + lenOffset;
            var totalEscCharLen = 0;
            var escCount = ReadVariableSizeInt(stringOffset + lenOffset + stringLength, out escOffset);
            if (escCount != 0)
            {
                var prevEscCharOffset = 0;
                for (var i = 0; i < escCount; i++)
                {
                    byte escCharOffsetLen;
                    var escCharOffset = ReadVariableSizeInt(str + stringLength + escOffset + totalEscCharLen, out escCharOffsetLen);
                    escCharOffset += prevEscCharOffset;
                    var escChar = (char)ReadNumber(_mem + str + escCharOffset, 1);
                    switch (escChar)
                    {
                        case '\\':
                        case '/':
                        case '"':
                        case '\b':
                        case '\f':
                        case '\n':
                        case '\r':
                        case '\t':
                            break;
                        default:
                            throw new InvalidDataException("String not valid, invalid escape character: " + escChar);
                    }
                    totalEscCharLen += escCharOffsetLen;
                    prevEscCharOffset = escCharOffset + 1;
                }
            }
            return stringLength + escOffset + totalEscCharLen + lenOffset;
        }

        private BlittableJsonToken TokenValidation(byte tokenStart, out int propOffsetSize,
            out int propIdSize)
        {
            var token = (BlittableJsonToken)tokenStart;
            var tokenType = ProcessTokenTypeFlags(token);
            propOffsetSize = ((tokenType == BlittableJsonToken.StartObject) ||
                              (tokenType == BlittableJsonToken.StartArray))
                ? ProcessTokenOffsetFlags(token)
                : 0;

            propIdSize = (tokenType == BlittableJsonToken.StartObject)
                ? ProcessTokenPropertyFlags(token)
                : 0;
            return tokenType;
        }

        private int PropertiesValidation(BlittableJsonToken rootTokenTypen, int mainPropOffsetSize, int mainPropIdSize,
            int objStartOffset, int numberOfPropsNames)
        {
            byte offset;
            var numberOfProperties = ReadVariableSizeInt(_mem + objStartOffset, 0, out offset);
            var current = objStartOffset + offset;

            if (numberOfProperties < 0)
                throw new InvalidDataException("Number of properties not valid");

            for (var i = 1; i <= numberOfProperties; i++)
            {
                var propOffset = ReadNumber(_mem + current, mainPropOffsetSize);
                if ((propOffset > objStartOffset) || (propOffset < 0))
                    throw new InvalidDataException("Properties offset not valid");
                current += mainPropOffsetSize;

                if (rootTokenTypen == BlittableJsonToken.StartObject)
                {
                    var id = ReadNumber(_mem + current, mainPropIdSize);
                    if ((id > numberOfPropsNames) || (id < 0))
                        throw new InvalidDataException("Properties id not valid");
                    current += mainPropIdSize;
                }

                int propOffsetSize;
                int propIdSize;
                var tokenType = TokenValidation(*(_mem + current), out propOffsetSize, out propIdSize);
                current++;

                var propValueOffset = objStartOffset - propOffset;

                switch (tokenType)
                {
                    case BlittableJsonToken.StartObject:
                        PropertiesValidation(tokenType, propOffsetSize, propIdSize, propValueOffset, numberOfPropsNames);
                        break;
                    case BlittableJsonToken.StartArray:
                        PropertiesValidation(tokenType, propOffsetSize, propIdSize, propValueOffset, numberOfPropsNames);
                        break;
                    case BlittableJsonToken.Integer:
                        ReadVariableSizeLong(propValueOffset);
                        break;
                    case BlittableJsonToken.Float:
                        var floatLen = ReadNumber(_mem + objStartOffset - propOffset, 1);
                        var floatStringBuffer = new string(' ', floatLen);
                        fixed (char* pChars = floatStringBuffer)
                        {
                            for (int j = 0; j < floatLen; j++)
                            {
                                pChars[j] = (char)ReadNumber((_mem + objStartOffset - propOffset + 1 + j), sizeof(byte));
                            }
                        }
                        double _double;
                        var result = double.TryParse(floatStringBuffer,NumberStyles.Float,CultureInfo.InvariantCulture, out _double);
                        if (!(result))
                            throw new InvalidDataException("Double not valid (" + floatStringBuffer + ")");
                        break;
                    case BlittableJsonToken.String:
                        StringValidation(propValueOffset);
                        break;
                    case BlittableJsonToken.CompressedString:
                        var stringLength = ReadVariableSizeInt(propValueOffset, out offset);
                        var compressedStringLength = ReadVariableSizeInt(propValueOffset + offset, out offset);
                        if ((compressedStringLength > stringLength) ||
                            (compressedStringLength < 0) ||
                            (stringLength < 0))
                            throw new InvalidDataException("Compressed string not valid");
                        break;
                    case BlittableJsonToken.Boolean:
                        var boolProp = ReadNumber(_mem + propValueOffset, 1);
                        if ((boolProp != 0) && (boolProp != 1))
                            throw new InvalidDataException("Bool not valid");
                        break;
                    case BlittableJsonToken.Null:
                        if (ReadNumber(_mem + propValueOffset, 1) != 0)
                            throw new InvalidDataException("Null not valid");
                        break;
                    default:
                        throw new InvalidDataException("Token type not valid");
                }
            }
            return current;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            var blittableJson = obj as BlittableJsonReaderObject;

            if (blittableJson != null)
                return Equals(blittableJson);

            return false;
        }

        protected bool Equals(BlittableJsonReaderObject other)
        {
            if (_size != other.Size)
                return false;

            if (_propCount != other._propCount)
                return false;

            foreach (var propertyName in GetPropertyNames())
            {
                object result;
                if (other.TryGetMember(propertyName, out result) == false)
                    return false;

                var current = this[propertyName];

                if (current == null && result == null)
                    continue;

                if ((current?.Equals(result) ?? false) == false)
                    return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            return _size ^ _propCount;
        }
    }
}