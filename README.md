# iidx to osu

[中文](./README.zh.md)

This is a tool that can convert the content in iidx hdd into osu!mania beatmaps.

## How to use


### Before running


1. Modify `Root` in `Program.cs` to the root directory of the hdd. The directory structure should be as follows:

    ```md
    - content
        - data
            - font
            - graphic
            - info
            - movie
            - qc
            - sound
        - dev
        - modules
        - prop
    ```
2. Modify `ResultPath` in `Converter/OsuConverter.cs` to the folder where you want to save the output.
3. Use [bemaniutils](https://github.com/DragonMinded/bemaniutils) to unpack the `.ifs` files in `path/to/hdd/contents/data/sound`.

    ```bash
    cd path/to/hdd/contents/data/sound
    for i in `ls *.ifs`; do
        path/to/bemaniutils/ifsutils $i -d .
    done
    ```

### What happens during the process?

1. First, the program reads the `music_data.bin` file to get information about all the songs.
2. Since some of the letters in the song names in `music_data.bin` are replaced with question marks (`0x3f`), the program reads `video_music_list.xml` to get the complete names of the songs.
3. Songs with multiple sample sets (`.2dx`, `.s3p`) are filtered out (this is because I couldn't find the mapping between difficulty and sample set).
4. For each song (this process is parallelized):
    1. Parse the beatmap (`.1`) file.
    2. Get the intersection of all the beatmap samples.
    3. Generate the main audio file (`.mp3`) from the intersection.
    4. Copy the main audio file and the complement of the samples to the output folder.
    5. Generate `.osu` files (including 7k, 8k, and 16k beatmaps).

Note: Intermediate files generated during the process are also saved as cache in the output folder, so you can safely terminate/re-run the program.

### After running

You can find all the beatmap files and audio files in `ResultPath`. The `ResultPath/osu` folder contains all the generated osu!mania beatmap files. You can copy all the files in this folder to the `osu!/Songs` directory and use `F5` in the game to refresh the song list.