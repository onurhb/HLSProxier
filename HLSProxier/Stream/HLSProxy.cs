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

        // - Segments
        public Queue<Segment> Segments = new Queue<Segment>();
        public float TargetDuration;
        public uint MediaSequences;
        public uint AddedSegments;

        public HLSProxy(string CacheFolder, int WindowSize)
        {
            this.CacheFolder            = CacheFolder;
            this.WindowSize             = WindowSize;
            this.UnhandledSegments      = 0;
            this.AddedSegments          = 0;

            // - Create cache folder
            if (Directory.Exists(CacheFolder))
            {
                Directory.Delete(CacheFolder, true);
            }

            Directory.CreateDirectory(CacheFolder);

        }

        public async Task LoadIndexFile(string uri)
        {
            // - Parse uri
            this.Host = new Uri(uri.Contains('/') ? uri.Substring(0, uri.LastIndexOf('/') + 1) : uri);

            using (var client = new HttpClient())
            using (var reader = new StringReader(await client.GetStringAsync(uri)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // - If stream information
                    if (line.Contains("#EXT-X-STREAM-INF"))
                    {
                        // - Parse bandwidth portion
                        var b = line.Substring(line.IndexOf("BANDWIDTH="));

                        b = b.Contains(',')
                            ? b.Substring(0, b.IndexOf(','))
                            : b;

                        b = b.Substring(b.IndexOf('=') + 1);

                        // - Parse url from next line
                        var u = reader.ReadLine();
                        if (!u.Contains("http")) u = Host.ToString() + '/' + u;

                        // - Add all available streams to list
                        this.Streams.Add(new Stream {Bandwidth = Int32.Parse(b), Uri = u});
                    }
                }
            }
        }



        public async Task CollectSubsequentSegments(Stream stream)
        {

            if ((DateTime.Now - this.PreviousTime).TotalSeconds < ScheduledTime)
            {
                this.UnhandledSegments = 0;
                return;
            }

            var start = DateTime.Now;

            var uri = stream.Uri;
            Console.WriteLine("Requesting: {0}", uri);

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
                        var d = Int32.Parse(line.Substring(line.IndexOf(":") + 1));
                        this.TargetDuration = d;

                    }else if (line.Contains("#EXT-X-MEDIA-SEQUENCE"))
                    {
                        var d = UInt32.Parse(line.Substring(line.IndexOf(":") + 1));
                        this.UnhandledSegments = d - this.MediaSequences;
                        this.MediaSequences = d;

                        // - Return early if no fresh segments are available
                        if (this.UnhandledSegments == 0)
                        {
                            this.PreviousTime = DateTime.Now;
                            return;
                        };

                    }else if (line.Contains("#EXTINF"))
                    {

                        var d = line.Substring(line.IndexOf(":") + 1);
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

            Console.WriteLine("Found ({0}) unhandled segment(s)", this.UnhandledSegments);

            // - Enque uhandled segments
            foreach (var segment in segments)
            {
                segment.Number = AddedSegments++;
                this.Segments.Enqueue(segment);
                Console.WriteLine("Requesting segment: {0}", segment.Uri);

                // - Deque a segment if exceeds window size
                if (this.Segments.Count() > this.WindowSize) this.Segments.Dequeue();
            }

            // - Calculate timer
            this.PreviousTime = DateTime.Now;
            var LostTime = (float) (DateTime.Now - start).TotalSeconds;
            ScheduledTime = this.Segments.Last().Duration - LostTime;
            Console.WriteLine("Status: Lost Time: ({0}), Scheduled Time: ({1}), Expected Time: ({2})",
                LostTime, ScheduledTime, this.Segments.Last().Duration);
        }


        public async Task<byte[]> GetSegmentContent(string URL)
        {
            using (var client = new HttpClient())
            {
                var stream = await client.GetByteArrayAsync(URL);
                if (stream.Length > 0) return stream;
            }

            return null;
        }


        public async Task DumpLatestSegments()
        {

            if (this.UnhandledSegments == 0) return;


            foreach (var segment in Segments.Reverse().Take((int)this.UnhandledSegments).Reverse())
            {
                Console.WriteLine("Dumping segment ({0}) to folder ({1})", segment.Number,
                    CacheFolder);

                var bytes = await GetSegmentContent(segment.Uri);

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
                var segmentIndex = Int32.Parse(file.Name.Substring(0, file.Name.IndexOf('.')));

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