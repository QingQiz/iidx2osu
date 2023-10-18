using System.Runtime.InteropServices;

namespace iidx2osu.Parser;

public record Sample(int Offset, int Stereo, int SampleId);

public record Note(int Offset, int Duration, Sample Sample);

public record TimingPoint(int Offset, int Bpm);

public record MeterChange(int Offset, int L, int R);

/// <param name="Notes">0-6 -> key, 7 -> Scratch</param>
/// <param name="BgmSounds"></param>
/// <param name="TimingPoints"></param>
/// <param name="MeterChanges"></param>
public record Chart(Dictionary<int, List<Note>> Notes, List<Sample> BgmSounds, List<TimingPoint> TimingPoints, List<MeterChange> MeterChanges)
{
    public int MaxSampleId => Notes.Count == 0
        ? 0
        : Math.Max(BgmSounds.Max(x => x.SampleId), Notes.Values.SelectMany(x => x).Select(x => x.Sample.SampleId).Max());

    public int MinSampleId => Notes.Count == 0
        ? int.MaxValue
        : Math.Min(BgmSounds.Min(x => x.SampleId), Notes.Values.SelectMany(x => x).Select(x => x.Sample.SampleId).Min());

    public HashSet<Sample> GetSamples()
    {
        var res = BgmSounds.Select(x => x with { Stereo = x.Stereo == 8 ? 0 : x.Stereo }).ToList();

        res.AddRange(Notes.Values.SelectMany(x => x).Select(x => x.Sample with { Offset = x.Offset }));

        return res.ToHashSet();
    }
    
    public bool HasSv => TimingPoints.DistinctBy(x => x.Bpm).Count() > 1;
}

public static class ChartParser
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Directory
    {
        public Int32 ChartOffset;
        public Int32 ChartLength;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ChartEvent
    {
        /// <summary>
        /// offset of the event, in ticks
        /// <remarks>
        /// Version: Ticks Per Second
        /// <list>
        /// <item> Before GOLD: 59.94 </item>
        /// <item> GOLD: 60.046 </item>
        /// <item> After GOLD: 1000 </item>
        /// </list>
        /// </remarks>
        /// </summary>
        public Int32 EventOffset;

        public byte EventType;
        public byte EventParameter;
        public Int16 EventValue;
    }

    public static List<Chart> Parse(string file)
    {
        var stream = File.OpenRead(file);
        var reader = new BinaryReader(stream);

        var directories = new List<Directory>();

        for (var i = 0; i <= 10; i++)
        {
            directories.Add(Utils.Byte2Type<Directory>(reader));
        }

        var result = new List<Chart>();

        foreach (var d in directories)
        {
            if (d.ChartLength == 0)
            {
                result.Add(new Chart(new Dictionary<int, List<Note>>(), new List<Sample>(), new List<TimingPoint>(), new List<MeterChange>()));
                continue;
            }

            stream.Seek(d.ChartOffset, SeekOrigin.Begin);
            var events = new List<ChartEvent>();

            var location = 0;

            while (location <　d.ChartLength)
            {
                var @event = Utils.Byte2Type<ChartEvent>(reader);
                events.Add(@event);
                location += Marshal.SizeOf(typeof(ChartEvent));
            }

            var endTime = events.First(x => x.EventType == 6).EventOffset;

            var samples = events
                .Where(e => e.EventType is 2 or 3 && e.EventOffset <= endTime)
                .Select(e => ((int Offset, int Id, int Lane))(e.EventOffset, e.EventValue, e.EventParameter + 8 * (e.EventType - 2)))
                .GroupBy(x => x.Lane)
                .ToDictionary(x => x.Key, x => x
                    .Select(y => new Sample(y.Offset, 0, y.Id))
                    .OrderBy(y => y.Offset).ToList()
                );

            var notes = events
                .Where(e => e.EventType is 0 or 1 && e.EventOffset <= endTime)
                .Select(e => ((int Offset, int Druation, int Lane))(e.EventOffset, e.EventValue, e.EventParameter + 8 * e.EventType))
                .GroupBy(x => x.Lane)
                .ToDictionary(x => x.Key, x => x
                    .Select(y =>
                    {
                        var lane = x.Key;
                        var ss   = samples[lane];

                        int l = 0, r = ss.Count - 1;
                        while (l <= r)
                        {
                            var mid = (r + l) / 2;

                            if (ss[mid].Offset <= y.Offset)
                                l = mid + 1;
                            else
                                r = mid - 1;
                        }

                        return new Note(y.Offset, y.Druation, ss[l - 1]);
                    })
                    .OrderBy(y => y.Offset)
                    .ToList()
                );

            var bgm = events
                .Where(e => e.EventType is 7)
                .Select(x => new Sample(x.EventOffset, x.EventParameter, x.EventValue))
                .ToList();

            var timings = events
                .Where(e => e.EventType is 4)
                .Select(e => new TimingPoint(e.EventOffset, e.EventValue / e.EventParameter))
                .OrderBy(x => x.Offset)
                .DistinctBy(x => (x.Offset, x.Bpm))
                .ToList();

            var meterChange = events
                .Where(e => e.EventType is 5)
                .Select(e => new MeterChange(e.EventOffset, e.EventValue, e.EventParameter))
                .OrderBy(x => x.Offset)
                .DistinctBy(x => (x.Offset, x.L, x.R))
                .ToList();

            result.Add(new Chart(notes, bgm, timings, meterChange));
        }

        reader.Close();
        stream.Close();

        return result;
    }
}