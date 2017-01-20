using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace HLSProxier.Stream
{
    public struct Stream
    {
        public int Bandwidth;
        public string Uri;
    }

    public class Segment
    {
        public float Duration;
        public string Uri;
        public uint Number;
    }

    public class HLSProxy
    {
        private readonly List<Stream> Streams = new List<Stream>();
        private readonly string CacheFolder;
        private Uri Host;

        // - Schedule
        private float ScheduledTime;

        private DateTime PreviousTime;

        // - Helpers
        private uint UnhandledSegments;

        private readonly int WindowSize;
        private string Uri;

        // - Segments
        public Queue<Segment> Segments = new Queue<Segment>();

        public float TargetDuration;
        public uint MediaSequences;
        public uint AddedSegments;

        public HLSProxy(string CacheFolder, int WindowSize, string Uri)
        {
            this.CacheFolder = CacheFolder;
            this.WindowSize = WindowSize;
            this.Uri = Uri;

            this.UnhandledSegments = 0;
            this.AddedSegments = 0;

            // - Create cache folder
            if (Directory.Exists(CacheFolder))
            {
                var di = new DirectoryInfo(CacheFolder);
                foreach (var file in di.GetFiles())
                {
                    file.Delete();
                }
            }
            else
            {
                Directory.CreateDirectory(CacheFolder);
            }
        }

        public async Task Initialize()
        {
            this.Streams.Clear();

            // - Parse uri
            this.Host = new Uri(
                this.Uri.Contains('/') ? this.Uri.Substring(0, this.Uri.LastIndexOf('/') + 1) : this.Uri);

            using (var client = new HttpClient())
            using (var reader = new StringReader(await client.GetStringAsync(this.Uri)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // - If stream information
                    if (!line.Contains("#EXT-X-STREAM-INF")) continue;

                    // - Parse bandwidth portion
                    var b = line.Substring(line.IndexOf("BANDWIDTH=", StringComparison.Ordinal));

                    b = b.Contains(',')
                        ? b.Substring(0, b.IndexOf(','))
                        : b;

                    b = b.Substring(b.IndexOf('=') + 1);

                    // - Parse url from next line
                    var u = reader.ReadLine();
                    if (!u.Contains("http")) u = Host.ToString() + '/' + u;

                    // - Add all available streams to list
                    this.Streams.Add(new Stream {Bandwidth = int.Parse(b), Uri = u});
                }
            }
        }

        public async Task CollectSubsequentSegments(Stream stream, bool dump)
        {
            if ((DateTime.Now - this.PreviousTime).TotalSeconds < ScheduledTime)
            {
                this.UnhandledSegments = 0;
                return;
            }

            var start = DateTime.Now;

            var uri = stream.Uri;

            // - Temporary save to list
            var temp = new List<Segment>();

            using (var client = new HttpClient())
            using (var reader = new StringReader(await client.GetStringAsync(uri)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains("#EXT-X-TARGETDURATION"))
                    {
                        var d = int.Parse(line.Substring(line.IndexOf(":", StringComparison.Ordinal) + 1));
                        this.TargetDuration = d;
                    }
                    else if (line.Contains("#EXT-X-MEDIA-SEQUENCE"))
                    {
                        var d = uint.Parse(line.Substring(line.IndexOf(":", StringComparison.Ordinal) + 1));
                        this.UnhandledSegments = d - this.MediaSequences;
                        this.MediaSequences = d;

                        // - Return early if no fresh segments are available
                        if (this.UnhandledSegments == 0)
                        {
                            this.PreviousTime = DateTime.Now;
                            return;
                        }

                    }
                    else if (line.Contains("#EXTINF"))
                    {
                        var d = line.Substring(line.IndexOf(":", StringComparison.Ordinal) + 1);
                        if (d.Contains(',')) d = d.Substring(0, d.IndexOf(','));
                        var duration = float.Parse(d, CultureInfo.InvariantCulture);

                        var url = reader.ReadLine();
                        if (!url.Contains("http")) url = Host.ToString() + '/' + url;

                        // - Add all segments to temp
                        temp.Add(new Segment
                        {
                            Duration = duration,
                            Uri = url
                        });
                    }
                }
            }

            // - Unhandled segments can't exceed available segments
            if (this.UnhandledSegments > temp.Count()) this.UnhandledSegments = (uint) temp.Count();

            // - Get only unhandled segments
            var segments = temp.Skip(temp.Count() - (int) this.UnhandledSegments);

            // - Enque uhandled segments
            foreach (var segment in segments)
            {
                segment.Number = AddedSegments++;
                this.Segments.Enqueue(segment);

                // - Deque a segment if exceeds window size
                if (this.Segments.Count() > this.WindowSize) this.Segments.Dequeue();
            }

            // - Dump segments
            if (dump) await DumpLatestSegments();

            // - Calculate timer
            this.PreviousTime = DateTime.Now;
            var SegmentTime = this.Segments.Last().Duration;
            var LostTime = (float) (DateTime.Now - start).TotalSeconds;
            ScheduledTime = SegmentTime - LostTime;

            // - When too much time is used
            if (ScheduledTime < 0)
            {
                ScheduledTime = 0;
                Console.WriteLine("Too much time was consumed downloading and writing segment to ({0})", CacheFolder);
            }
        }

        public async Task<byte[]> GetSegmentContent(string URL)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var stream = await client.GetByteArrayAsync(URL);
                    if (stream.Length > 0) return stream;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            return null;
        }

        public async Task DumpLatestSegments()
        {
            if (this.UnhandledSegments == 0) return;

            foreach (var segment in Segments.Skip(Segments.Count() - (int) this.UnhandledSegments))
            {
                var bytes = await GetSegmentContent(segment.Uri);
                if (!bytes.Any()) return;

                using (var stream = new FileStream(CacheFolder + "/" + segment.Number + ".ts", FileMode.Create))
                {
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                }
            }
        }

        public void CleanCacheFolder()
        {
            var counter = this.Segments.Last().Number - this.WindowSize + 1;

            // - Remove all segments except those within window
            var di = new DirectoryInfo(CacheFolder);

            foreach (var file in di.GetFiles())
            {
                var segmentIndex = int.Parse(file.Name.Substring(0, file.Name.IndexOf('.')));

                if (segmentIndex < counter)
                {
                    file.Delete();
                }
            }
        }

        // ------------------------------- GETTERS
        public List<Stream> GetAllStreams()
        {
            return this.Streams;
        }
    }
}