// Chạy TRÊN VPS: gửi các lỗi mới (drafts.json + img/ cùng thư mục) thành BẢN NHÁP bug đứng tên Bot QA - đúng đường của
// công cụ MCP submit_bug (lưu ảnh vào kho bằng chứng rồi saveDraft). Lead duyệt ở trang "Nháp bot" mới thành bug thật.
//   node submit-bug-drafts.mjs <portal.env.json> [--apply] [--bot <email .bot>] [--site https://portal...]
// drafts.json: [{ title, screen, steps, actual, expected, severity_suggested, precondition?, account?, device?, os?,
//                 frequency?, related?, note?, images: ["a.png", ...] }]   (ít nhất 1 ảnh, tối đa 10)
// Không --apply = chạy thử (kiểm trường bắt buộc, độ dài, ảnh có thật, bot tồn tại). Chạy lại an toàn: bỏ qua nháp
// đã có cùng tiêu đề (đang chờ / đã duyệt) của bot. Ghi prod -> thường do USER chạy; sao lưu DB trước.
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const argv = process.argv.slice(2);
const opt = (k, d) => { const i = argv.indexOf(`--${k}`); return i >= 0 ? argv[i + 1] : d; };
const envFile = path.resolve(argv[0]);
const APPLY = argv.includes('--apply');
const BOT_EMAIL = opt('bot', 'bot-qa@3q-portal.bot');
const SITE = opt('site', 'https://portal.wanmeivn.com');
const HERE = import.meta.dirname;
const APP = path.join(path.dirname(envFile), 'app');
const imp = (rel) => import(pathToFileURL(path.join(APP, rel)).href);
const { openD1 } = await imp('server/d1-sqlite-adapter.mjs');
const { openKv } = await imp('server/kv-file-adapter.mjs');
const { storeEvidence } = await imp('cloudflare/bug-evidence-storage.js');
const { saveDraft } = await imp('cloudflare/bug-drafts-rpc.js');
const cfg = JSON.parse(fs.readFileSync(envFile, 'utf8').replace(/^﻿/, ''));
const DATA = path.resolve(path.dirname(envFile), cfg.DATA_DIR || 'data');
const env = { DB: openD1(path.join(DATA, 'portal.sqlite')), EVIDENCE: openKv(path.join(DATA, 'evidence')), EVIDENCE_LIMIT_BYTES: Number(cfg.EVIDENCE_LIMIT_GB || 5) * 1024 ** 3 };
const raw = env.DB.raw;
const drafts = JSON.parse(fs.readFileSync(path.join(HERE, 'drafts.json'), 'utf8').replace(/^﻿/, ''));

const bot = raw.prepare('select id, email, name, role, reporter, active, session_ver from users where email = ? and active = 1').get(BOT_EMAIL);
const tokenRow = bot && raw.prepare('select id, name from api_tokens where user_id = ? and revoked_at is null order by created_at desc').get(bot.id);
const MAX = { screen: 200, steps: 5000, actual: 3000, expected: 3000, title: 150, precondition: 1000, account: 100, device: 60, os: 40, frequency: 30, related: 200, note: 3000 };
const problems = [];
if (!bot || !tokenRow) problems.push(`không thấy Bot QA ${BOT_EMAIL} (hoặc chưa có token)`);
drafts.forEach((d, i) => {
  const tag = `#${i + 1} ${d.title || '(không tiêu đề)'}`;
  for (const k of ['screen', 'steps', 'actual', 'expected']) if (!String(d[k] ?? '').trim()) problems.push(`${tag}: thiếu ${k}`);
  for (const [k, n] of Object.entries(MAX)) if (String(d[k] ?? '').length > n) problems.push(`${tag}: ${k} dài quá ${n}`);
  if (d.severity_suggested && !['Nặng', 'Trung bình', 'Nhẹ'].includes(d.severity_suggested)) problems.push(`${tag}: mức độ sai`);
  if (!Array.isArray(d.images) || d.images.length < 1 || d.images.length > 10) problems.push(`${tag}: cần 1-10 ảnh`);
  for (const n of d.images || []) if (!fs.existsSync(path.join(HERE, 'img', n))) problems.push(`${tag}: thiếu ảnh ${n}`);
});
const existing = new Set(bot ? raw.prepare("select json_extract(bug, '$.title') t from bug_drafts where reporter = ? and status in ('pending', 'approved')").all(bot.reporter).map((r) => r.t) : []);
const todo = drafts.filter((d) => !existing.has(d.title));
console.log(`bản nháp: ${drafts.length} | đã có (bỏ qua): ${drafts.length - todo.length} | sẽ gửi: ${todo.length} | Bot QA: ${bot ? `${bot.name} (${bot.reporter})` : 'không có'}`);
if (problems.length) { console.log(`DỪNG:\n${problems.join('\n')}`); process.exit(1); }
if (!APPLY) { todo.forEach((d) => console.log(`  - [${d.severity_suggested || '-'}] ${d.title} (${d.images.length} ảnh)`)); console.log('Kiểm xong. Chạy lại với --apply.'); process.exit(0); }

const me = { ...bot, token_id: tokenRow.id, token_name: tokenRow.name };
const day = new Date().toISOString().slice(0, 10).replace(/-/g, '');
const mime = (n) => (/\.jpe?g$/i.test(n) ? 'image/jpeg' : 'image/png');
let sent = 0;
for (const d of todo) {
  const urls = [];
  for (const n of d.images) {
    const key = `${day}/${crypto.randomUUID()}.${/\.jpe?g$/i.test(n) ? 'jpg' : 'png'}`;
    const refused = await storeEvidence(env, me, key, mime(n), async () => { const b = fs.readFileSync(path.join(HERE, 'img', n)); return b.buffer.slice(b.byteOffset, b.byteOffset + b.byteLength); });
    if (refused) throw new Error(`ảnh ${n}: ${refused.message}`);
    urls.push(`${SITE}/sb/storage/v1/object/public/bug-evidence/${key}`);
  }
  const { images, ...fields } = d;
  const r = await saveDraft({ ...env, VIA: tokenRow.name }, me, fields, urls);
  console.log(`  ✓ ${d.title} -> nháp ${r.draft_id}`);
  sent++;
}
console.log(`Đã gửi ${sent} bản nháp. Lead duyệt ở trang "Nháp bot".`);
