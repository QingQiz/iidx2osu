using System.Runtime.InteropServices;
using System.Text;

namespace iidx2osu.Parser;

public class MusicInfo
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private unsafe struct MusicInfoOri
    {
        public fixed byte Title[64];
        public fixed byte EnglishTitle[64];
        public fixed byte Genre[64];
        public fixed byte Artist[64];
        private fixed byte _p1[24];
        public byte Folder;
        private fixed byte _p2[7];
        public fixed byte Difficulties[10];
        public fixed byte _p3[646];
        public Int16 SongId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private unsafe struct Header
    {
        public fixed byte Magic[4];
        public byte Version;
        private fixed byte _p[3];
        public Int16 SongCount;
        public Int16 IndexCount;
    }

    public string Title { get; set; }
    public string TitleEng { get; set; }
    public string Artist { get; set; }
    public string Genre { get; set; }
    public int[] Difficulties { get; set; }

    public static string DifficultyName(int idx) => idx switch
    {
        0  => "Hyper",
        1  => "Normal",
        2  => "Another",
        3  => "Beginner",
        4  => "Black Another",
        6  => "Hyper",
        7  => "Normal",
        8  => "Another",
        9  => "Beginner",
        10 => "Black Another",
        _  => throw new ArgumentOutOfRangeException(nameof(idx), idx, null)
    };

    public short SongId { get; set; }
    public byte Folder { get; set; }

    private MusicInfo(MusicInfoOri ori)
    {
        unsafe
        {
            var jis = Encoding.GetEncoding(932);

            Title    = jis.GetString(Utils.GetBytes(ori.Title));
            TitleEng = jis.GetString(Utils.GetBytes(ori.EnglishTitle));
            Artist   = jis.GetString(Utils.GetBytes(ori.Artist));
            Genre    = jis.GetString(Utils.GetBytes(ori.Genre));

            Difficulties = new[]
            {
                ori.Difficulties[2], ori.Difficulties[1], ori.Difficulties[3], ori.Difficulties[0], ori.Difficulties[4],
                0,
                ori.Difficulties[7], ori.Difficulties[6], ori.Difficulties[8], ori.Difficulties[5], ori.Difficulties[9]
            };

            SongId = ori.SongId;
            Folder = ori.Folder;
        }
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public MusicInfo()
    {
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public static List<MusicInfo> ParseMusicData(string file)
    {
        var stream = File.OpenRead(file);
        var reader = new BinaryReader(stream);

        var head = Utils.Byte2Type<Header>(reader);

        const int leap   = 0x52c;
        var       offset = head.IndexCount * 2 + 0x10;

        var songs = new List<MusicInfo>();

        for (var i = 0; i < head.SongCount; i++)
        {
            stream.Seek(offset + i * leap, SeekOrigin.Begin);

            var ori = Utils.Byte2Type<MusicInfoOri>(reader);
            songs.Add(new MusicInfo(ori));
        }

        reader.Close();
        stream.Close();

        return songs;
    }
}

public static class MusicDataParser
{
    public static List<MusicInfo> Parse(string file)
    {
        return MusicInfo.ParseMusicData(file);
    }
}