// -----------------------------------------------------------------------
//  <copyright file="InitialSize.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System.IO;
using Xunit;
using Voron;
using Voron.Global;

namespace FastTests.Voron.Storage
{
    public class InitialSize : StorageTest
    {
        public int GetExpectedInitialSize()
        {
            return 64 * 1024;
        }

        [Fact]
        public void WhenInitialFileSizeIsNotSetTheFileSizeForDataFileAndScratchFileShouldBeSetToSystemAllocationGranularity()
        {


            var options = StorageEnvironmentOptions.ForPath(DataDir);
            options.InitialFileSize = null;

            using (new StorageEnvironment(options))
            {
                var dataFile = Path.Combine(DataDir, Constants.DatabaseFilename);
                var scratchFile = Path.Combine(DataDir, StorageEnvironmentOptions.ScratchBufferName(0));

                Assert.Equal(GetExpectedInitialSize(), new FileInfo(dataFile).Length);
                Assert.Equal(GetExpectedInitialSize(), new FileInfo(scratchFile).Length);
            }
        }

        [Fact]
        public void WhenInitialFileSizeIsSetTheFileSizeForDataFileAndScratchFileShouldBeSetAccordingly()
        {
            var options = StorageEnvironmentOptions.ForPath(DataDir);
            options.InitialFileSize = GetExpectedInitialSize() * 2;

            using (new StorageEnvironment(options))
            {
                var dataFile = Path.Combine(DataDir, Constants.DatabaseFilename);
                var scratchFile = Path.Combine(DataDir, StorageEnvironmentOptions.ScratchBufferName(0));

                Assert.Equal(0, new FileInfo(dataFile).Length % GetExpectedInitialSize());
                Assert.Equal(0, new FileInfo(scratchFile).Length % GetExpectedInitialSize());
            }
        }

        [Fact]
        public void WhenInitialFileSizeIsSetTheFileSizeForDataFileAndScratchFileShouldBeSetAccordinglyAndItWillBeRoundedToTheNearestGranularity()
        {
            var options = StorageEnvironmentOptions.ForPath(DataDir);
            options.InitialFileSize = GetExpectedInitialSize() * 2 + 1;

            using (new StorageEnvironment(options))
            {
                var dataFile = Path.Combine(DataDir, Constants.DatabaseFilename);
                var scratchFile = Path.Combine(DataDir, StorageEnvironmentOptions.ScratchBufferName(0));

                if (StorageEnvironmentOptions.RunningOnPosix)
                {
                    // on Linux, we use 4K as the allocation granularity
                    Assert.Equal(0, new FileInfo(dataFile).Length % 4096);
                    Assert.Equal(0, new FileInfo(scratchFile).Length % 4096);
                }
                else
                {
                    // on Windows, we use 64K as the allocation granularity
                    Assert.Equal(0, new FileInfo(dataFile).Length % GetExpectedInitialSize());
                    Assert.Equal(0, new FileInfo(scratchFile).Length % GetExpectedInitialSize());
                }
            }
        }

        public override void Dispose()
        {
            if (!string.IsNullOrEmpty(DataDir) && Directory.Exists(DataDir))
                Directory.Delete(DataDir, true);

            base.Dispose();
        }
    }
}
