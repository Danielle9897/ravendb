﻿namespace Sparrow.Json
{
    public class JsonContextPool : JsonContextPoolBase<JsonOperationContext>
    {
        protected override JsonOperationContext CreateContext()
        {
            return new JsonOperationContext(1024*1024, 16*1024);
        }
    }
}