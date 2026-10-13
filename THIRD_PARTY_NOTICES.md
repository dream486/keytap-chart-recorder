# Third-party notices

This prototype adapts the Judgement class and timing-window judgment approach from [rhy-game](https://github.com/juneekim7/rhy-game), by Junee (juneekim7).

- Upstream commit: `7b267a92321da756578a9f5cf777c2247fb89f52`
- Copyright (c) 2023 준이 (Junee, juneekim7)
- License: MIT, full text in `vendor/rhy-game/LICENSE`
- Adaptation: `vendor/judgement.js` removes TypeScript annotations and adds browser/CommonJS exports. `src/core.js` implements the same absolute timing-error selection, with inclusive boundaries, an early-input guard, and separate score/session management.
- Reference-only original files: `vendor/rhy-game/lib/`, `vendor/rhy-game/dist/`, `vendor/rhy-game/README.md`.

The original example chart (`examples/games/play.js`) and its four-lane wrapper are retained in `vendor/rhy-game/examples/` under the source-code MIT license. `tools/convert-upstream.cjs` converts the original `s` / `l` lane strings into tap / hold notes, preserving the original note times and converting the song's -450 ms delay into a +450 ms audio timeline shift. Outputs: `charts/karma.js` and `charts/karma.json`.

The public repository does not distribute Dareharu (달의 하루) — Karma (염라), its Base64 wrapper, album covers or original MUSYNX resources. Optional local audio files are ignored by Git. The music is not covered by the source-code MIT license; users supply their own audio. Other browser practice tracks and the Mod example tracks are synthesized locally by `src/audio.js`.

Original project code is licensed under the root `LICENSE`. MUSYNX Mod dependencies keep their own licenses; see `mods/ChartRecorder/THIRD_PARTY_NOTICES.md`.
