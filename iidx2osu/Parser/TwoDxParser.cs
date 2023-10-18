using System.Runtime.InteropServices;
using System.Text;

namespace iidx2osu.Parser;

public static class TwoDxParser
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private unsafe struct Header
    {
        public fixed byte Magic[16];
        public UInt32 HeaderSize;
        public UInt32 NumFiles;
        public fixed byte _p[48];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private unsafe struct SoundFileHeader
    {
        private fixed byte _magic[4];
        public UInt32 HeaderSize;
        public Int32 WaveSize;
        private fixed byte _p1[2];
        public Int16 Track;
        private fixed byte _p2[2];
        public Int16 Attenuation;
        public Int32 Loop;

        public string GetMagic()
        {
            fixed (SoundFileHeader* p = &this)
            {
                return Encoding.Default.GetString(Utils.GetBytes(p->_magic, 4));
            }
        }
    }

    public static IEnumerable<byte[]> Parse(string file)
    {
        var stream = File.OpenRead(file);
        var reader = new BinaryReader(stream);

        var header = Utils.Byte2Type<Header>(reader);

        var fileOffsets = new List<int>();

        for (var i = 0; i < header.NumFiles; i++)
        {
            fileOffsets.Add(reader.ReadInt32());
        }

        foreach (var offset in fileOffsets)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            var soundFileHeader = Utils.Byte2Type<SoundFileHeader>(reader);

            if (soundFileHeader.GetMagic() != "2DX9")
            {
                throw new Exception($"Invalid 2DX file: {file}");
            }

            var waveOffset = offset + soundFileHeader.HeaderSize;

            stream.Seek(waveOffset, SeekOrigin.Begin);
            var waveData = reader.ReadBytes(soundFileHeader.WaveSize);

            yield return waveData;
        }

        reader.Close();
        stream.Close();
    }
}