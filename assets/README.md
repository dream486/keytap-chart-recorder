# 本地音频目录

公开仓库不包含第三方歌曲音频或 Base64 音频副本。四张内置练习谱使用浏览器本地合成音乐，无需音频文件。

`charts/karma.json` / `charts/karma.js` 保留 rhy-game 上游 MIT 示例谱面（42 个音符、2 个长条，约 10 秒片段）。如要游玩这张谱，请自行准备对应音乐，通过页面的“选择本地音乐”导入；打谱器也支持导入自己的歌曲。

已有的本地 `karma.mp3` 与 `karma-audio.js` 保留在电脑上，但被 `.gitignore` 排除。可在提供本地 `assets/karma.mp3` 后运行 `node tools/convert-upstream.cjs` 生成离线包装。页面仅在请求 Karma 示例音乐时尝试加载该包装；缺失时会提示导入本地歌曲。

上游歌曲为 Dareharu（달의 하루）《Karma》（염라）。上游作者明确说明不拥有歌曲版权；歌曲不属于源码 MIT 许可。来源固定提交：`7b267a92321da756578a9f5cf777c2247fb89f52`。
