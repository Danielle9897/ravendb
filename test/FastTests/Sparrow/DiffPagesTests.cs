﻿using System;
using Sparrow;
using Sparrow.Utils;
using Xunit;

namespace FastTests.Sparrow
{
    public unsafe class DiffPagesTests : NoDisposalNeeded
    {
        [Fact]
        public void CanComputeNoDifference()
        {

            var fst = new byte[4096];
            var sec = new byte[4096];

            new Random().NextBytes(fst);
            Buffer.BlockCopy(fst, 0, sec, 0, fst.Length);

            fixed (byte* one = fst)
            fixed (byte* two = sec)
            fixed (byte* tmp = new byte[4096])
            {
                var diffPages = new DiffPages
                {
                    Output = tmp,
                };

                diffPages.ComputeDiff(one, two, 4096);

                Assert.Equal(0, diffPages.OutputSize);
            }
        }

        [Fact]
        public void CanComputeSmallDifference()
        {
            var fst = new byte[4096];
            var sec = new byte[4096];

            new Random().NextBytes(fst);
            Buffer.BlockCopy(fst, 0, sec, 0, fst.Length);

            sec[12] ++;
            sec[433]++;

            fixed (byte* one = fst)
            fixed (byte* two = sec)
            fixed (byte* tmp = new byte[4096])
            {
                var diffPages = new DiffPages
                {
                    Output = tmp,
                };

                diffPages.ComputeDiff(one, two, 4096);

                Assert.Equal(96, diffPages.OutputSize);
            }
        }

        [Fact]
        public void CanComputeSmallDifferenceFromNew()
        {
            var fst = new byte[4096];


            fst[12]++;
            fst[433]++;

            fixed (byte* one = fst)
            fixed (byte* tmp = new byte[4096])
            {
                var diffPages = new DiffPages
                {
                    Output = tmp,
                };

                diffPages.ComputeNew(one, 4096);

                Assert.Equal(96, diffPages.OutputSize);
            }
        }

        [Fact]
        public void CanComputeSmallDifference_AndThenApplyit()
        {
            var fst = new byte[4096];
            var sec = new byte[4096];
            var trd = new byte[4096];

            new Random().NextBytes(fst);
            Buffer.BlockCopy(fst, 0, sec, 0, fst.Length);

            sec[12]++;
            sec[433]++;

            fixed (byte* one = fst)
            fixed (byte* two = sec)
            fixed (byte* tri = trd)
            fixed (byte* tmp = new byte[4096])
            {
                var diffPages = new DiffPages
                {
                    Output = tmp,
                };

                diffPages.ComputeDiff(one, two, 4096);

                Memory.Copy(tri, one, 4096);
                new DiffApplier
                {
                    Destination = tri,
                    Diff = tmp,
                    Size = 4096,
                    DiffSize = diffPages.OutputSize
                }.Apply();

                Assert.Equal(0, Memory.Compare(tri, two, 4096));
            }
        }

        [Fact]
        public void CompletelyDifferent()
        {
            var fst = new byte[4096];
            var sec = new byte[4096];

            new Random(1).NextBytes(fst);
            new Random(2).NextBytes(sec);

            fixed (byte* one = fst)
            fixed (byte* two = sec)
            fixed (byte* tmp = new byte[4096])
            {
                var diffPages = new DiffPages
                {
                    Output = tmp,
                };

                diffPages.ComputeDiff(one, two, 4096);

                Assert.False(diffPages.IsDiff);
            }
        }
    }
}