using System.Text;
using iidx2osu.Parser;

namespace iidx2osu.Converter;

public static class OsuConverter
{
    public const string ResultPath = @"F:\workspace\iidx";
    public const string BeatmapRoot = $@"{ResultPath}\osu";
    //public const string BeatmapRoot = $@"O:\GameStorage\osu!\Songs";
    //public const string BeatmapRoot = @"F:\workspace\iidx\patch";
    private const bool ShouldCopyFiles = true;

    private static decimal BeatDuration(double bpm)
    {
        if (bpm == 0) return 999999;

        return 60.0m / (decimal)bpm * 1000;
    }

    private static void GenerateMeta(StringBuilder bd, MusicInfo info, string difficultyName, int difficulty, bool noSv, int keyCount)
    {
        bd.AppendLine("osu file format v14\n");

        bd.AppendLine("[General]");
        bd.AppendLine("Mode: 3");
        bd.AppendLine("SampleSet: Soft");
        bd.AppendLine("Countdown: 0");

        bd.AppendLine($"AudioFilename: {info.SongId}.mp3");

        if (keyCount == 8)
        {
            bd.AppendLine("SpecialStyle: 1");
        }

        bd.AppendLine("[Editor]");
        bd.AppendLine("DistanceSpacing: 1");
        bd.AppendLine("BeatDivisor: 1");
        bd.AppendLine("GridSize: 1");
        bd.AppendLine("TimelineZoom: 1");

        bd.AppendLine("[Metadata]");
        bd.AppendLine($"Title:{info.TitleEng}");
        bd.AppendLine($"Artist:{info.Artist}");
        bd.AppendLine($"TitleUnicode:{info.Title}");
        bd.AppendLine($"ArtistUnicode:{info.Artist}");
        bd.AppendLine("Creator:QINGQIZ");
        bd.AppendLine("Source:IIDX");
        bd.AppendLine($"Tags:{info.Genre} BMS Converted");
        bd.AppendLine($"Version:[IIDX]{(noSv ? "[NSV]" : "")}[{difficultyName}]{difficulty}");
        bd.AppendLine("BeatmapID:0");
        bd.AppendLine("BeatmapSetID:0");

        bd.AppendLine("[Difficulty]");
        bd.AppendLine("HPDrainRate:8.5");
        bd.AppendLine($"CircleSize:{keyCount}");
        bd.AppendLine("OverallDifficulty:8.0");
        bd.AppendLine("ApproachRate:0");
        bd.AppendLine("SliderMultiplier:1");
        bd.AppendLine("SliderTickRate:1");
        bd.AppendLine("osu file format v14\n");

        bd.AppendLine("[General]");
        bd.AppendLine("Mode: 3");
        bd.AppendLine("SampleSet: Soft");
        bd.AppendLine("Countdown: 0");
    }

    private static int FindMainBpm(IReadOnlyList<TimingPoint> timings, Chart chart)
    {
        var dict = new Dictionary<int, int>();

        var maxOffset = chart.Notes.Values.SelectMany(x => x).Max(x => x.Offset);

        var current       = timings[0].Bpm;
        var currentOffset = timings[0].Offset;

        for (var i = 1; i < timings.Count; i++)
        {
            if (timings[i].Bpm == current) continue;

            dict.TryAdd(current, 0);
            dict[current] = timings[i].Offset - currentOffset;
        }

        dict.TryAdd(current, 0);
        dict[current] = maxOffset - currentOffset;

        return dict.MaxBy(x => x.Value).Key;
    }

    private static void GenerateTimingPoints(StringBuilder bd, Chart chart, bool noSv)
    {
        var timings = chart.TimingPoints.OrderBy(x => x.Offset).ToList();

        var mainBpm = FindMainBpm(timings, chart);

        bd.AppendLine("[TimingPoints]");
        bd.AppendLine($"0,{BeatDuration(mainBpm)},4,2,0,100,1,0");

        foreach (var t in timings.Where(t => !noSv || t.Bpm != 0))
        {
            bd.AppendLine($"{t.Offset},{BeatDuration(t.Bpm)},4,2,0,100,1,0");

            if (noSv)
            {
                bd.AppendLine($"{t.Offset},-{t.Bpm * 100 / mainBpm},4,2,0,100,0,0");
            }
        }

        var l = 0;
        var r = 0;

        var meters = chart.MeterChanges.OrderBy(x => x.Offset).ToList();

        while (true)
        {
            if (l >= timings.Count || r >= meters.Count) break;

            if (timings[l].Offset <= meters[r].Offset)
            {
                l++;
                continue;
            }

            do
            {
                l--;
            } while (l >= 0 && noSv && timings[l].Bpm == 0);

            r++;

            if (l < 0) continue;

            bd.AppendLine($"{meters[r - 1].Offset},{BeatDuration(timings[l].Bpm)},{meters[r - 1].L},2,0,100,1,0");
        }
    }

    private static void GenerateBgm(StringBuilder bd, HashSet<Sample> samples, bool isWma)
    {
        bd.AppendLine("[Events]");
        foreach (var sample in samples)
        {
            bd.AppendLine($"Sample,{sample.Offset},0,\"{sample.SampleId}.{(isWma ? "wma" : "wav")}\",100");
        }
    }

    private static void GenerateNotes(StringBuilder bd, Chart chart, bool includePlate)
    {
        var laneSize = 512.0 / (includePlate ? chart.Notes.Count : chart.Notes.Count - 1);

        const int hitSoundVolume = 0;

        bd.AppendLine("[HitObjects]");

        foreach (var (laneNumber, notes) in chart.Notes)
        {
            if (!includePlate && laneNumber == 0) continue;

            var xPos = (int)Math.Floor(laneSize * laneNumber + laneSize / 2 - (includePlate ? 0 : laneSize));

            foreach (var note in notes)
            {
                var ln   = note.Duration != 0;
                var type = ln ? 1 << 7 : 1;

                bd.AppendLine(ln
                    ? $"{xPos},192,{note.Offset},{type},0,{note.Offset + note.Duration}:0:0:0:{hitSoundVolume}:"
                    : $"{xPos},192,{note.Offset},{type},0,0:0:0:{hitSoundVolume}:");
            }
        }
    }

    private static void Swap<T>(List<T> list, int idx1, int idx2)
    {
        (list[idx2], list[idx1]) = (list[idx1], list[idx2]);
    }

    public static void Convert(MusicInfo info, List<Chart> charts, string soundPath)
    {
        if (charts[0].NoteCount < charts[1].NoteCount)
        {
            Swap(charts, 0, 1);
        }

        if (charts[6].NoteCount < charts[7].NoteCount)
        {
            Swap(charts, 6, 7);
        }

        var samples = info.Difficulties.Zip(charts)
            .Where(x => x.First != 0)
            .Select(x => x.Second.GetSamples()).ToList();

        SampleToMp3Converter.Convert(info, samples, soundPath);

        var isWma = soundPath.EndsWith(".wma");

        var targetPath = Path.Join(BeatmapRoot, $"[iidx]{info.SongId}");

        Directory.CreateDirectory(targetPath);

        var idx = 0;

        for (var i = 0; i < info.Difficulties.Length; i++)
        {
            if (info.Difficulties[i] == 0) continue;

            var param = (charts[i].HasSv ? new[] { true, false } : new[] { false })
                .SelectMany(x => new[] { (x, true), (x, false) })
                .ToList();

            var diffName = MusicInfo.DifficultyName(i);
            var diff     = info.Difficulties[i];
            var chart    = charts[i];
            var sample   = samples[idx++];

            foreach (var (noSv, includePlate) in param)
            {
                // dp mode always include plate
                if (!includePlate && charts[i].Notes.Count > 8) continue;

                var filename = $"{info.SongId}_{(i > 5 ? "DP" : "SP")}_{diff}_{(noSv ? "NSV" : "SV")}_{(includePlate ? "P" : "NP")}.osu";

                // if (File.Exists(Path.Join(targetPath, filename))) continue;

                var bd = new StringBuilder();

                GenerateMeta(bd, info, diffName, diff, noSv, includePlate ? chart.Notes.Count : chart.Notes.Count - 1);
                GenerateTimingPoints(bd, chart, noSv);
                GenerateBgm(bd, sample, isWma);
                GenerateNotes(bd, chart, includePlate);

                File.WriteAllText(Path.Join(targetPath, filename), bd.ToString());
            }
        }

        if (!ShouldCopyFiles) return;

        var files2Copy = new List<string>
        {
            SampleToMp3Converter.GetAudioPath(info.SongId)
        };

        files2Copy.AddRange(samples
            .SelectMany(x => x)
            .Select(sample => SampleToMp3Converter.GetAudioPath(info.SongId, sample.SampleId))
        );

        foreach (var file in files2Copy)
        {
            var fn   = Path.GetFileName(file);
            var dest = Path.Join(targetPath, fn);

            if (File.Exists(dest)) continue;

            File.Copy(file, dest);
        }
    }
}