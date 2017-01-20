# HLSProxy
*Work in progress* <br />
HLSProxy is a HLS stream (HTTP Live Stream) proxier that mirrors a hls server. <br />
Currently, it will download all segments from the HLS server into a cache folder when it should. <br />
A proxy server will be hosted at http://localhost:8080/.../.../playlist.m3u8 <br />
This server will host the mirror HLS which can be used to play the stream using VLC or any supported media player.

## How does HLSProxy work?

HLSProxy works by caching the original segments from a HLS server, then generates new playlist (.m3u8) file <br />
This file can be used to play the stream from our server instead of the original stream (Proxy). <br />