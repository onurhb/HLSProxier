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
            private readonly HSLProxy HSL = new HSLProxy("Temp");


            public async Task Run()
            {
                await HSL.LoadEntryFile("http://trtcanlitv-lh.akamaihd.net/i/TRT1HD_1@181842/master.m3u8");
                await HSL.CollectAllSegments(HSL.GetStreams().Last());
                HSL.DumpCurrentSegments();
            }

            public async Task Loop()
            {
                await HSL.CollectNextSegment(HSL.GetStreams().Last());
                HSL.DumpLastSegmentIfAvailable();
            }
        }

        public static void Main(string[] args)
        {
            Test t = new Test();
            t.Run().Wait();
            while (true)
            {
                t.Loop().Wait();
                Thread.Sleep(new TimeSpan(0,0,0,1));
            }

        }
    }
}
