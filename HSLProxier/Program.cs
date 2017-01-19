using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HSLProxy
{
    public class Program
    {
        public class Test
        {
            private readonly HLSProxy _hls = new HLSProxy("Temp", 10);

            public async Task Run()
            {
                await _hls.LoadIndexFile(
                    "http://mn-i.mncdn.com/haberturk/smil:haberturk.smil/playlist.m3u8?token=12b03adfcca7404e2dbc1bd8cca83645340c5afb45c182c4");

            }

            public async Task Loop()
            {
                await _hls.CollectsSubsequentSegments(_hls.GetAllStreams().OrderByDescending(x => x.Bandwidth).First());
                await _hls.DumpLatestSegments();
                _hls.CleanCacheFolder();
            }
        }

        public static void Main(string[] args)
        {
            Test t = new Test();
            t.Run().Wait();
            while (true)
            {
                t.Loop().Wait();
                Thread.Sleep(new TimeSpan(0, 0, 0, 0, 100));
            }
        }
    }
}