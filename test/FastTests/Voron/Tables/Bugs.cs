﻿using Sparrow;
using System;
using System.Collections.Generic;
using System.IO;
using Voron;
using Voron.Data.Tables;
using Voron.Util.Conversion;
using Xunit;

namespace FastTests.Voron.Tables
{
    public unsafe class Bugs : TableStorageTest
    {
        public const int ItemsPerTransaction = 100;
        public const int Transactions = 10;
        public const string Template = "000000000000000000000000000000000000000000000000000";

        [Fact]
        public void RepeatedInsertingInTheSameTransaction()
        {
            var schema = new TableSchema()
                                .DefineKey(new TableSchema.SchemaIndexDef
                                {
                                    StartIndex = 0,
                                });


            using (var tx = Env.WriteTransaction())
            {
                schema.Create(tx, "docs", 16);

                tx.Commit();
            }

            var value = new byte[100];
            new Random().NextBytes(value);
            var ms = new MemoryStream(value);

            using (var tx = Env.WriteTransaction())
            {
                var docs = tx.OpenTable(schema, "docs");
                for (long i = 0; i < Transactions * ItemsPerTransaction; i++)
                {
                    ms.Position = 0;

                    SetHelper(docs, i.ToString(Template), ms);
                }

                tx.Commit();
            }

            using (var tx = Env.ReadTransaction())
            {
                var docs = tx.OpenTable(schema, "docs");

                var array = new byte[100];
                for (int i = 0; i < Transactions * ItemsPerTransaction; i++)
                {
                    var key = i.ToString(Template);
                    Slice slice;
                    Slice.From(tx.Allocator, key, out slice);
                    var tableReader = docs.ReadByKey(slice);

                    Assert.NotNull(tableReader);

                    int size;
                    byte* buffer = tableReader.Read(1, out size);
                    Assert.True(buffer != null);
                    Assert.Equal(100, size);
                }
            }        
        }

        [Fact]
        public void CanInsertThenDeleteBySecondary2()
        {
            using (var tx = Env.WriteTransaction())
            {
                DocsSchema.Create(tx, "docs", 16);

                tx.Commit();
            }

            for (int j = 0; j < 10; j++)
            {
                using (var tx = Env.WriteTransaction())
                {
                    var docs = tx.OpenTable(DocsSchema, "docs");

                    for (int i = 0; i < 1000; i++)
                    {
                        SetHelper(docs, "users/" + i, "Users", 1L + i, "{'Name': 'Oren'}");
                    }

                    tx.Commit();
                }

                using (var tx = Env.WriteTransaction())
                {
                    var docs = tx.OpenTable(DocsSchema, "docs");

                    var ids = new List<long>();
                    foreach (var sr in docs.SeekForwardFrom(DocsSchema.Indexes[EtagsSlice], Slices.BeforeAllKeys))
                    {
                        foreach (var tvr in sr.Results)
                            ids.Add(tvr.Id);
                    }

                    foreach (var id in ids)
                        docs.Delete(id);

                    tx.Commit();
                }

                using (var tx = Env.ReadTransaction())
                {
                    var docs = tx.OpenTable(DocsSchema, "docs");

                    Slice val;
                    Slice.From(Allocator, EndianBitConverter.Big.GetBytes(1), out val);
                    var reader = docs.SeekForwardFrom(DocsSchema.Indexes[EtagsSlice], val);
                    Assert.Empty(reader);
                }
            }
        }
    }
}