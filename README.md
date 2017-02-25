# HLSProxy

HLSProxy is a HLS stream (HTTP Live Stream) proxier that mirrors a hls server. 

## How does HLSProxy work?

HLSProxy works by caching the original segments from a HLS server, then generates new playlist (.m3u8) file. <br />
Any mediaplayer with HLS support can be used to play the stream from your own server! <br />

## Can HLSProxy handle multiple HLS?

Yes, HLSProxy can handle serveral streams parallelly. This depends on your internet speed of course!

## How do I use HLSProxy?

First create a list of HLSProxy like so (see `Program.cs`): <br />

`var sources = new List<HLSProxy>
            {
                new HLSProxy("Resources/<website>", 10,
                    "http://<website>/master.m3u8"),
                ...
            };`

> Warning: If you pass window size of 0 or less, it will never delete any segments from the cache. 

Then pass `sources` to a stream handler `var handler = new HLSHandler(sources).Run();` <br />

If you don't want the main thread to exit, simply call `Wait()` on `handler` <br />

> By default, the Kestrel will prevent the main thread from exiting so you don't need to call `Wait()`

The stream will be available at `Resources/TRT WORLD` in the root folder.  <br />
Use VLC or any supported mediaplayer to stream `http://localhost:5000/Resources/<website>/playlist.m3u8`.

## The HLSProxy class 

Caches HLS segments from a single source. <br/>
The segments are automatically downloaded when a new segment is available. 

> `public HLSProxy(string CacheFolder, int WindowSize, string indexUrl)` <br/>
> CacheFolder : The folder to cache segments to <br/>
> WindowSize : Contraints cache size <br/>
> indexUrl : the full URL to the m3u8 file  <br/>

## THE HLSHandler class

Schedules HLSProxy on multiple websites. <br/>
The HLSHandler schedules jobs by adding a time padding between each. <br/>
This makes sure that all jobs don't do network request at same time (prevents network overload). <br/>

> `public HLSHandler(List<HLSProxy> HLSProxies)` <br/>
> HLSProxies : A list of HLSProxy classes <br/>


