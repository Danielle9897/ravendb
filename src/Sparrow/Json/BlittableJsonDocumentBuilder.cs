﻿using System;
using System.Collections.Generic;
using System.IO;
using Sparrow.Json.Parsing;

namespace Sparrow.Json
{
    public class BlittableJsonDocumentBuilder : IDisposable
    {
        protected readonly Stack<BuildingState> _continuationState = new Stack<BuildingState>();

        protected readonly JsonOperationContext _context;
        private UsageMode _mode;
        private  IJsonParser _reader;
        private readonly BlittableWriter<UnmanagedWriteBuffer> _writer;
        private readonly JsonParserState _state;
        
        protected WriteToken _writeToken;
        private  string _debugTag;
        

        public BlittableJsonDocumentBuilder(JsonOperationContext context, JsonParserState state, IJsonParser reader, BlittableWriter<UnmanagedWriteBuffer> writer =null)
        {
            _context = context;
            _state = state;
            _reader = reader;
            _writer = writer ?? new BlittableWriter<UnmanagedWriteBuffer>(context);
        }

        public BlittableJsonDocumentBuilder(JsonOperationContext context, UsageMode mode, string debugTag, IJsonParser reader, JsonParserState state, BlittableWriter<UnmanagedWriteBuffer> writer = null) : this(context, state, reader, writer)
        {
            Renew(debugTag, mode);
        }

        public BlittableJsonDocumentBuilder(JsonOperationContext context, JsonParserState state, UsageMode mode,  string debugTag, IJsonParser reader, BlittableWriter<UnmanagedWriteBuffer> writer = null):this(context,state,reader,writer)
        {
            Renew(debugTag, mode);
        }

        public void Reset()
        {
            _debugTag = null;
            _mode = UsageMode.None;
            _continuationState.Clear();
            _writeToken = default(WriteToken);
            _writer.Reset();
        }

        public void Renew(string debugTag, UsageMode mode)
        {
            Reset();
            _debugTag = debugTag;
            _mode = mode;
            _writer.Renew();
        }

        public virtual void ReadArrayDocument()
        {
            _continuationState.Push(new BuildingState
            {
                State = ContinuationState.ReadArrayDocument
            });
        }

        public virtual void ReadObjectDocument()
        {
            _continuationState.Push(new BuildingState
            {
                State = ContinuationState.ReadObjectDocument
            });
        }

        public virtual void ReadNestedObject()
        {
            _continuationState.Push(new BuildingState
            {
                State = ContinuationState.ReadObject
            });
        }

        public int SizeInBytes => _writer.SizeInBytes;
        

        public void Dispose()
        {
            _writer.Dispose();
        }

        public virtual bool Read()
        {
            if (_continuationState.Count == 0)
                return false; //nothing to do

            var currentState = _continuationState.Pop();
            while (true)
            {
                switch (currentState.State)
                {
                    case ContinuationState.ReadObjectDocument:
                        if (_reader.Read() == false)
                        {
                            _continuationState.Push(currentState);
                            return false;
                        }
                        currentState.State = ContinuationState.ReadObject;
                        continue;
                    case ContinuationState.ReadArrayDocument:
                        if (_reader.Read() == false)
                        {
                            _continuationState.Push(currentState);
                            return false;
                        }

                        var fakeFieldName = _context.GetLazyStringForFieldWithCaching("_");
                        var propIndex = _context.CachedProperties.GetPropertyId(fakeFieldName);
                        currentState.CurrentPropertyId = propIndex;
                        currentState.MaxPropertyId = propIndex;
                        currentState.FirstWrite = _writer.Position;
                        currentState.Properties = new List<PropertyTag>
                        {
                            new PropertyTag
                            {
                                PropertyId = propIndex
                            }
                        };
                        currentState.State = ContinuationState.CompleteDocumentArray;
                        _continuationState.Push(currentState);
                        currentState = new BuildingState
                        {
                            State = ContinuationState.ReadArray
                        };
                        continue;
                    case ContinuationState.CompleteDocumentArray:
                        currentState.Properties[0].Type = (byte)_writeToken.WrittenToken;
                        currentState.Properties[0].Position = _writeToken.ValuePos;

                        // Register property position, name id (PropertyId) and type (object type and metadata)
                        _writeToken = _writer.WriteObjectMetadata(currentState.Properties, currentState.FirstWrite, currentState.MaxPropertyId);

                        return true;
                    case ContinuationState.ReadObject:
                        if (_state.CurrentTokenType != JsonParserToken.StartObject)
                            throw new InvalidDataException("Expected start of object, but got " + _state.CurrentTokenType);
                        currentState.State = ContinuationState.ReadPropertyName;
                        currentState.Properties = new List<PropertyTag>();
                        currentState.FirstWrite = _writer.Position;
                        continue;
                    case ContinuationState.ReadArray:
                        if (_state.CurrentTokenType != JsonParserToken.StartArray)
                            throw new InvalidDataException("Expected start of array, but got " + _state.CurrentTokenType);
                        currentState.Types = new List<BlittableJsonToken>();
                        currentState.Positions = new List<int>();
                        currentState.State = ContinuationState.ReadArrayValue;
                        continue;
                    case ContinuationState.ReadArrayValue:
                        if (_reader.Read() == false)
                        {
                            _continuationState.Push(currentState);
                            return false;
                        }
                        if (_state.CurrentTokenType == JsonParserToken.EndArray)
                        {
                            currentState.State = ContinuationState.CompleteArray;
                            continue;
                        }
                        currentState.State = ContinuationState.CompleteArrayValue;
                        _continuationState.Push(currentState);
                        currentState = new BuildingState
                        {
                            State = ContinuationState.ReadValue
                        };
                        continue;
                    case ContinuationState.CompleteArrayValue:
                        currentState.Types.Add(_writeToken.WrittenToken);
                        currentState.Positions.Add(_writeToken.ValuePos);
                        currentState.State = ContinuationState.ReadArrayValue;
                        continue;
                    case ContinuationState.CompleteArray:
                        
                        var arrayToken = BlittableJsonToken.StartArray;
                        var arrayInfoStart = _writer.WriteArrayMetadata(currentState.Positions, currentState.Types,
                            ref arrayToken);

                        _writeToken = new WriteToken
                        {
                            ValuePos = arrayInfoStart,
                            WrittenToken = arrayToken
                        };
                        
                        currentState = _continuationState.Pop();
                        continue;
                    case ContinuationState.ReadPropertyName:
                        if (_reader.Read() == false)
                        {
                            _continuationState.Push(currentState);
                            return false;
                        }

                        if (_state.CurrentTokenType == JsonParserToken.EndObject)
                        {
                            _writeToken = _writer.WriteObjectMetadata(currentState.Properties, currentState.FirstWrite,
                                currentState.MaxPropertyId);
                            if (_continuationState.Count == 0)
                                return true;
                            currentState = _continuationState.Pop();
                            continue;
                        }

                        if (_state.CurrentTokenType != JsonParserToken.String)
                            throw new InvalidDataException("Expected property, but got " + _state.CurrentTokenType);


                        var property = CreateLazyStringValueFromParserState();

                        currentState.CurrentPropertyId = _context.CachedProperties.GetPropertyId(property);
                        currentState.MaxPropertyId = Math.Max(currentState.MaxPropertyId, currentState.CurrentPropertyId);
                        currentState.State = ContinuationState.ReadPropertyValue;
                        continue;
                    case ContinuationState.ReadPropertyValue:
                        if (_reader.Read() == false)
                        {
                            _continuationState.Push(currentState);
                            return false;
                        }
                        currentState.State = ContinuationState.CompleteReadingPropertyValue;
                        _continuationState.Push(currentState);
                        currentState = new BuildingState
                        {
                            State = ContinuationState.ReadValue
                        };
                        continue;
                    case ContinuationState.CompleteReadingPropertyValue:
                        // Register property position, name id (PropertyId) and type (object type and metadata)
                        currentState.Properties.Add(new PropertyTag
                        {
                            Position = _writeToken.ValuePos,
                            Type = (byte)_writeToken.WrittenToken,
                            PropertyId = currentState.CurrentPropertyId
                        });
                        currentState.State = ContinuationState.ReadPropertyName;
                        continue;
                    case ContinuationState.ReadValue:
                        ReadJsonValue();
                        currentState = _continuationState.Pop();
                        break;
                }
            }
        }

        private unsafe void ReadJsonValue()
        {
            int start;
            switch (_state.CurrentTokenType)
            {
                case JsonParserToken.StartObject:
                    _continuationState.Push(new BuildingState
                    {
                        State = ContinuationState.ReadObject
                    });
                    return;
                case JsonParserToken.StartArray:
                    _continuationState.Push(new BuildingState
                    {
                        State = ContinuationState.ReadArray
                    });
                    return;
                case JsonParserToken.Integer:
                    start = _writer.WriteValue(_state.Long);
                    _writeToken = new WriteToken
                    {
                        ValuePos = start,
                        WrittenToken = BlittableJsonToken.Integer
                    };
                    return;
                case JsonParserToken.Float:
                    if ((_mode & UsageMode.ValidateDouble) == UsageMode.ValidateDouble)
                        _reader.ValidateFloat();
                    BlittableJsonToken ignored;

                    start = _writer.WriteValue(_state.StringBuffer, _state.StringSize, out ignored, _mode, _state.CompressedSize);
                    _state.CompressedSize = null;
                    _writeToken = new WriteToken
                    {
                        ValuePos = start,
                        WrittenToken = BlittableJsonToken.Float
                    };
                    return;
                case JsonParserToken.String:
                    BlittableJsonToken stringToken;
                    start = _writer.WriteValue(_state.StringBuffer, _state.StringSize, _state.EscapePositions, out stringToken, _mode, _state.CompressedSize);
                    _state.CompressedSize = null;
                    _writeToken = new WriteToken
                    {
                        ValuePos = start,
                        WrittenToken = stringToken
                    };
                    return;
                case JsonParserToken.True:
                case JsonParserToken.False:
                    start = _writer.WriteValue(_state.CurrentTokenType == JsonParserToken.True ? (byte)1 : (byte)0);
                    _writeToken = new WriteToken
                    {
                        ValuePos = start,
                        WrittenToken = BlittableJsonToken.Boolean
                    };
                    return;
                case JsonParserToken.Null:
                    start = _writer.WriteValue((byte) 0);
                    _writeToken = new WriteToken // nothing to do here, we handle that with the token
                    {
                        WrittenToken = BlittableJsonToken.Null,
                        ValuePos = start
                    };
                    return;
                default:
                    throw new InvalidDataException("Expected a value, but got " + _state.CurrentTokenType);
            }
        }


        public enum ContinuationState
        {
            ReadPropertyName,
            ReadPropertyValue,
            ReadArray,
            ReadArrayValue,
            ReadObject,
            ReadValue,
            CompleteReadingPropertyValue,
            ReadObjectDocument,
            ReadArrayDocument,
            CompleteDocumentArray,
            CompleteArray,
            CompleteArrayValue
        }

        public struct BuildingState
        {
            public ContinuationState State;
            public List<PropertyTag> Properties;
            public int CurrentPropertyId;
            public int MaxPropertyId;
            public List<BlittableJsonToken> Types;
            public List<int> Positions;
            public long FirstWrite;
        }


        public class PropertyTag
        {
            public int Position;
            public int PropertyId;
            public byte Type;
        }
        [Flags]
        public enum UsageMode
        {
            None = 0,
            ValidateDouble = 1,
            CompressStrings = 2,
            CompressSmallStrings = 4,
            ToDisk = ValidateDouble | CompressStrings
        }

        public struct WriteToken
        {
            public int ValuePos;
            public BlittableJsonToken WrittenToken;
        }


        private unsafe LazyStringValue CreateLazyStringValueFromParserState()
        {
            var lazyStringValueFromParserState = new LazyStringValue(null, _state.StringBuffer, _state.StringSize, _context);

            if (_state.EscapePositions.Count > 0)
            {
                lazyStringValueFromParserState.EscapePositions = _state.EscapePositions.ToArray();
            }
            return lazyStringValueFromParserState;
        }

        protected static int SetOffsetSizeFlag(ref BlittableJsonToken objectToken, long distanceFromFirstProperty)
        {
            int positionSize;
            if (distanceFromFirstProperty <= byte.MaxValue)
            {
                positionSize = sizeof(byte);
                objectToken |= BlittableJsonToken.OffsetSizeByte;
            }
            else
            {
                if (distanceFromFirstProperty <= ushort.MaxValue)
                {
                    positionSize = sizeof(short);
                    objectToken |= BlittableJsonToken.OffsetSizeShort;
                }
                else
                {
                    positionSize = sizeof(int);
                    objectToken |= BlittableJsonToken.OffsetSizeInt;
                }
            }
            return positionSize;
        }

        public virtual void FinalizeDocument()
        {
            var documentToken = _writeToken.WrittenToken;
            var rootOffset = _writeToken.ValuePos;

            _writer.WriteDocumentMetadata(rootOffset, documentToken);
        }

        public BlittableJsonReaderObject CreateReader()
        {
            return _writer.CreateReader();
        }

        public BlittableJsonReaderArray CreateArrayReader()
        {
            var reader = CreateReader();
            BlittableJsonReaderArray array;
            if (reader.TryGet("_", out array))
                return array;
            throw new InvalidOperationException("Couldn't find array");
        }

        public override string ToString()
        {
            return "Building json for " + _debugTag;
        }
    }
}