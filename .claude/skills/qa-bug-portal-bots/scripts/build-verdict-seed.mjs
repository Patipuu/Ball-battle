// Gom bảng kết quả kiểm lại (agent viết, cột | Mã | Kết luận | Build | Ảnh | Đã thấy |) thành gói gửi Phòng bot:
// <out>/room-seed.json (kết luận + ghi chú) và <out>/img/ (tối đa 3 ảnh / bug, ảnh zoom trước).
//   node build-verdict-seed.mjs --out <thư mục gói> --shots <thư mục ảnh> \
//     --file "<bảng lượt 1.md>|tester13 Lv124, client 3.1.0.0.445" [--file "<bảng lượt 2.md>|..."] [--build 3.1.0.0.448] [--date "08/10"]
// Gộp nhiều lượt: lượt sau có kết luận (không phải KHONG_KIEM_DUOC) thì thay lượt trước.
// DA_SUA -> kết luận pass; CHUA_SUA -> fail; "CHUA_SUA ... MỘT PHẦN" / "CHUA_SUA (điều kiện)" -> ghi chú chờ Lead / PO;
// KHONG_KIEM_DUOC, QUAN_SAT... -> ghi chú. Dòng của bug không ở "Đã sửa" sẽ bị apply-verdict-seed.mjs chặn khi chạy thử.
import fs from 'node:fs';
import path from 'node:path';

const args = process.argv.slice(2);
const opt = (k) => { const i = args.indexOf(`--${k}`); return i >= 0 ? args[i + 1] : null; };
const files = args.flatMap((a, i) => (a === '--file' ? [args[i + 1]] : []));
const OUT = path.resolve(opt('out') || 'verdict-seed');
const SHOTS = path.resolve(opt('shots') || 'after');
const DATE = opt('date') || new Date().toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit' });
if (!files.length) { console.error('Thiếu --file "<bảng.md>|<mô tả>"'); process.exit(1); }

fs.rmSync(OUT, { recursive: true, force: true });
fs.mkdirSync(path.join(OUT, 'img'), { recursive: true });
const clean = (s) => s.replace(/\*\*/g, '').replace(/\s+/g, ' ').trim();
const byCode = new Map();
for (const spec of files) {
  const [file, who = ''] = spec.split('|');
  for (const line of fs.readFileSync(file, 'utf8').split(/\r?\n/)) {
    if (!/^\|\s*[A-Z]-\d{3}/.test(line)) continue;
    const c = line.split('|').map((s) => s.trim());
    const build = c[3]?.match(/\d+(\.\d+)+/)?.[0] || null;
    const r = { code: c[1].match(/[A-Z]-\d{3}/)[0], verdict: c[2], build, images: c[4], seen: c[5], who };
    const prev = byCode.get(r.code);
    if (prev && !/^\s*KHONG_KIEM_DUOC/i.test(r.verdict)) {
      byCode.set(r.code, { ...r, seen: `Kiểm ${r.who}: ${r.seen} | Lần trước: ${prev.seen}`, images: `${r.images}, ${prev.images}` });
    } else if (prev) { prev.seen += ` | ${r.seen}`; prev.images += `, ${r.images}`; } else byCode.set(r.code, r);
  }
}
const pickImages = (text) => {
  const names = [...new Set(text.match(/[A-Za-z0-9_-]+\.png/g) || [])].filter((n) => fs.existsSync(path.join(SHOTS, n)));
  const zoom = names.filter((n) => /z[-_]|zoom/i.test(n));
  return [...zoom, ...names.filter((n) => !zoom.includes(n))].slice(0, 3);
};
const verdicts = [];
const notes = [];
for (const r of byCode.values()) {
  const v = clean(r.verdict).toUpperCase();
  const body = `Kiểm lại ${DATE}, ${r.who}. ${clean(r.seen)}`.slice(0, 1900);
  const imgs = pickImages(r.images);
  for (const n of imgs) fs.copyFileSync(path.join(SHOTS, n), path.join(OUT, 'img', n));
  if (/^CHUA_SUA.*MỘT PHẦN/.test(v)) notes.push({ code: r.code, text: `Sửa một phần, chờ Lead chốt. ${body}`.slice(0, 1990), images: imgs });
  else if (/^CHUA_SUA.*\(/.test(v)) notes.push({ code: r.code, text: `Cần Lead / PO xem ảnh để chốt. ${body}`.slice(0, 1990), images: imgs });
  else if (v.startsWith('DA_SUA')) verdicts.push({ code: r.code, result: 'pass', summary: body, images: imgs, build: r.build });
  else if (v.startsWith('CHUA_SUA')) verdicts.push({ code: r.code, result: 'fail', summary: body, images: imgs, build: r.build });
  else notes.push({ code: r.code, text: `Chưa kiểm được. ${body}`.slice(0, 1990), images: imgs });
}
fs.writeFileSync(path.join(OUT, 'room-seed.json'), JSON.stringify({ build: opt('build'), verdicts, notes }, null, 1));
console.log('verdicts', verdicts.length, verdicts.map((v) => `${v.code}:${v.result}@${v.build}`).join(' '));
console.log('notes', notes.length, notes.map((n) => n.code).join(' '));
console.log('images', fs.readdirSync(path.join(OUT, 'img')).length, '->', OUT);
