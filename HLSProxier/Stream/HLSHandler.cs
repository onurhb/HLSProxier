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
        public readonly List<Task> TaskList = new List<Task>();


        public HLSHandler(IEnumerable<HLSSource> HLSSources){
            this.HLSProxies = new List<HLSProxy>();

            foreach (var source in HLSSources)
            {
                Console.WriteLine("Initializing ({0})", source.CachePath);

                HLSProxies.Add(new HLSProxy(source.CachePath, source.WindowSize, source.Uri));
            }
        }

        public async Task Run()
        {

            var index = 0;
            foreach (var proxy in HLSProxies)
            {
                TaskList.Add(Runner(proxy, index++, HLSProxies.Count()));
            }

            await Task.WhenAll(TaskList.ToArray());
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