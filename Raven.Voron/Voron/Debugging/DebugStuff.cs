﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Voron.Impl;
using Voron.Impl.FileHeaders;
using Voron.Trees;
using Voron.Trees.Fixed;

namespace Voron.Debugging
{
	public class DebugStuff
	{
		[Conditional("DEBUG")]
        public static void RenderFreeSpace(Transaction tx)
        {
            RenderAndShow(tx, tx.ReadTree(tx.Environment.State.FreeSpaceRoot.Name).State.RootPageNumber);
        }

		[Conditional("DEBUG")]
		public static void DumpHumanReadable(Transaction tx, long startPageNumber,string filenamePrefix = null)
		{
			if (Debugger.IsAttached == false)
				return;
			var path = Path.Combine(Environment.CurrentDirectory, String.Format("{0}tree.hdump", filenamePrefix ?? String.Empty));
			TreeDumper.DumpHumanReadable(tx, path, tx.GetReadOnlyPage(startPageNumber));
		}

		public unsafe static bool HasDuplicateBranchReferences(Transaction tx, Page start,out long pageNumberWithDuplicates)
		{
			var stack = new Stack<Page>();
			var existingTreeReferences = new ConcurrentDictionary<long, List<long>>();
			stack.Push(start);
			while (stack.Count > 0)
			{
				var currentPage = stack.Pop();
				if (currentPage.IsBranch)
				{
					for (int nodeIndex = 0; nodeIndex < currentPage.NumberOfEntries; nodeIndex++)
					{
						var node = currentPage.GetNode(nodeIndex);

						existingTreeReferences.AddOrUpdate(currentPage.PageNumber, new List<long> { node->PageNumber },
							(branchPageNumber, pageNumberReferences) =>
							{
								pageNumberReferences.Add(node->PageNumber);
								return pageNumberReferences;
							});
					}

					for (int nodeIndex = 0; nodeIndex < currentPage.NumberOfEntries; nodeIndex++)
					{
						var node = currentPage.GetNode(nodeIndex);
						if (node->PageNumber < 0 || node->PageNumber > tx.State.NextPageNumber)
						{
							throw new InvalidDataException("found invalid reference on branch - tree is corrupted");
						}

						var child = tx.GetReadOnlyPage(node->PageNumber);
						stack.Push(child);
					}

				}
			}

			Func<long, HashSet<long>> relevantPageReferences =
				branchPageNumber => new HashSet<long>(existingTreeReferences
					.Where(kvp => kvp.Key != branchPageNumber)
					.SelectMany(kvp => kvp.Value));

			// ReSharper disable once LoopCanBeConvertedToQuery
			foreach (var branchReferences in existingTreeReferences)
			{
				if (
					branchReferences.Value.Any(
						referencePageNumber => relevantPageReferences(branchReferences.Key).Contains(referencePageNumber)))
				{
					pageNumberWithDuplicates = branchReferences.Key;
					return true;
				}
			}
			pageNumberWithDuplicates = -1;
			return false;
		}

        private const string css = @".css-treeview ul,
.css-treeview li
{
    padding: 0;
    margin: 0;
    list-style: none;
}
 
.css-treeview input
{
    position: absolute;
    opacity: 0;
}
 
.css-treeview
{
    font: normal 11px 'Segoe UI', Arial, Sans-serif;
    -moz-user-select: none;
    -webkit-user-select: none;
    user-select: none;
}
 
.css-treeview a
{
    color: #00f;
    text-decoration: none;
}
 
.css-treeview a:hover
{
    text-decoration: underline;
}
 
.css-treeview input + label + ul
{
    margin: 0 0 0 22px;
}
 
.css-treeview input ~ ul
{
    display: none;
}
 
.css-treeview label,
.css-treeview label::before
{
    cursor: pointer;
}
 
.css-treeview input:disabled + label
{
    cursor: default;
    opacity: .6;
}
 
.css-treeview input:checked:not(:disabled) ~ ul
{
    display: block;
}
 
.css-treeview label,
.css-treeview label::before
{
    background: url('http://experiments.wemakesites.net/pages/css3-treeview/example/icons.png') no-repeat;
}
 
.css-treeview label,
.css-treeview a,
.css-treeview label::before
{
    display: inline-block;
    height: 16px;
    line-height: 16px;
    vertical-align: middle;
}
 
.css-treeview label
{
    background-position: 18px 0;
}
 
.css-treeview label::before
{
    content: '';
    width: 16px;
    margin: 0 22px 0 0;
    vertical-align: middle;
    background-position: 0 -32px;
}
 
.css-treeview input:checked + label::before
{
    background-position: 0 -16px;
}
 
/* webkit adjacent element selector bugfix */
@media screen and (-webkit-min-device-pixel-ratio:0)
{
    .css-treeview 
    {
        -webkit-animation: webkit-adjacent-element-selector-bugfix infinite 1s;
    }
 
    @-webkit-keyframes webkit-adjacent-element-selector-bugfix 
    {
        from 
        { 
            padding: 0;
        } 
        to 
        { 
            padding: 0;
        }
    }
}";

	    [Conditional("DEBUG")]
	    public unsafe static void RenderAndShow_FixedSizeTree(Transaction tx, Tree tree, Slice name)
	    {
	        RenderHtmlTreeView(writer =>
	        {
	            var ptr = tree.DirectRead(name);
	            if (ptr == null)
	            {
	                writer.WriteLine("<p>empty fixed size tree</p>");
	            }
	            else if (((FixedSizeTreeHeader.Embedded*) ptr)->Flags == FixedSizeTreeHeader.OptionFlags.Embedded)
	            {
	                var header = ((FixedSizeTreeHeader.Embedded*) ptr);
	                writer.WriteLine("<p>Number of entries: {0:#,#;;0}, val size: {1:#,#;;0}.</p>", header->NumberOfEntries, header->ValueSize);
	                writer.WriteLine("<ul>");
	                var dataStart = ptr + sizeof (FixedSizeTreeHeader.Embedded);
	                for (int i = 0; i < header->NumberOfEntries; i++)
	                {
	                    var key = *(long*) (dataStart + ((sizeof (long) + header->ValueSize)*i));
	                    writer.WriteLine("<li>{0:#,#;;0}</li>", key);
	                }
	                writer.WriteLine("</ul>");
	            }
	            else
	            {
	                var header = (FixedSizeTreeHeader.Large*) ptr;
	                writer.WriteLine("<p>Number of entries: {0:#,#;;0}, val size: {1:#,#;;0}.</p>", header->NumberOfEntries, header->ValueSize);
	                writer.WriteLine("<div class='css-treeview'><ul>");

	                var page = tx.GetReadOnlyPage(header->RootPageNumber);

	                RenderFixedSizeTreePage(tx, page, writer, header, "Root", true);

	                writer.WriteLine("</ul></div>");
	            }
	        });
	    }

	    private static void RenderHtmlTreeView(Action<TextWriter> action)
	    {
            if (Debugger.IsAttached == false)
                return;

            var output = Path.GetTempFileName() + ".html";
	        using (var sw = new StreamWriter(output))
	        {
	            sw.WriteLine("<html><head><style>{0}</style></head><body>", css);
	            action(sw);
	            sw.WriteLine("</body></html>");
	            sw.Flush();
	        }
	        Process.Start(output);
	    }

	    private unsafe static void RenderFixedSizeTreePage(Transaction tx, Page page, TextWriter sw, FixedSizeTreeHeader.Large* header, string text, bool open)
	    {
	        sw.WriteLine(
                "<ul><li><input type='checkbox' id='page-{0}' {3} /><label for='page-{0}'>{4}: Page {0:#,#;;0} - {1} - {2:#,#;;0} entries</label><ul>",
	            page.PageNumber, page.IsLeaf ? "Leaf" : "Branch", page.FixedSize_NumberOfEntries, open ? "checked" : "", text);

	        for (int i = 0; i < page.FixedSize_NumberOfEntries; i++)
	        {
	            if (page.IsLeaf)
	            {
	                var key =
	                    *(long*) (page.Base + page.FixedSize_StartPosition + (((sizeof (long) + header->ValueSize))*i));
                    sw.Write("{0:#,#;;0}, ", key);
	            }
	            else
	            {
                    var key =
                     *(long*)(page.Base + page.FixedSize_StartPosition + (((sizeof(long) + sizeof(long))) * Math.Max(i,1)));
                    var pageNum = *(long*)(page.Base + page.FixedSize_StartPosition + (((sizeof(long) + sizeof(long))) * i) + sizeof(long));

	                var s = key.ToString("#,#");
	                if (i == 0)
	                    s = "[smallest]";

	                RenderFixedSizeTreePage(tx, tx.GetReadOnlyPage(pageNum), sw, header, s, false);
	            }
	        }

            sw.WriteLine("</ul></li></ul>");
	    }

	    [Conditional("DEBUG")]
	    public static void RenderAndShow(Tree tree)
	    {
	        RenderAndShow(tree.Tx, tree.State.RootPageNumber);
	    }

        [Conditional("DEBUG")]
		public static void RenderAndShow(Transaction tx, long startPageNumber)
		{
            RenderHtmlTreeView(writer =>
            {
                writer.WriteLine("<div class='css-treeview'><ul>");

                var page = tx.GetReadOnlyPage(startPageNumber);
                RenderPage(tx, page, writer, "Root", true);

                writer.WriteLine("</ul></div>");
            });

        }

	    private unsafe static void RenderPage(Transaction tx, Page page, TextWriter sw, string text, bool open)
	    {
            sw.WriteLine(
               "<ul><li><input type='checkbox' id='page-{0}' {3} /><label for='page-{0}'>{4}: Page {0:#,#;;0} - {1} - {2:#,#;;0} entries</label><ul>",
               page.PageNumber, page.IsLeaf ? "Leaf" : "Branch", page.NumberOfEntries, open ? "checked" : "", text);

            for (int i = 0; i < page.NumberOfEntries; i++)
            {
                var nodeHeader = page.GetNode(i);
                if (page.IsLeaf)
                {
                    var key = new Slice(nodeHeader).ToString();
                    sw.Write("<li>{0} {1} - size: {2:#,#}</li>", key, nodeHeader->Flags, NodeHeader.GetDataSize(tx, nodeHeader));
                }
                else
                {
                    var key = new Slice(nodeHeader).ToString();

                    var pageNum = nodeHeader->PageNumber;

                    if (i == 0)
                        key = "[smallest]";

                    RenderPage(tx, tx.GetReadOnlyPage(pageNum), sw, key, false);
                }
            }

	        sw.WriteLine("</ul></li></ul>");
        }
	}
}