# Steam 原版 MUSYNX 接入实验

本机 Build `25570400` 曾完成《蜡烛人》的固定资源替换实验，入口显示为 **蜡烛人 [TEST]**。该实验现已升级为 [游戏内敲谱曲库 Mod](../../mods/ChartRecorder/README.md)：在游戏内导入、切歌和切谱。当前安装已恢复原资源，使用运行时 Mod 加载曲库，无需 MelonLoader。

测试音乐由本项目合成：120 BPM、24 秒；测试谱含 24 个单点和 4 个长条，覆盖双押、四押、长条中插入其他轨道单点、双长条。同一曲目共用一个音频库，因此该曲目的 4K/6K 简单/困难四份谱面同步替换；6K 中只使用四条轨道，本次原生运行验证针对 4K 简单。

## 验证

Unity 原生游戏副本成功从选歌进入 `UIB_PlayingScene`，加载 `lazhuren` CRI 音频库并识别为 24 秒。逐一核对了全部 28 个音符的轨道、起点和长条终点。CRI 播放状态为 `Playing`，一次运行中测得音频时钟与游戏时钟相差 15 ms；这是本次运行观测值，不代表设备延迟校准结果。音频正常达到 `PlayEnd`，游戏进入 `SettlementScene`。

自动验证没有模拟玩家键盘击打，不能代替逐键手感测试。测试副本的后台截图请求未生成可用图片，未将其作为验证证据。

原始运行日志、核对结果、可导入本项目的 JSON 和 WAV 在 `artifacts/musynx-test/`。项目测试共 20 项通过。

## 修改与恢复

以下三文件替换和恢复命令属于历史实验。当前曲库 Mod 请使用其安装包的卸载脚本；不要在 Mod 启用时使用旧实验恢复脚本。

安装仅替换以下三个文件：

- `MUSYNX_Data/resources.assets`：四份测试谱和曲目名称/结束时长；其余 3004 个资源对象的原始内容逐一校验未变。
- `MUSYNX_Data/StreamingAssets/song_lazhuren.acb`：24 秒 HCA 音乐、采样信息、Cue 时长及非循环设置。
- `MUSYNX_Data/Managed/Assembly-CSharp.dll`：在 `SongInfoCore.UpdateSync` 和 `UploadScore` 开头仅针对 `lazhuren` 返回，跳过该测试曲目的本地成绩更新与排行榜提交。没有安装自动验证探针或额外运行库。

原文件在 `artifacts/musynx-test/original/`，替换文件在 `replacement/`，SHA256 和目标安装目录在 `installation.json`。请保留这些文件。

关闭游戏后，在项目根目录的 PowerShell 中运行：

```powershell
# 查看是否安装成功
.\tools\musynx\apply-test.ps1 -Mode Verify

# 恢复原曲、原谱和原程序集
.\tools\musynx\apply-test.ps1 -Mode Restore
```

脚本会检查全部文件和备份的 SHA256 后再复制。游戏更新导致文件变化时会停止覆盖。Steam 验证文件完整性也可能撤销替换。

## 代码入口

`convert.cjs` 将本项目 JSON 转成实验用 MUSYNX 文本，保留单点、多押、长条及原曲毫秒时间，并增加 2 秒前导。`01` 不定义采样，因此音符不触发独立 Key 音；`02` 关联背景音乐 Cue。

```powershell
node tools/musynx/convert.cjs 我的谱面.json 输出谱面.txt lazhuren
```

输出 TXT 本身不会自动导入原游戏。目前这是已验证的固定曲目替换实验，尚未实现任意歌曲的一键导入。

`make-fixture.cjs`、`patch-fixture-assets.py` 和 `fixture-bank.cs` 保存了这次测试素材与资源的生成逻辑；音频库生成器目前专用于本次 24 秒、44100 Hz 双声道测试。`runtime-probe.cs`、`inject-probe.cs` 用于游戏副本验证，禁止将验证探针用于日常安装；`verify-runtime.py` 根据原生运行日志核对音符和播放结果。`guard-test-score.cs` 生成实际安装的曲目成绩保护补丁。

临时工具放在 `.tmp/musynx-research/`：UnityPy、Mono.Cecil、SonicAudioLib、VGAudioCli。第三方二进制和原游戏资源未加入源代码目录。

参考：[MUSYNX 原版 Mod](https://github.com/bnfour/mx-mods)、[MUSYNX 格式解析器](https://github.com/4accccc/Cpp_Mania_Player/blob/master/src/parsers/MuSynxParser.cpp)、[UnityPy](https://github.com/K0lb3/UnityPy)、[SonicAudioTools](https://github.com/blueskythlikesclouds/SonicAudioTools)、[VGAudio](https://github.com/Thealexbarney/VGAudio)、[Mono.Cecil](https://github.com/jbevain/cecil)。
