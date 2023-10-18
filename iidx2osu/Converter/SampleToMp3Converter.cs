using iidx2osu.Parser;
using NAudio.Lame;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace iidx2osu.Converter;

public static class SampleToMp3Converter
{
    private const string ResultPath = OsuConverter.ResultPath;

    public static string GetAudioPath(int songId)
    {
        var root = Path.Join(ResultPath, "audios");

        if (!Directory.Exists(root))
        {
            Directory.CreateDirectory(root);
        }

        return Path.Join(root, $"{songId}.mp3");
    }

    public static string GetAudioPath(int songId, int sampleId)
    {
        var p = Path.Join(ResultPath, "samples", $"{songId}", $"{sampleId - 1}");
        if (File.Exists(p + ".wav")) return p + ".wav";
        return p + ".wma";
    }

    private static void WriteAudios(int songId, IReadOnlyList<byte[]> bytes, bool isWma)
    {
        var path = Path.Join(ResultPath, "samples", $"{songId}");

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        for (var i = 0; i < bytes.Count; i++)
        {
            var file = Path.Join(path, $"{i}.{(isWma ? "wma" : "wav")}");

            if (File.Exists(file))
            {
                continue;
            }

            File.WriteAllBytes(file, bytes[i]);
        }
    }

    private static VolumeSampleProvider ReSample(ISampleProvider provider)
    {
        return new VolumeSampleProvider(
            new WaveToSampleProvider(
                new MediaFoundationResampler(
                    new SampleToWaveProvider(provider),
                    WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))
                {
                    ResamplerQuality = 60
                }
            )
        );
    }

    private static ISampleProvider GetSampleProvider(MusicInfo info, Sample sample)
    {
        var reader = new AudioFileReader(GetAudioPath(info.SongId, sample.SampleId));

        var volSample = new VolumeSampleProvider(reader);

        if (volSample.WaveFormat.Channels == 1)
        {
            volSample = new VolumeSampleProvider(volSample.ToStereo());
        }

        if (volSample.WaveFormat.SampleRate != 44100)
        {
            volSample = ReSample(volSample);
        }

        volSample.Volume = 1;

        ISampleProvider provider = new OffsetSampleProvider(volSample)
        {
            DelayBy = TimeSpan.FromMilliseconds(sample.Offset)
        };

        if (sample.Stereo != 0 && sample.Stereo != 8)
        {
            // 1-7 is left, 9-15 is right
            provider = new PanningSampleProvider(provider)
            {
                Pan = (sample.Stereo - 8) / 7f
            };
        }

        return provider;
    }

    private static void GenerateMainMp3(MusicInfo info, IEnumerable<Sample> samples)
    {
        var mixed = samples.AsParallel().Select(sample => GetSampleProvider(info, sample)).ToList();

        while (mixed.Count > 1)
        {
            mixed = mixed
                .Select((x, i) => (x, i))
                .GroupBy(x => x.i / 128)
                .AsParallel()
                .Select(x => new MixingSampleProvider(x.Select(y => y.x)) as ISampleProvider)
                .ToList();
        }

        var id3 = new ID3TagData
        {
            Title  = info.Title,
            Album  = info.Title,
            Artist = info.Artist,
        };
        id3.Artist = info.Artist;
        id3.Genre  = info.Genre;

        var temp = Path.GetTempFileName() + ".wav";

        WaveFileWriter.CreateWaveFile16(temp, mixed[0]);

        using (var reader = new AudioFileReader(temp))
        using (var writer = new LameMP3FileWriter(GetAudioPath(info.SongId), reader.WaveFormat, 320, id3))
        {
            reader.CopyTo(writer);
        }

        File.Delete(temp);
    }

    private static List<byte[]> ReadSounds(string soundPath)
    {
        if (soundPath.EndsWith(".s3p"))
        {
            return S3PParser.Parse(soundPath).ToList();
        }

        if (soundPath.EndsWith(".2dx"))
        {
            return TwoDxParser.Parse(soundPath).ToList();
        }

        throw new Exception("Unknown sound file format");
    }

    private static bool NeedWrite(int songId, IEnumerable<int> ids)
    {
        return ids.Any(id => !File.Exists(GetAudioPath(songId, id)));
    }

    private static bool NeedGenerate(int songId)
    {
        return !File.Exists(GetAudioPath(songId));
    }

    public static void Convert(MusicInfo info, List<HashSet<Sample>> samples, string soundPath)
    {
        var intersection = samples.Aggregate((x, y) => x.Intersect(y).ToHashSet());

        if (intersection.Count < 100)
        {
            throw new Exception("intersection is too small");
        }

        samples.ForEach(x => x.ExceptWith(intersection));

        if (!NeedGenerate(info.SongId)) return;

        var sampleIds = samples.SelectMany(x => x.Select(y => y.SampleId)).ToHashSet();

        if (NeedWrite(info.SongId, sampleIds))
        {
            WriteAudios(info.SongId, ReadSounds(soundPath), soundPath.EndsWith(".s3p"));
        }

        GenerateMainMp3(info, intersection);
    }
}