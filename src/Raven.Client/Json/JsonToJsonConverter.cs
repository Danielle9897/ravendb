using System;
using System.Reflection;
using  Raven.Imports.Newtonsoft.Json;
using  Raven.Imports.Newtonsoft.Json.Linq;
using Raven.Abstractions.Linq;
using Raven.Client.Linq;
using  Raven.Imports.Newtonsoft.Json.Utilities;
using Raven.Json.Linq;

namespace Raven.Abstractions.Json
{
    public class JsonToJsonConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is RavenJToken)
                ((RavenJToken)value).WriteTo(writer);
            else if(value is DynamicNullObject)
                writer.WriteNull();
            else
                ((IDynamicJsonObject)value).WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // NOTE: THIS DOESN'T SUPPORT READING OF DynamicJsonObject !!!

            var o = RavenJToken.Load(reader);
            return (o.Type == JTokenType.Null || o.Type == JTokenType.Undefined) ? null : o;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof (RavenJToken) ||
                   objectType == typeof (DynamicJsonObject) ||
                   objectType == typeof (DynamicNullObject) ||
                   objectType.GetTypeInfo().IsSubclassOf(typeof (RavenJToken)) ||
                   objectType.GetTypeInfo().IsSubclassOf(typeof (DynamicJsonObject));
        }
    }
}
