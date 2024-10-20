using Sparrow;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Voron.Impl.Scratch
{
    public class PageFromScratchBufferEqualityComparer : IEqualityComparer<PageFromScratchBuffer>
    {
        public static readonly PageFromScratchBufferEqualityComparer Instance = new PageFromScratchBufferEqualityComparer();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(PageFromScratchBuffer x, PageFromScratchBuffer y)
        {
            if (x == y) return true;
            if (x == null || y == null) return false;            

            return x.PositionInScratchBuffer == y.PositionInScratchBuffer && x.Size == y.Size && x.NumberOfPages == y.NumberOfPages && x.ScratchFileNumber == y.ScratchFileNumber;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(PageFromScratchBuffer obj)
        {
            int v = Hashing.Combine(obj.NumberOfPages, obj.ScratchFileNumber);
            int w = Hashing.Combine(obj.Size.GetHashCode(), obj.PositionInScratchBuffer.GetHashCode());
            return Hashing.Combine(v, w);
        }
    }


    public sealed class PageFromScratchBuffer
    {
        public readonly int ScratchFileNumber;
        public readonly long PositionInScratchBuffer;
        public readonly long Size;
        public readonly int NumberOfPages;
        public Page? PreviousVersion;

        public PageFromScratchBuffer( int scratchFileNumber, long positionInScratchBuffer, long size, int numberOfPages )
        {
            this.ScratchFileNumber = scratchFileNumber;
            this.PositionInScratchBuffer = positionInScratchBuffer;
            this.Size = size;
            this.NumberOfPages = numberOfPages;
        }

        public override bool Equals(object obj)
        {
            return PageFromScratchBufferEqualityComparer.Instance.Equals(this, obj as PageFromScratchBuffer);
        }

        public override int GetHashCode()
        {
            return PageFromScratchBufferEqualityComparer.Instance.GetHashCode(this);
        }

        public override string ToString()
        {
            return string.Format("PositionInScratchBuffer: {0}, ScratchFileNumber: {1},  Size: {2}, NumberOfPages: {3}", PositionInScratchBuffer, ScratchFileNumber, Size, NumberOfPages);
        }
    }
}
