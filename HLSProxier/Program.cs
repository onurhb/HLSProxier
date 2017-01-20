using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HLSProxier.Stream;

namespace HLSProxier
{
    public class Program
    {
        public class Test
        {
            private readonly HLSProxy _hls = new HLSProxy("Temp", 10);

            public async Task Run()
            {
                await _hls.LoadIndexFile(
                    "http://trtcanlitv-lh.akamaihd.net/i/TRTWORLD_1@321783/master.m3u8");

            }

            public async Task Loop()
            {
                await _hls.CollectSubsequentSegments(_hls.GetAllStreams().OrderByDescending(x => x.Bandwidth).First());
                await _hls.DumpLatestSegments();
                _hls.CleanCacheFolder();
            }
        }

        public static void Main(string[] args)
        {
            var t = new Test();
            t.Run().Wait();
            while (true)
            {
                t.Loop().Wait();
                Thread.Sleep(new TimeSpan(0, 0, 0, 0, 100));
            }
        }
    }
}