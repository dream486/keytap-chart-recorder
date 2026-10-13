// Adapted from rhy-game/lib/rhy-game.ts, commit 7b267a92321da756578a9f5cf777c2247fb89f52.
// Copyright (c) 2023 준이 (Junee, juneekim7). MIT; see rhy-game/LICENSE.
// TypeScript annotations removed; CommonJS/browser exports added. Model unchanged.
(function (root) {
  class Judgement {
    constructor(name, time, scoreRatio, isCombo = true) {
      if ([name, time, scoreRatio, isCombo].map(arg => typeof arg).includes('undefined')) {
        throw new Error('judgement must requires name, time, scoreRatio');
      }
      if (time < 0) throw new Error('judgement time must be not negative');
      if (scoreRatio < 0 || scoreRatio > 1) throw new Error('scoreRatio should be between in 0 to 1');
      this.name = name; this.time = time; this.scoreRatio = scoreRatio; this.isCombo = isCombo;
    }
  }
  Judgement.miss = new Judgement('miss', 0, 0, false);
  if (typeof module !== 'undefined') module.exports = Judgement;
  else root.Judgement = Judgement;
})(globalThis);
