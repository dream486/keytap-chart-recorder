# KeyTap · 敲谱与 MUSYNX 本地制谱工具

听着歌曲按 **D / F / J / K**，把你的敲击录成四键谱面，再通过波形时间轴精修。项目包含浏览器敲谱器、音游白模预览，以及 Steam 原版 MUSYNX 的本地曲库 Mod。

这是一个用于学习、制谱和玩法可行性测试的原型。制谱是主要入口，浏览器白模用于快速试听和验证。

## 快速开始

```bash
git clone https://github.com/dream486/keytap-chart-recorder.git
cd keytap-chart-recorder
```

- **无需安装依赖**：直接用 Edge / Chrome 打开 `index.html`。
- **本地服务**：安装 Node.js 22 或更新版本，运行 `npm start`，打开 `http://127.0.0.1:4173`。
- **Windows 快捷启动**：双击 `start.bat`；没有 Node.js 时会退回直接打开 HTML。

所有歌曲导入、录制、精修和草稿保存都在本机完成。仓库不包含第三方歌曲音频；四张练习谱使用本地合成音乐，可以直接体验。

## 听歌敲谱

1. 点击 **听歌打谱**，导入自己的本地歌曲。
2. 设置 BPM、首拍位置与录制区间，选择追加或覆盖。
3. 按需要选择 **0.25× / 0.50× / 0.75× / 1.00×** 慢放，以及 **1/8、1/16、1/32** 网格吸附。
4. 播放歌曲并敲 D / F / J / K：短按生成单点，同时按生成多押，持续按住生成长条。
5. 在波形时间轴上拖动音符、调整长条终点，或输入毫秒时间精修；支持撤销／重做。
6. **游玩测试本段** 可预览所选区间；**导出 JSON** 保存整张谱面。

慢放录制仍保存为原曲时间。自选音乐不打包进 JSON，分享或迁移时请同时保留对应音频。草稿保存在浏览器 localStorage，刷新后需要重新选择音频。

浏览器白模支持单点、多押、长条、暂停／续播、重开／放弃、自动演示和本地最高分。下落速度可以调整，默认 **2.50×**。

## 接入 MUSYNX

支持已验证的 **Steam 原版 MUSYNX，Build 25570400 / Unity 2017.4.4f1**，使用《蜡烛人》槽位加载自定义曲库。

下载 [MUSYNX 敲谱曲库 Mod 0.2.0](dist/MUSYNX-敲谱曲库-Mod-0.2.0.zip)，完整解压，关闭游戏后运行 `安装.bat`，再正常从 Steam 启动游戏。

1. 在选歌界面按 **F8**，导入敲谱器导出的 JSON 和对应 MP3 / WAV。
2. 在曲库内选歌、选谱；同一首音乐可以保存多张谱面。
3. 点击 **进入官方开歌设置**，调整下落速度、补偿及游戏原有皮肤／场景，再点击原版“开始”。
4. 打击音可选关闭、原版切换音、原版确认音，支持音量与试音。
5. 结算直接使用原版判定与同步率，按“歌曲＋谱面”独立记录最高分和最近 20 局。

自定义成绩不写入原曲存档或排行榜。主题设置沿用游戏自身偏好和已拥有内容。安装器保留原文件备份，卸载时恢复；个人曲库和成绩保留在游戏目录的 `CustomCharts/`。

浏览器预览使用项目自己的评分规则，MUSYNX 中使用游戏原版规则，二者分数不作直接比较。

完整说明见 [Mod 使用文档](mods/ChartRecorder/README.md)。

## 项目结构

| 目录／文件 | 内容 |
| --- | --- |
| `index.html`、`styles.css` | 浏览器入口和样式 |
| `src/editor-core.js`、`src/editor.js` | 敲击录制、量化、分段覆盖、波形时间轴与精修 |
| `src/core.js`、`src/audio.js`、`src/app.js` | 白模判定、音频时钟、输入与渲染 |
| `src/optional-audio.js` | 按需读取可选的本地歌曲包装 |
| `charts/` | 自合成练习谱及上游示例谱面 |
| `mods/ChartRecorder/` | C# Mod、导入助手、构建／安装脚本、独立成绩与测试 |
| `tools/make-mod-examples.cjs` | 生成自己的合成音乐和 Mod 示例曲库 |
| `tools/musynx/` | 历史资源替换实验及谱面转换工具 |
| `vendor/` | 上游判定模型、参考源码及原许可证 |
| `tests/` | 浏览器逻辑与本地服务测试 |
| `docs/USER_GUIDE.md` | 详细玩法、编辑行为与谱面格式 |
| `dist/` | 当前可分享的干净 Mod 安装包 |

`artifacts/`、`.tmp/`、原游戏备份、第三方歌曲及旧分享压缩包只保留在本地，不上传 Git。

## 开发与验证

前端使用原生 **JavaScript / HTML / CSS / Canvas / Web Audio**，无 npm 运行时依赖。Mod 使用 **C# / .NET Framework**；安装与构建脚本使用 PowerShell。

```bash
npm test
npm run examples
```

在 Windows 下运行不依赖原游戏的 C# 核心检查：

```powershell
.\mods\ChartRecorder\tests\run-core.ps1
```

从源码构建 Mod（需要自己安装对应版本的 MUSYNX）：

```powershell
# 从官方 NuGet 下载固定版本依赖
.\mods\ChartRecorder\dependencies.ps1
# 自动查找 Steam 安装；也可使用 -GameRoot 指定位置
.\mods\ChartRecorder\build.ps1
# 生成示例曲库与可分享安装包
.\mods\ChartRecorder\package.ps1 -Archive
```

如果游戏已经安装本 Mod，构建脚本会使用经过校验的原程序集备份；不会将原游戏程序集上传或打进安装包。构建结果写入 `artifacts/musynx-mod/`。

自动检查覆盖单点／长条、多押、时间窗、音频时钟、慢放、网格吸附、分段覆盖、音频缺失提示、谱面导出和成绩持久化。CI 可运行前端测试与 C# 核心检查；实际游戏运行验证需要本机正版游戏，测试探针仅用于独立副本。

## 当前范围

- 支持四键；暂不支持六键、滑键、变 BPM 或自动识别 BPM。
- 慢放使用重采样，音调会降低，尚未实现保持音调的时间拉伸。
- 最长歌曲 10 分钟，最多 20000 个音符；谱面 JSON 最大 3 MB。
- MUSYNX Mod 适配指定的 Steam 原版构建，不适用于 MUSYNX RETURN。
- `Karma` 仅保留 MIT 上游示例谱面；使用者自行提供对应音乐。

## 复用与许可

原创代码采用 [MIT License](LICENSE)。判定模型参考并改编自 [juneekim7/rhy-game](https://github.com/juneekim7/rhy-game)，固定提交为 `7b267a92321da756578a9f5cf777c2247fb89f52`。

实际复用、上游源码与音乐的许可边界见 [第三方说明](THIRD_PARTY_NOTICES.md)。Mod 使用 NAudio 1.8.0（Ms-PL）与 Mono.Cecil 0.11.6（MIT），许可文本随安装包保留。原 MUSYNX 程序、Unity 程序集、游戏资源和商业歌曲不随仓库分发。
