﻿using System;
using Raven.Database.Util;
using Raven.Server.Config;
using Raven.Server.Utils;

namespace Raven.Server.ServerWide
{
    public interface IResourceStore : IDisposable
    {
        string Name { get; }

        string ResourceName { get; }

        RavenConfiguration Configuration { get; }

        MetricsCountersManager Metrics { get; }
    }
}