using System.Collections.Generic;
using System.Linq;
using HLSProxier.Stream;

namespace HLSProxier
{
    public class Program
    {

        public static void Main(string[] args)
        {

            var sources = new List<HLSProxy>();

            for (var i = 0; i < 250; i++)
            {
                sources.Add(new HLSProxy("Resources/TRT_" + i.ToString(), 5,
                    "http://trtcanlitv-lh.akamaihd.net/i/TRT1HD_1@181842/master.m3u8"));
            }


            var handler = new HLSHandler(sources);

            // - Block
            handler.Run().Wait();

        }

    }
}