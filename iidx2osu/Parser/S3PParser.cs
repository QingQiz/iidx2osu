using System.Runtime.InteropServices;
using System.Text;

namespace iidx2osu.Parser;

public static class S3PParser
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private unsafe struct Header
    {
        public fixed byte Magic[4];
        public UInt32 NumFiles;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private unsafe struct SoundFileHeader
    {
        private fixed byte _magic[4];
        public UInt32 HeaderSize;
        public Int32 WaveSize;

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

        var fileOffsets = new List<(uint, int)>();

        for (var i = 0; i < header.NumFiles; i++)
        {
            var offset = reader.ReadUInt32();
            var size   = reader.ReadInt32();
            fileOffsets.Add((offset, size));
        }

        foreach (var (offset, _) in fileOffsets)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            var soundFileHeader = Utils.Byte2Type<SoundFileHeader>(reader);
            
            if (soundFileHeader.GetMagic() != "S3V0")
            {
                throw new Exception($"Invalid S3P file: {file}");
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