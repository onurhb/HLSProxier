using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HLSProxier.Stream;

namespace HLSProxier
{
    public class Program
    {
        public class Stream
        {
            private readonly HLSProxy _hls;
            private readonly string uri;
            public Stream(string path, string uri){
                this._hls =  new HLSProxy(path, 10);
                this.uri = uri;
                this.Run().Wait();
            }
            public async Task Run()
            {
                await _hls.LoadIndexFile(this.uri);

            }

            public async Task Loop()
            {
                while (true)
                {
                    await _hls.CollectSubsequentSegments(_hls.GetAllStreams().OrderByDescending(x => x.Bandwidth).First(), true);
                    _hls.CleanCacheFolder();
                    await Task.Delay(100);
                }
            }
        }   

        public static void Main(string[] args)
        {
            var t1 = new Stream("Resources/TRT WORLD", 
                    "http://trtcanlitv-lh.akamaihd.net/i/TRTWORLD_1@321783/master.m3u8");
            
            var t2 = new Stream("Resources/TRT 1", "http://trtcanlitv-lh.akamaihd.net/i/TRT1HD_1@181842/master.m3u8");

            var task1 = t1.Loop();
            var task2 = t2.Loop();
            
            task1.Wait();
            task2.Wait();
            
        }

    }
}