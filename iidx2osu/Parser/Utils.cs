using System.Runtime.InteropServices;

namespace iidx2osu.Parser;

public static class Utils
{
    public static T Byte2Type<T>(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(Marshal.SizeOf(typeof(T)));

        var handle       = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        var theStructure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T))!;
        handle.Free();

        return theStructure;
    }

    public static unsafe byte[] GetBytes(byte* ptr, int length = 64)
    {
        var bytes = new List<byte>();

        for (var i = 0; i < length; i++)
        {
            if (ptr[i] == 0) break;
            bytes.Add(ptr[i]);
        }

        return bytes.ToArray();
    }
}