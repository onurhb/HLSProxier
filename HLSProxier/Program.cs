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
                new HLSProxy("Resources/TRT_WORLD", 10,
                    "http://trtcanlitv-lh.akamaihd.net/i/TRTWORLD_1@321783/master.m3u8"),
                new HLSProxy("Resources/A_Haber", 10,
                    "http://trkvz-live.ercdn.net/ahaberhd/ahaberhd.m3u8?st=pg-WY98uZ1h4H4UEaNwTPA&e=1485224276")
            };



            var handler = new HLSHandler(sources);

            // - Block
            handler.Run().Wait();

        }

    }
}