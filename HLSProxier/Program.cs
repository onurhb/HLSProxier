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
                new HLSProxy("Resources/SHOW_TV", 0,
                    "http://mn-i.mncdn.com/showtv_ios/smil:showtv.smil/playlist.m3u8?token=2c6b92c0c9d3d1b72f62d3784923bd796cfb27295723b0c3")
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