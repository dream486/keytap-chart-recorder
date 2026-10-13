// Experimental MUSYNX original-PC adapter. Audio and game resource installation
// are separate steps; writing a TXT alone does not import a song into MUSYNX.
const fs = require('node:fs');
const { validateChart } = require('../../src/core.js');

function convert(raw, { leadInMs = 2000, cueName = 'lazhuren' } = {}) {
  const chart = validateChart(raw);
  if (!Number.isFinite(leadInMs) || leadInMs < 0) throw new Error('Invalid lead-in');
  if (!cueName || /[\t\r\n]/.test(cueName)) throw new Error('Invalid cue name');
  const ticks = ms => Math.round((ms + leadInMs) * 10000);
  const tracks = [3, 4, 6, 7];
  const lines = [
    `BPM\t${chart.bpm}`, 'Rank\t3',
    'NoteFactoryPrepare\t100\t100\t200\t1000',
    'DefaultWeatherUIB\tnight', `WAV\t02\t${cueName}`,
  ];
  const events = [[ticks(0), `MusicNote\t${ticks(0)}\t1\t02`]];
  // BeatLine expects measure indices, and a 100 ns start/end time.
  const measureMs = 240000 / chart.bpm;
  const finish = ticks(chart.duration + 500);
  for (let i = 0; i * measureMs < chart.duration + leadInMs + 500; i++) {
    const start = Math.round(i * measureMs * 10000);
    events.push([start, `BeatLine\t${start}\t${i}\t--\t${Math.min(finish, Math.round((i + 1) * measureMs * 10000))}`]);
  }
  for (const n of chart.notes) {
    const head = `${ticks(n.time)}\t${tracks[n.lane]}\t01`;
    // 01 deliberately has no WAV definition: the game handles it as silent.
    events.push([ticks(n.time), n.endTime === undefined ? `Note\t${head}` : `LongNote\t${head}\t${ticks(n.endTime)}`]);
  }
  events.sort((a, b) => a[0] - b[0]);
  return lines.concat(events.map(e => e[1])).join('\n') + '\n';
}
if (require.main === module) {
  const [input, output, cueName = 'lazhuren'] = process.argv.slice(2);
  if (!input || !output) throw new Error('Usage: node tools/musynx/convert.cjs input.json output.txt [cueName]');
  fs.writeFileSync(output, convert(JSON.parse(fs.readFileSync(input, 'utf8')), { cueName }));
  console.log(`Converted ${input} -> ${output}`);
}
module.exports = { convert };
