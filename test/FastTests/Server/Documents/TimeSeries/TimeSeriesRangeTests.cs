﻿using System;
using System.Globalization;
using Raven.Server.Documents.Queries.AST;
using Raven.Server.Documents.TimeSeries;
using Sparrow;
using Sparrow.Server;
using Sparrow.Threading;
using Xunit;
using Xunit.Abstractions;

namespace FastTests.Server.Documents.TimeSeries
{
    public unsafe class TimeSeriesRangeTests : NoDisposalNeeded
    {
        public TimeSeriesRangeTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [InlineData("1s", "2019-05-05T06:03:51.1077101Z", "2019-05-05T06:03:51.0000000Z", "2019-05-05T06:03:52.0000000Z")]
        [InlineData("1m", "2019-05-05T06:03:51.1077101Z", "2019-05-05T06:03:00.0000000Z", "2019-05-05T06:04:00.0000000Z")]
        [InlineData("1h", "2019-05-05T06:03:51.1077101Z", "2019-05-05T06:00:00.0000000Z", "2019-05-05T07:00:00.0000000Z")]
        [InlineData("1d", "2019-05-05T06:03:51.1077101Z", "2019-05-05T00:00:00.0000000Z", "2019-05-06T00:00:00.0000000Z")]
        [InlineData("1 month", "2019-05-05T06:03:51.1077101Z", "2019-05-01T00:00:00.0000000Z", "2019-06-01T00:00:00.0000000Z")]
        public void CanGetRangeStartAndNext(string rangeStr, string dateStr, string startStr, string nextStr)
        {
            var rangeSpec = RangeGroup.ParseRangeFromString(rangeStr);
            var date = DateTime.ParseExact(dateStr, "o", CultureInfo.InvariantCulture).ToUniversalTime();

            rangeSpec.InitializeRange(date);

            Assert.Equal(
                DateTime.ParseExact(startStr, "o", CultureInfo.InvariantCulture).ToUniversalTime(),
                rangeSpec.Start);

            rangeSpec.MoveToNextRange(rangeSpec.End);

            Assert.Equal(
                DateTime.ParseExact(nextStr, "o", CultureInfo.InvariantCulture).ToUniversalTime(),
                rangeSpec.Start);

        }

        [Fact]
        public unsafe void BitBufferCompression()
        {
            using (var allocator = new ByteStringContext(SharedMultipleUseFlag.None))
            using (allocator.Allocate(2048, out var buffer))
            {
                Memory.Set(buffer.Ptr, 0, buffer.Length);
                var bitBuffer = new BitsBuffer(buffer.Ptr, buffer.Length);

                for (int i = 0; i < 12; i++)
                {
                    bitBuffer.AddValue((ulong)(i & 1), 1);
                }

                bitBuffer.TryCompressBuffer(allocator, 0);


                bitBuffer.Uncompress(allocator, out var newBuffer);

                for (int i = 0; i < 12; i++)
                {
                    int copy = i;
                    var v = newBuffer.ReadValue(ref copy, 1);
                    Assert.Equal((ulong)(i & 1), v);
                }
            }
        }

        [Fact]
        public void BitBufferCanHandleLargeValuesAndCompression()
        {
            var values = GetFailingValues();

            using (var allocator = new ByteStringContext(SharedMultipleUseFlag.None))
            using (allocator.Allocate(2048, out var buffer))
            {
                Memory.Set(buffer.Ptr, 0, buffer.Length);
                var bitBuffer = new BitsBuffer(buffer.Ptr, buffer.Length);

                var numberOfValues = values.Length;


                for (int i = 0; i < numberOfValues; i++)
                {

                    bitBuffer.EnsureAdditionalBits(allocator, values[i].Item2);
                    bitBuffer.AddValue(values[i].Item1, values[i].Item2);


                }

                using (bitBuffer.Uncompress(allocator, out var newBuf))
                {
                    int offset = 0;
                    for (int i = 0; i < numberOfValues; i++)
                    {
                        var val = newBuf.ReadValue(ref offset, values[i].Item2);
                        if (val != values[i].Item1)
                        {
                            Assert.Fail("Unmatch value at index: " + i);
                        }
                    }
                }
            }
        }

        private static (ulong, int)[] GetFailingValues()
        {
            return new (ulong, int)[] {(6978797568, 64),
                (512, 43),
                (17870299427499917312, 64),
                (0, 39),
                (690950699080482816, 61),
                (1364238843372371968, 62),
                (879609302220800, 51),
                (949978046398464, 51),
                (879609302220800, 51),
                (2693117392795467776, 63),
                (1706442046308352, 52),
                (1741626418397184, 52),
                (1706442046308352, 52),
                (1811995162574848, 52),
                (1706442046308352, 52),
                (1741626418397184, 52),
                (1706442046308352, 52),
                (1952732650930176, 52),
                (1697645953286144, 52),
                (1715238139330560, 52),
                (1697645953286144, 52),
                (1750422511419392, 52),
                (1697645953286144, 52),
                (1715238139330560, 52),
                (1697645953286144, 52),
                (1820791255597056, 52),
                (1697645953286144, 52),
                (1715238139330560, 52),
                (1697645953286144, 52),
                (1750422511419392, 52),
                (1697645953286144, 52),
                (1715238139330560, 52),
                (1697645953286144, 52),
                (5315364664111005696, 64),
                (90819660454297600, 58),
                (179431501560020992, 59),
                (114349209288704, 48),
                (354438568329871360, 60),
                (219902325555200, 49),
                (237494511599616, 49),
                (219902325555200, 49),
                (700019470986379264, 61),
                (431008558088192, 50),
                (448600744132608, 50),
                (431008558088192, 50),
                (483785116221440, 50),
                (431008558088192, 50),
                (448600744132608, 50),
                (431008558088192, 50),
                (1382314814533009408, 62),
                (853221023154176, 51),
                (870813209198592, 51),
                (853221023154176, 51),
                (905997581287424, 51),
                (853221023154176, 51),
                (870813209198592, 51),
                (853221023154176, 51),
                (976366325465088, 51),
                (853221023154176, 51),
                (870813209198592, 51),
                (853221023154176, 51),
                (905997581287424, 51),
                (853221023154176, 51),
                (870813209198592, 51),
                (853221023154176, 51),
                (2729172578093498368, 63),
                (1693247906775040, 52),
                (1702043999797248, 52),
                (1693247906775040, 52),
                (1719636185841664, 52),
                (1693247906775040, 52),
                (1702043999797248, 52),
                (1693247906775040, 52),
                (1754820557930496, 52),
                (1693247906775040, 52),
                (1702043999797248, 52),
                (1693247906775040, 52),
                (1719636185841664, 52),
                (1693247906775040, 52),
                (1702043999797248, 52),
                (1693247906775040, 52),
                (1825189302108160, 52),
                (1693247906775040, 52),
                (1702043999797248, 52),
                (1693247906775040, 52),
                (1719636185841664, 52),
                (1693247906775040, 52),
                (1702043999797248, 52),
                (1693247906775040, 52),
                (1754820557930496, 52),
                (1693247906775040, 52),
                (1702043999797248, 52),
                (1693247906775040, 52),
                (1719636185841664, 52),
                (1693247906775040, 52),
                (1702043999797248, 52),
                (1693247906775040, 52),
                (1965926790463488, 52),
                (1693247906775040, 52),
                (1702043999797248, 52),
                (1693247906775040, 52),
                (1719636185841664, 52),
                (1693247906775040, 52),
                (1702043999797248, 52),
                (1693247906775040, 52),
                (1754820557930496, 52),
                (1693247906775040, 52),
                (1702043999797248, 52),
                (1693247906775040, 52),
                (1719636185841664, 52),
                (1693247906775040, 52),
                (1702043999797248, 52),
                (1693247906775040, 52),
                (1825189302108160, 52),
                (1693247906775040, 52),
                (1702043999797248, 52),
                (1693247906775040, 52),
                (1719636185841664, 52),
                (1693247906775040, 52),
                (1702043999797248, 52),
                (1693247906775040, 52),
                (1754820557930496, 52),
                (1693247906775040, 52),
                (1702043999797248, 52),
                (1693247906775040, 52),
                (1719636185841664, 52),
                (1693247906775040, 52),
                (1702043999797248, 52),
                (1693247906775040, 52),
                (5387426656195444736, 64),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3393092883316736, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3410685069361152, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3393092883316736, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3445869441449984, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3393092883316736, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3410685069361152, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3393092883316736, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3516238185627648, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3393092883316736, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3410685069361152, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3393092883316736, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3445869441449984, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3393092883316736, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3410685069361152, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3393092883316736, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3656975673982976, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3393092883316736, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3410685069361152, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3393092883316736, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3445869441449984, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3393092883316736, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3410685069361152, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3393092883316736, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3516238185627648, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3393092883316736, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3410685069361152, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3393092883316736, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3445869441449984, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3393092883316736, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3410685069361152, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3393092883316736, 53),
                (3379898743783424, 53),
                (3384296790294528, 53),
                (3379898743783424, 53),
                (3938450650693632, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3394192394944512, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3411784580988928, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3394192394944512, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3446968953077760, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3394192394944512, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3411784580988928, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3394192394944512, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3517337697255424, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3394192394944512, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3411784580988928, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3394192394944512, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3446968953077760, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3394192394944512, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3411784580988928, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3394192394944512, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3658075185610752, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3394192394944512, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3411784580988928, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3394192394944512, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3446968953077760, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3394192394944512, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3411784580988928, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3394192394944512, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3517337697255424, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3394192394944512, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3411784580988928, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3394192394944512, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3446968953077760, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3394192394944512, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3411784580988928, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3394192394944512, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (3385396301922304, 53),
                (3378799232155648, 53),
                (3380998255411200, 53),
                (3378799232155648, 53),
                (5245567391101878272, 64),
                (0, 2),
                (5953305708593152, 54),
                (11768622707900416, 55),
                (7146825580544, 44),
                (23260718241415168, 56),
                (13743895347200, 45),
                (14843406974976, 45),
                (13743895347200, 45),
                (45967832378245120, 57),
                (26938034880512, 46),
                (28037546508288, 46),
                (26938034880512, 46),
                (30236569763840, 46),
                (26938034880512, 46),
                (28037546508288, 46),
                (26938034880512, 46),
                (90827906791505920, 58),
                (53326313947136, 47),
                (54425825574912, 47),
                (53326313947136, 47),
                (56624848830464, 47),
                (53326313947136, 47),
                (54425825574912, 47),
                (53326313947136, 47),
                (61022895341568, 47),
                (53326313947136, 47),
                (54425825574912, 47),
                (53326313947136, 47),
                (56624848830464, 47),
                (53326313947136, 47),
                (54425825574912, 47),
                (53326313947136, 47),
                (179439747897229312, 59)
            };
        }
    }
}
