using System.Runtime.InteropServices;
using Xunit;

namespace NewClientTests
{
    public class NonLinuxFactAttribute : FactAttribute
    {
        public NonLinuxFactAttribute()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Skip = "Test cannot be run on Linux machine";
            }
        }
    }
}