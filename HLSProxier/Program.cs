using System.Collections.Generic;
using HLSProxier.Stream;

namespace HLSProxier
{
    public class Program
    {

        public static void Main(string[] args)
        {

            var sources = new List<HLSSource>
            {
                new HLSSource
                {
                    CachePath = "Resources/TRT WORLD",
                    Uri = "http://trtcanlitv-lh.akamaihd.net/i/TRTWORLD_1@321783/master.m3u8",
                    WindowSize = 10
                },
                new HLSSource
                {
                    CachePath = "Resources/TRT 1",
                    Uri = "http://trtcanlitv-lh.akamaihd.net/i/TRT1HD_1@181842/master.m3u8",
                    WindowSize = 10
                }
                ,
                new HLSSource
                {
                    CachePath = "Resources/TRT HABER",
                    Uri = "http://trtcanlitv-lh.akamaihd.net/i/TRTHABERHD_1@181942/master.m3u8",
                    WindowSize = 10
                }
            };


            var handler = new HLSHandler(sources);

            // - Block
            handler.Run().Wait();

        }

    }
}