# HLSProxy

HLSProxy is a HLS stream (HTTP Live Stream) proxier that mirrors a hls server. <br />
Currently, it will download all segments from the HLS server into a cache folder when it should. <br />
A proxy server will be hosted at http://localhost:5000/{Channel Name}/playlist.m3u8 <br />
This server will host the mirror HLS which can be used to play the stream using VLC or any supported media player.

## How does HLSProxy work?

HLSProxy works by caching the original segments from a HLS server, then generates new playlist (.m3u8) file <br />
This file can be used to play the stream from our server instead of the original stream (Proxy). <br />

## Can HLSProxy handle multiple HLS?

Yes, HLSProxy uses threads to handle serveral streams parallelly. I managed to download 500 streams without any problem (with 1Gbps connection) <br />
This depends on your internet connection though.

## How do I use HLSProxy?

First create a list of HLSProxy like so (see `Program.cs`): <br />

`var sources = new List<HLSProxy>
            {
                new HLSProxy("Resources/TRT WORLD", 10,
                    "http://trtcanlitv-lh.akamaihd.net/i/TRTWORLD_1@321783/master.m3u8"),
                ...
            };`


Then pass `sources` to a stream handler `var handler = new HLSHandler(sources).Run();` <br />

If you don't want the main thread to exit, simply call `Wait()` on `handler` <br />

The stream will then be available at `Resources/TRT WORLD`.  <br />
Use VLC or any supported mediaplayer to stream `http://localhost:5000/Resources/TRT WORLD/playlist.m3u8`.