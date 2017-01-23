using System.Collections.Generic;
using System.Linq;
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
                    WindowSize = 5
                }
            };

            // - ~300mbps internet speed is requied to handle this amount of streams!
            for (var i = 0; i < 250; i++)
            {
                var s = sources.First();

                // - Clone first source
                sources.Add(new HLSSource
                {
                    CachePath = s.CachePath + i.ToString(),
                    Uri = s.Uri,
                    WindowSize = 5
                });
            }


            var handler = new HLSHandler(sources);

            // - Block
            handler.Run().Wait();

        }

    }
}