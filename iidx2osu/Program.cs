using System.Diagnostics.PerformanceData;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using iidx2osu.Converter;
using iidx2osu.Parser;
using NAudio.Midi;
using Newtonsoft.Json;

namespace iidx2osu;

public class Cache
{
    public Dictionary<int, MusicInfo>? MusicInfos { get; set; }
    public Dictionary<int, string>? SoundPath { get; set; }
    public Dictionary<int, string>? ChartPath { get; set; }
    public Dictionary<int, string>? InvalidSoundPath { get; set; }

    public List<int> ConvertError { get; set; } = new();
}

public static class Program
{
    private const string ResultPath = OsuConverter.ResultPath;
    private const string Root = @"D:\Downloads\npupt\LDJ-003-2022103100";
    private const string MusicDataBin = $@"{Root}\contents\data\info\0\music_data.bin";
    private const string MusicTitle = $@"{Root}\contents\data\info\0\video_music_list.xml";
    private const string SoundDataDir = $@"{Root}\contents\data\sound";
    private const string CachePath = $@"{ResultPath}\cache.json";

    private static void ReadData(Cache cache)
    {
        var xml = XElement.Parse(File.ReadAllText(MusicTitle));

        var dict = new Dictionary<int, (string, string)>();
        foreach (var music in xml.Elements("music"))
        {
            var id     = int.Parse(music.Attribute("id")!.Value.TrimStart('0'));
            var title  = music.Element("info")!.Element("title_name")!.Value;
            var artist = music.Element("info")!.Element("artist_name")!.Value;
            dict[id] = (title, artist);
        }

        cache.MusicInfos = MusicDataParser.Parse(MusicDataBin).ToDictionary(x => (int)x.SongId, x => x);
        cache.MusicInfos.Values.ToList().ForEach(i =>
        {
            if (!dict.ContainsKey(i.SongId))
            {
                Console.WriteLine($"Missing title: {i.SongId}. Using fallback: {i.Title}");
                return;
            }

            var x = dict[i.SongId];
            i.Title  = x.Item1;
            i.Artist = x.Item2;
        });
    }

    private static void ReadPath(Cache cache)
    {
        cache.SoundPath        = new Dictionary<int, string>();
        cache.ChartPath        = new Dictionary<int, string>();
        cache.InvalidSoundPath = new Dictionary<int, string>();

        foreach (var info in cache.MusicInfos!.Values)
        {
            var idStr = info.SongId.ToString("00000");
            var dir   = Path.Join(SoundDataDir, idStr);

            var soundFiles = Directory
                .GetFiles(dir, "*.*")
                .Where(x => x.EndsWith(".s3p") || x.EndsWith(".2dx"))
                .Where(x => !x.Contains("_pre"))
                .ToList();

            cache.ChartPath[info.SongId] = Path.Join(dir, $"{idStr}.1");

            if (soundFiles.Count != 1)
            {
                Console.WriteLine($"Multi sound files: {string.Join(", ", soundFiles)}");
                cache.InvalidSoundPath[info.SongId] = string.Join('|', soundFiles);
                continue;
            }

            cache.SoundPath[info.SongId] = soundFiles[0];
        }
    }

    private static readonly object LockWrite = new { };

    private static void Work(Cache cache, int id, CountdownEvent countdown, SemaphoreSlim limit)
    {
        try
        {
            Console.WriteLine("work: " + id);
            var charts = ChartParser.Parse(cache.ChartPath![id]);
            OsuConverter.Convert(cache.MusicInfos![id], charts, cache.SoundPath![id]);
        }
        catch (Exception e)
        {
            lock (LockWrite)
            {
                Console.WriteLine("====================================================================");
                Console.WriteLine($"Error: {Path.GetDirectoryName(cache.ChartPath![id])}");
                Console.WriteLine(e);
                Console.WriteLine("====================================================================");
                cache.ConvertError.Add(id);
            }
        }
        finally
        {
            Console.WriteLine("Remaining: " + countdown.CurrentCount);
            countdown.Signal();
            limit.Release();
        }
    }

    private static void WriteCache(Cache cache)
    {
        var writer  = new StreamWriter(CachePath);
        var jWriter = new JsonTextWriter(writer);
        jWriter.Indentation = 4;
        jWriter.Formatting  = Formatting.Indented;

        new JsonSerializer().Serialize(jWriter, cache);
        writer.Close();
    }

    public static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        Cache cache = new();

        if (File.Exists(CachePath))
        {
            var reader = new StreamReader(CachePath);
            cache = new JsonSerializer().Deserialize<Cache>(new JsonTextReader(reader))!;
            reader.Close();
        }

        if (cache.MusicInfos == null)
        {
            ReadData(cache);
            WriteCache(cache);
        }

        if (cache.SoundPath == null || cache.ChartPath == null)
        {
            ReadPath(cache);
            WriteCache(cache);
        }

        var remaining = new CountdownEvent(cache.SoundPath!.Keys.Count);
        var limit     = new SemaphoreSlim(16);

        foreach (var id in cache.SoundPath!.Keys)
        {
            limit.Wait();
            Task.Run(() => Work(cache, id, remaining, limit));
        }

        remaining.Wait();

        WriteCache(cache);
    }
}