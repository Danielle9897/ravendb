﻿using System;

namespace Raven.NewClient.Client
{
    public class BatchOptions
    {
        public bool WaitForReplicas { get; set; }
        public int NumberOfReplicasToWaitFor { get; set; }
        public TimeSpan WaitForReplicasTimeout { get; set; }
        public bool Majority { get; set; }
        public bool ThrowOnTimeoutInWaitForReplicas { get; set; }

        public bool WaitForIndexes { get; set; }
        public TimeSpan WaitForIndexesTimeout { get; set; }
        public bool ThrowOnTimeoutInWaitForIndexes { get; set; }
        public string[] WaitForSpecificIndexes { get; set; }
    }
}
