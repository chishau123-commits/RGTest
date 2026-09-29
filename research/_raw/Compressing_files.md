osu-md osu-md--wiki'>Compressing files

Each beatmap has a file size limit dictated by its total length, and any video and audio content must meet format, resolution, and bit rate requirements.

This guide will help you get your beatmap under that limit and meet such requirements.

## Introduction

There are 2 types of compression, **lossless** and **lossy**:

-  **Lossless** compression implies that the quality never degrades and can thus be repeatedly compressed and decompressed
-  **Lossy** compression uses certain powerful techniques to greatly reduce file size at the expense of quality
The process of converting between audio and video formats, to reduce file size, average bit rate, or resolution, is called **re-encoding** or **transcoding**. Re-encoding an already lossy-compressed audio or video using lossy compression can result in varying degrees of further quality reduction, depending on the settings used.

Due to that reason, re-encoding should be avoided, except if the original audio or video file is any of the following:

-  Too large in file size
-  Too high of a resolution or average bit rate
-  Encoded in an incompatible format
In case re-encoding is necessary, it is suggested to use the highest-quality source file available, i.e. with the highest resolution and/or bit rate.

## Video

**osu! supports video encoded in the H.264 format with the `.mp4` file extension.** Other formats, such as H.265, VP9, and AV1, and file extensions such as `.mkv` and `.mov`, are currently not supported.

**The ranking criteria specify a maximum video resolution of 1280x720 pixels.**

### Using Handbrake

To begin, download and install Handbrake, then follow these steps:

Open Handbrake, then import your video file by either:

  -  Drag-and-dropping the file into Handbrake, or
  -  Clicking the `File` option, then selecting the file to import.
![](https://i.ppy.sh/f2b6b692060776d58bc19714ca25da095fd28e98/68747470733a2f2f6f73752e7070792e73682f77696b692f696d616765732f4775696465732f436f6d7072657373696e675f66696c65732f696d672f696d706f72742d68616e646272616b652e706e67)*Importing the video into Handbrake*

1. Select the `Fast 720p30` preset.
![](https://i.ppy.sh/88a9c3ec38c5ea6b6765f2815617a69efe4027e6/68747470733a2f2f6f73752e7070792e73682f77696b692f696d616765732f4775696465732f436f6d7072657373696e675f66696c65732f696d672f7072657365742d68616e646272616b652e706e67)*Selecting the preset*

1. Select the `Audio` tab and remove all audio tracks. Do the same for any subtitles by going into the `Subtitles` tab and removing all entries.
![](https://i.ppy.sh/bceb03e72de2dceebccebe0c7d4498d2c4fd7a77/68747470733a2f2f6f73752e7070792e73682f77696b692f696d616765732f4775696465732f436f6d7072657373696e675f66696c65732f696d672f72656d6f7665617564696f2d68616e646272616b652e706e67)*Removing the audio tracks*

Go into the `Video` tab and use the following settings:

  -  `Video Encoder` set to `H.264 (x264)` to encode in the H.264 format using the x264 encoder
  -  `Framerate (FPS)` set to `Same as source` with `Constant Framerate` selected
  -  `Constant Quality` set to a value between 20 to 25. Smaller value will result in larger, higher quality files
Depending on how long you are willing to spend time encoding, change the `Encoder Preset` under `Encoder Options` (`Veryslow` is recommended). Slower presets result in better video quality and may also reduce video file size.

  -  Do not use the `Placebo` preset, as it takes much longer to encode than `Veryslow` for very little improvement in quality or file size.
![](https://i.ppy.sh/834d99f7743b6c5599f4ac3a2331416729327a7e/68747470733a2f2f6f73752e7070792e73682f77696b692f696d616765732f4775696465732f436f6d7072657373696e675f66696c65732f696d672f636f6465637175616c6974792d68616e646272616b652e706e67)*Setting the video codec and constant quality*

1. To resize the image of the video file, go to the `Dimensions` tab and change the width to `1280` and the height to `720`.
![](https://i.ppy.sh/a45deca0920e0fe438732899a7f08285976414da/68747470733a2f2f6f73752e7070792e73682f77696b692f696d616765732f4775696465732f436f6d7072657373696e675f66696c65732f696d672f64696d656e73696f6e732d68616e646272616b652e706e67)*Setting the video dimensions*

1. Lastly, pick the location you want to save your result to, then click `Start Encode`.
![](https://i.ppy.sh/0d9f5b531334e216abe688772514d655ff17b06f/68747470733a2f2f6f73752e7070792e73682f77696b692f696d616765732f4775696465732f436f6d7072657373696e675f66696c65732f696d672f736176652d68616e646272616b652e706e67)*Encoding and saving the video*

### Using FFmpeg

FFmpeg is a program used through a command-line interface (CLI), meaning it does not have any graphical interface by itself. While this may seem intimidating, FFmpeg can offer more flexibility than other tools, such as when integrated into a script.

To install FFmpeg on Windows, download FFmpeg and add its directory to your `PATH` environment variable. On macOS, you can alternatively install it using the brew package manager. On Linux, most Linux distributions either already provide or pre-install FFmpeg by default (if not, research about the distribution you use for more information).

To use FFmpeg to re-encode a video file, open a terminal and paste in the following command, changing the values as needed:

```
ffmpeg -i input -c:v libx264 -crf 20 -preset veryslow -vf scale=-1:720 -an -sn -map_metadata -1 -map_chapters -1 output.mp4
```
-  `-i input`: Your source file. If the file name contains spaces, wrap it in double quotes (`"`)
-  `-c:v libx264`: Specify that the video should be encoded using the x264 encoder, producing video in the H.264 format
-  `-crf 20`: The compression quality, where lower values give better quality at the expense of larger files and vice versa. The recommended range is around 20-25
-  `-preset veryslow`: Specify an encoding preset, with recommended values ranging from `ultrafast` to `veryslow`. Slower presets allow the encoder to give you higher quality for the same bit rate, or lower bit rate for the same quality. More information about available presets can be found on FFmpeg's official website
-  `-vf scale=-1:720`: Downscale the video to a height of 720 pixels. The `-1` lets FFmpeg automatically determine the width of the new video based on the aspect ratio of the source
-  `-an -sn`: Remove audio and subtitles if present
-  `-map_metadata -1 -map_chapters -1`: Remove metadata and chapters if present
-  `output.mp4`: Your output file. If the file name contains spaces, wrap it around double quotes (`"`)
## Audio

**Audio encoded in either MP3 or OGG (Vorbis) formats is supported with `.mp3` and `.ogg` file extensions, respectively.** Other formats are currently not supported (except for audio with the `.wav` file extension for hitsounds).

Generally, OGG (Vorbis) results in better quality than MP3 for a given bit rate.

**The ranking criteria specifies that average bit rate must be between 192kbps and 128kbps for MP3 format, and between 208kbps and 128kbps for OGG (Vorbis) format.** As a reference, Featured Artists songs included in the beatmap templates are encoded to MP3 with a constant bit rate of 192kbps.

### Using Audacity

**See also:** Audio editing guide

To begin, download and install Audacity, then follow these steps:

1. Open Audacity, then import the audio file into Audacity.
![](https://i.ppy.sh/5e075a0483857a50502a5f09261efd5a1c2f28e7/68747470733a2f2f6f73752e7070792e73682f77696b692f696d616765732f4775696465732f436f6d7072657373696e675f66696c65732f696d672f696d706f72742d61756461636974792e706e67)*Importing audio into Audacity*

1. Export the audio as either MP3 or OGG.
![](https://i.ppy.sh/592d3dc8f2dffcb6003996ab8ddaa2553dad18c9/68747470733a2f2f6f73752e7070792e73682f77696b692f696d616765732f4775696465732f436f6d7072657373696e675f66696c65732f696d672f6578706f72746d656e752d61756461636974792e706e67)*Export as MP3*

Change the export options to compress your file, depending on selected format:

  -  For MP3, change the bit rate mode to `Constant` and select the quality of `192 kbps`
  -  For OGG (Vorbis), adjust the `Quality` slider to `6`, which sets the average bit rate to 192 kbps
1. Select the output location and click `Save`, and a new dialog will appear for you to enter audio metadata.
![](https://i.ppy.sh/75b4df45da4dc26e7d2b3828625a2a5df83fce32/68747470733a2f2f6f73752e7070792e73682f77696b692f696d616765732f4775696465732f436f6d7072657373696e675f66696c65732f696d672f6578706f727473657474696e67732d61756461636974792e706e67)*Export settings*

1. Once done entering metadata, which can be left blank if desired, click `OK` to start re-encoding.
**Notice** Clicking `Cancel` in the metadata dialog will abort the re-encoding process.

### Using FFmpeg

**Note:** For instructions on installing FFmpeg, see Video/Using FFmpeg

After installing FFmpeg, open a terminal, then use one of the below commands.

To encode in the MP3 format, paste the following command into your terminal and change these values as needed:

```
ffmpeg -i input -c:a libmp3lame -b:a 192k -vn -sn -map_metadata -1 -map_chapters -1 output.mp3
```
-  `-i input`: Your source file. If the file name contains spaces, wrap it in double quotes (`"`)
-  `-c:a libmp3lame`: Specify that the audio should be encoded using the LAME MP3 encoder
-  `-b:a 192k`: Set the bit rate to a constant 192kbps. If you want variable bit rate, you would instead use for instance `-q:a 2` for average 192 kbps (the lower number means higher bit rate)
-  `-vn -sn`: Remove video and subtitles if present
-  `-map_metadata -1 -map_chapters -1`: Remove metadata and chapters if present
-  `output.mp3`: Your output file. If the file name contains spaces, wrap it in double quotes (`"`)
To encode in the OGG (Vorbis) format, paste the following command into your terminal and change these values as needed:

```
ffmpeg -i input -c:a libvorbis -q:a 6 -vn -sn -map_metadata -1 -map_chapters -1 output.ogg
```
-  `-i input`: Your source file. If the file name contains spaces, wrap it in double quotes (`"`)
-  `-c:a libvorbis`: Specify that the audio should be encoded using the libvorbis encoder
-  `-q:a 6`: Use the same variable bit rate range as in the Audacity example (where a higher number means higher bit rate). If you want constant bit rate, you would instead use for instance `-b:a 192k` for a constant 192kbps bit rate
-  `-vn -sn`: Remove video and subtitles if present
-  `-map_metadata -1 -map_chapters -1`: Remove metadata and chapters if present
-  `output.ogg`: Your output file. If the file name contains spaces, wrap it in double quotes (`"`)
## Verification

It is recommended to check the technical information of re-encoded audio and video files to confirm that they meet your expectations.

### Using MediaInfo

MediaInfo is very easy to use. After installing, open the file with MediaInfo and the technical information about that file will appear.

1. Right-click any file and select MediaInfo from the context menu, or use `File` -> `Open` -> `Open file(s)...` in MediaInfo.
1. Change the view from `Basic` to either `Tree`, `Text`, or `HTML`. The default `Basic` view only displays a condensed series of information.
Relevant fields for video files:

-  `Format` and `Format/Info`, which must be `AVC` and `Advanced Video Codec`, respectively
-  `Width`, which must be at or below `1280 pixels`
-  `Height`, which must be at or below `720 pixels`
-  `Frame rate mode`, which must be `Constant`
Relevant fields for audio files:

-  `Overall bitrate`, which must be between `192kbps` and `128kbps`, as specified in the ranking criteria
For MP3, make sure to look for:

  -  `Format`, which must be `MPEG Audio`
  -  `Format profile` which must be `Version 1`
  -  `Format settings` which must be `Layer 3`
For OGG (Vorbis), make sure to look for:

  -  `Format`, which must be both `OGG` and `Vorbis`
If everything seems correct and the file size is small enough, then you can put either re-encoded audio or video file into your beatmap.

<!-- Background of PhotoSwipe.

It's a separate element as animating opacity is faster than rgba(). -->

<!-- Container that holds slides.

PhotoSwipe keeps only 3 of them in the DOM to save memory.

Don't modify these 3 pswp__item elements, data is added later on. -->

