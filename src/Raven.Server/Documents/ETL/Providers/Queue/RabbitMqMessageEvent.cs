﻿using System.Collections.Generic;
using Amqp;

namespace Raven.Server.Documents.ETL.Providers.Queue;

public class RabbitMqMessageEvent
{
    public string Queue { get; set; }
    
    public List<RabbitMqMessage> Messages { get; set; } = new();
}

public class RabbitMqMessage
{
    public string ExchangeKey { get; set; }

    public Message Message { get; set; }
}
