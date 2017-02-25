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
        public readonly string CacheFolder;    // - Folder where segments are cached
        private readonly string StreamFile;

        // - Schedule
        private float ScheduledTime;
        private DateTime PreviousTime;

        private uint UnhandledSegments;    // - Unprocessed segments

        public readonly int WindowSize;    // - Window
        private readonly string IndexURL;  // - Full URL to the first file (master.m3u8)
        private string HostURL;            // - URL to the server hosting the segments and files

        // - Segments
        private Queue<Segment> Segments = new Queue<Segment>();

        private float TargetDuration;    // - Maximum segment duration
        public uint MediaSequences;    // - How many media segments is currently available at the target source
        public uint AddedSegments;    // - How many segments collected so far

        public HLSProxy(string CacheFolder, int WindowSize, string indexUrl)
        {
            this.CacheFolder = CacheFolder;
            this.WindowSize = WindowSize;
            this.StreamFile = "playlist.m3u8";
            this.IndexURL = indexUrl;

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
            else Directory.CreateDirectory(CacheFolder);
        }

        public async Task<bool> Initialize()
        {
            this.Streams.Clear();

            // - Parse uri
            this.HostURL =
                this.IndexURL.Contains('/') ? this.IndexURL.Substring(0, this.IndexURL.LastIndexOf('/') + 1) : this.IndexURL;


            var valid = false;

            using (var reader = new StringReader(await GetFileStringAsync(this.IndexURL)))
            {
                if (reader.Peek() < 0) return false;

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // - If stream information
                    if (!line.Contains("#EXT-X-STREAM-INF")) continue;

                    // - Parse bandwidth portion
                    var b = line.Substring(line.IndexOf("BANDWIDTH=", StringComparison.Ordinal));

                    b = b.Contains(',') ? b.Substring(0, b.IndexOf(',')) : b;

                    b = b.Substring(b.IndexOf('=') + 1);

                    // - Parse url from next line
                    var u = reader.ReadLine();
                    if (!u.Contains("http")) u = HostURL.ToString() + '/' + u;

                    // - Add all available streams to list
                    this.Streams.Add(new Stream {Bandwidth = int.Parse(b), Uri = u});

                    valid = true;

                }
            }

            return valid;
        }

        public async Task CollectSubsequentSegments(Stream stream, bool dump)
        {
            if ((DateTime.Now - this.PreviousTime).TotalSeconds < ScheduledTime)
            {
                this.UnhandledSegments = 0;
                return;
            }

            var start = DateTime.Now;

            // - Temporary save to list
            var temp = new List<Segment>();

            using (var reader = new StringReader(await GetFileStringAsync(stream.Uri)))
            {

                if (reader.Peek() < 0) return;

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

                        if (this.UnhandledSegments != 0) continue;

                        // - Return early if no fresh segments are available
                        this.PreviousTime = DateTime.Now;
                        return;
                    }
                    else if (line.Contains("#EXTINF"))
                    {
                        var d = line.Substring(line.IndexOf(":", StringComparison.Ordinal) + 1);
                        if (d.Contains(',')) d = d.Substring(0, d.IndexOf(','));
                        var duration = float.Parse(d, CultureInfo.InvariantCulture);

                        var url = reader.ReadLine();
                        if (!url.Contains("http")) url = HostURL + '/' + url;

                        // - Add all segments to temp
                        temp.Add(new Segment{ Duration = duration, Uri = url});


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

                // - Deque a segment if exceeds window size, if not 0 or less 
                if (this.Segments.Count() > this.WindowSize && this.WindowSize > 0) this.Segments.Dequeue();
            }

            // - Dump segments
            if (dump) await DumpLatestSegmentsAsync();

            // - Calculate timer
            this.PreviousTime = DateTime.Now;
            var SegmentTime = this.Segments.Last().Duration;
            var LostTime = (float) (DateTime.Now - start).TotalSeconds;
            ScheduledTime = SegmentTime - LostTime;

            // - When too much time is used
            if (ScheduledTime < 0)
            {
                ScheduledTime = 0;
                Console.WriteLine("Warning: thread is throttling ({0})", CacheFolder);
            }
        }

        public void DumpStreamFile()
        {

            using(var file = new FileStream(CacheFolder + "/" + StreamFile, FileMode.Create))
            using(var stream = new StreamWriter(file))
            {
                stream.WriteLine("#EXTM3U");
                stream.WriteLine("#EXT-X-TARGETDURATION:" + TargetDuration);
                stream.WriteLine("#EXT-X-ALLOW-CACHE:YES");
                stream.WriteLine("#EXT-X-VERSION:3");
                stream.WriteLine("#EXT-X-MEDIA-SEQUENCE:" + AddedSegments);

                foreach (var segment in Segments)
                {
                    stream.WriteLine("#EXTINF:" + segment.Duration.ToString("0.00", CultureInfo.InvariantCulture));
                    stream.WriteLine(segment.Number + ".ts");
                }

            }
        }

        private async Task<byte[]> GetSegmentContentAsync(string URL)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetByteArrayAsync(URL);
                    if (response.Length > 0) return response;
                }
                catch
                {
                    Console.WriteLine("Error: Failed requesting segment {0} for ({1})", AddedSegments + 1, CacheFolder);
                }
            }

            return null;
        }

        private async Task<string> GetFileStringAsync(string URL)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetStringAsync(URL);
                    if (response.Length > 0) return response;
                }
                catch
                {
                    Console.WriteLine("Error: Failed requesting ({0}) for ({1})", URL, CacheFolder);
                }
            }

            return "";
        }

        public async Task DumpLatestSegmentsAsync()
        {
            if (this.UnhandledSegments == 0) return;

            foreach (var segment in Segments.Skip(Segments.Count() - (int) this.UnhandledSegments))
            {
                var bytes = await GetSegmentContentAsync(segment.Uri);
                if (!bytes.Any()) return;

                using (var stream = new FileStream(CacheFolder + "/" + segment.Number + ".ts", FileMode.Create))
                {
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                }
            }
        }

        public void CleanCacheFolder()
        {

            if(this.WindowSize < 1) return;

            var counter = this.Segments.Last().Number - this.WindowSize + 1;

            // - Remove all segments except those within window
            var di = new DirectoryInfo(CacheFolder);

            foreach (var file in di.GetFiles())
            {
                if(file.Name.Substring(file.Name.IndexOf('.')) != ".ts") continue;

                var segmentIndex = int.Parse(file.Name.Substring(0, file.Name.IndexOf('.')));

                if (segmentIndex < counter)
                {
                   try
                   {
                        file.Delete();
                   }
                   catch
                   {
                      Console.WriteLine("Failed to delete a segment at ({0})", CacheFolder);
                   }
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