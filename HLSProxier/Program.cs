using System.Collections.Generic;
using System.IO;
using HLSProxier.Stream;
using Microsoft.AspNetCore.Hosting;

namespace HLSProxier
{
    public class Program
    {

        public static void Main(string[] args)
        {

            var sources = new List<HLSProxy>
            {
                new HLSProxy("Resources/TRT WORLD", 10,
                    "http://trtcanlitv-lh.akamaihd.net/i/TRTWORLD_1@321783/master.m3u8"),
                new HLSProxy("Resources/A Haber", 10,
                    "http://trkvz-live.ercdn.net/ahaberhd/ahaberhd.m3u8?st=pg-WY98uZ1h4H4UEaNwTPA&e=1485224276")
            };



            var handler = new HLSHandler(sources).Run();

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            host.Run();

        }

    }
}