using System.Collections.Generic;
using System.Linq;
using HLSProxier.Stream;

namespace HLSProxier
{
    public class Program
    {

        public static void Main(string[] args)
        {

            var sources = new List<HLSProxy>
            {
                new HLSProxy("Resources/TRT WORLD", 5, "http://trtcanlitv-lh.akamaihd.net/i/TRTWORLD_1@321783/master.m3u8"),
                new HLSProxy("Resources/TRT 1", 5, "http://trtcanlitv-lh.akamaihd.net/i/TRT1HD_1@181842/master.m3u8")

            };


            var handler = new HLSHandler(sources);

            // - Block
            handler.Run().Wait();

        }

    }
}