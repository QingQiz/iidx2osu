# iidx to osu

这是一个可以将 iidx hdd 中的内容转换成为 osu!mania 谱面的工具

## 使用方法


### 运行前


1. 修改 `Program.cs` 中的 `Root` 为 hdd 的根目录，该目录的结构为

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
2. 修改 `Converter/OsuConverter.cs` 中的 `ResultPath` 为你想要将输出保存到的文件夹
3. 使用 [bemaniutils](https://github.com/DragonMinded/bemaniutils) 将 `path/to/hdd/contents/data/sound` 下的 `.ifs` 文件进行解包

    ```bash
    cd path/to/hdd/contents/data/sound
    for i in `ls *.ifs`; do
        path/to/bemaniutils/ifsutils $i -d .
    done
    ```

### 运行中会发生什么？

1. 首先程序读取 `music_data.bin` 文件获取所有歌曲的信息
2. 由于 `music_data.bin` 中的歌名部分的某些西文字母会被问号（`0x3f`）替换，因此读取 `video_music_list.xml` 获取歌曲的完整名称
3. 过滤有多个采样集（`.2dx`, `.s3p`）的歌曲（这是由于我没有找到难度和采样集的对应关系）
4. 对于每个歌曲（这个过程是并行的）
    1. 解析谱面（`.1`）文件
    2. 获取所有谱面采样的交集
    3. 交集生成主音频文件（`.mp3`）
    4. 将主音频文件和采样的补集复制到输出文件夹
    5. 生成 `.osu` 文件（包含 7k, 8k, 16k 的谱面）

注意：生成过程中的中间文件同样会作为缓存保存到输出文件夹中，因此可以放心的终止/重新运行程序

### 运行后

你可以在 `ResultPath` 中找到所有的谱面文件，以及所有的音频文件。其中 `ResultPath/osu` 是生成的所有 osu!mania 谱面文件，你可以将这个文件夹中的所有文件见复制到 `osu!/Songs` 目录中，并在游戏中使用 `F5` 刷新歌曲列表
