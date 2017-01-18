using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.Kestrel.Filter.Internal;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HSLProxy
{
    // - Entry stream file
    public struct Stream
    {
        public int Bandwidth;
        public string Uri;
    }

    // - Actual stream file containing segments
    public struct Segment
    {
        public string Uri;
        public float Duration;
        public byte[] Content;
    }

    public class HSLProxy
    {
        // - Member variables
        private readonly Queue<Segment> SegmentsQueue; // - Window of segments
        private readonly List<Stream> Streams; // - List of available streams of different resolution
        private readonly string CacheFolder; // - A folder where .ts file are dumped to
        private uint SegmentCount = 1; // - Counter for current installed segment
        private Uri Host; // - Host URL
        private float WaitTime; // - How long to wait to download next segment
        private DateTime LastTime; // - When last segment was downloaded
        private float SegmentDuration; // - Duration of each segments
        private bool NewSegmentAvailable = false; // - If we got a new segment

        public HSLProxy(string cacheFolder)
        {
            // - Initialize variables
            this.Streams = new List<Stream>();
            this.SegmentsQueue = new Queue<Segment>();
            this.CacheFolder = cacheFolder;

            // - Create root folder
            Directory.CreateDirectory(cacheFolder);
        }

        public async Task LoadEntryFile(string URL)
        {
            this.Host = URL.Contains('/') ? new Uri(URL.Substring(0, URL.LastIndexOf('/') + 1)) : new Uri(URL);

            using (var client = new HttpClient())
            {
                Console.WriteLine("Requesting: " + URL);

                var response = await client.GetStringAsync(URL);
                using (var reader = new StringReader(response))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Contains("#EXT-X-STREAM-INF"))
                        {
                            var bandwidth = line.Substring(line.IndexOf("BANDWIDTH="));

                            bandwidth = bandwidth.IndexOf(',') > -1
                                ? bandwidth.Substring(0, bandwidth.IndexOf(','))
                                : bandwidth;

                            bandwidth = bandwidth.Substring(bandwidth.IndexOf('=') + 1);

                            var url = reader.ReadLine();

                            if (!url.Contains("http")) url = Host.ToString() + '/' + url;

                            this.Streams.Add(new Stream {Bandwidth = Int32.Parse(bandwidth), Uri = url});
                        }
                    }
                }
            }
        }

        public async Task CollectAllSegments(Stream stream)
        {
            Console.WriteLine("Requesting: " + stream.Uri);

            using (var client = new HttpClient())
            {
                var response = await client.GetStringAsync(stream.Uri);

                using (var reader = new StringReader(response))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Contains("#EXTINF:"))
                        {
                            var url = reader.ReadLine();
                            if (!url.Contains("http")) url = Host.ToString() + '/' + url;
                            var duration = line.Substring(line.IndexOf(':') + 1);
                            if (duration.Contains(',')) duration = duration.Substring(0, duration.IndexOf(','));
                            var segment = await GetSegmentContent(url);

                            SegmentsQueue.Enqueue(new Segment
                            {
                                Duration = float.Parse(duration, CultureInfo.InvariantCulture),
                                Content = segment,
                                Uri = url
                            });
                        }
                    }
                }

            }
            Console.WriteLine("Collected ({0}) segments", SegmentsQueue.Count);
            WaitTime = SegmentDuration = SegmentsQueue.Last().Duration;
            SegmentCount = (uint) SegmentsQueue.Count;
            LastTime = DateTime.Now;
        }

        public async Task CollectNextSegment(Stream stream)
        {
            // - Check if should get next segment
            if ((DateTime.Now - LastTime).TotalSeconds >= WaitTime)
            {
                // - Retrieve async segment
                using (var client = new HttpClient())
                {
                    var start = DateTime.Now;

                    var response = await client.GetStringAsync(stream.Uri);

                    var lines = response.Split(new string[] {"\r\n", "\n"}, StringSplitOptions.None);

                    var url = String.IsNullOrEmpty(lines.Last()) ? lines[lines.Count() - 2] : lines.Last();

                    var duration = String.IsNullOrEmpty(lines.Last()) ? lines[lines.Count() - 3] : lines[lines.Count() - 2];
                    duration = duration.Substring(duration.IndexOf(':') + 1);
                    if (duration.Contains(',')) duration = duration.Substring(0, duration.IndexOf(','));

                    Console.WriteLine("Retrieving next segment ({0}) from {1}", ++SegmentCount, url);

                    if (!String.IsNullOrEmpty(url))
                    {
                        var segment = await GetSegmentContent(url);

                        SegmentsQueue.Dequeue();

                        SegmentsQueue.Enqueue(new Segment
                        {
                            Duration = float.Parse(duration, CultureInfo.InvariantCulture),
                            Content = segment,
                            Uri = url
                        });

                    }

                    // - Calculate time
                    LastTime = DateTime.Now;
                    WaitTime = float.Parse(duration, CultureInfo.InvariantCulture) - (float) (DateTime.Now - start).TotalSeconds;
                    NewSegmentAvailable = true;
                }

            }
            else NewSegmentAvailable = false;

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

        public void DumpCurrentSegments()
        {
            var counter = SegmentCount - SegmentsQueue.Count + 1;

            foreach (var segment in SegmentsQueue)
            {
                Console.WriteLine("Dumping segment ({0}) of ({1}) to folder ({2})", counter, SegmentsQueue.Count,
                    CacheFolder);

                using (var stream = new FileStream(CacheFolder + "/" + ++counter + ".ts", FileMode.Create))
                {
                    stream.Write(segment.Content, 0, segment.Content.Length);
                }
            }
        }

        public void DumpLastSegmentIfAvailable()
        {
            if (!NewSegmentAvailable) return;

            Console.WriteLine("Dumping segment ({0}) to folder ({1})", SegmentCount, CacheFolder);

            var segment = SegmentsQueue.Last();

            using (var stream = new FileStream(CacheFolder + "/" + SegmentCount + ".ts", FileMode.Create))
            {
                stream.Write(segment.Content, 0, segment.Content.Length);
            }
        }

        public List<Stream> GetStreams()
        {
            return Streams;
        }
    }
}