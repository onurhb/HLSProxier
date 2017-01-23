using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HLSProxier.Stream
{

    public class HLSSource
    {
        public string Uri;
        public string CachePath;
        public int WindowSize;
    }

    public class HLSHandler
    {

        private readonly List<HLSProxy> HLSProxies;


        public HLSHandler(List<HLSProxy> HLSProxies)
        {

            this.HLSProxies = HLSProxies;

            Console.Write("Initialized ({0}) tasks", this.HLSProxies.Count());

        }

        public async Task Run()
        {
            var index = 0;
            await Task.WhenAll(HLSProxies.Select(proxy => Runner(proxy, index++, HLSProxies.Count())).ToList());
        }

        private static async Task Runner(HLSProxy proxy, int index, int total)
        {
            // - Spread tasks
            await Task.Delay((total > 100 ? 20000 : 5000) * index / total);

            await proxy.Initialize();

            while (true)
            {
                await proxy.CollectSubsequentSegments(proxy.GetAllStreams().OrderByDescending(x => x.Bandwidth).First(), true);
                proxy.CleanCacheFolder();

                // - Sleep a lil bit
                await Task.Delay(100);
            }
        }

    }
}