using System;
using System.IO;

namespace Raven.Server.Utils
{
    public class PartialStream : Stream
    {
        private readonly Stream inner;
        private int size;

        public PartialStream(Stream inner, int size)
        {
            this.inner = inner;
            this.size = size;
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (size == 0)
                return 0;
            var actualCount = Math.Min(size, count);
            var read = inner.Read(buffer, offset, actualCount);
            size -= read;
            return read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        protected override void Dispose(bool disposing)
        {
            while (size > 0)
            {
                if (inner.ReadByte() == -1)
                    break;
                size--;
            }
            base.Dispose(disposing);
        }
    }
}
