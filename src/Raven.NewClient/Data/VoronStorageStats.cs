// -----------------------------------------------------------------------
//  <copyright file="VoronStorageStats.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;

namespace Raven.NewClient.Abstractions.Data
{
    public class VoronStorageStats
    {
        public long FreePagesOverhead;
        public long RootPages;
        public long UnallocatedPagesAtEndOfFile;
        public long UsedDataFileSizeInBytes;
        public long AllocatedDataFileSizeInBytes;
        public long NextWriteTransactionId;
        public List<VoronActiveTransaction> ActiveTransactions;
    }
}
