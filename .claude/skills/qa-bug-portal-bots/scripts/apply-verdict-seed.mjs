// Chạy TRÊN VPS (thư mục gói có room-seed.json + img/). Đi qua đúng RPC của web (callRpcAs), không ghi thẳng bảng.
//   node apply-verdict-seed.mjs <portal.env.json> [--apply] [--lead <email Lead>] [--bot <email bot .bot>] [--site https://portal...]
// Không --apply = chạy thử: chỉ kiểm (bug còn "Đã sửa", đủ ảnh, còn ô QA cho bot) rồi dừng.
// --apply: tạo Bot QA nếu chưa có (tester, email .bot, ô QA trống) + token MCP (ghi file cạnh portal.env.json, không in),
// phân tích bù bug Đã sửa chưa có phân tích, tải ảnh, gửi kết luận (bỏ qua bug đã có kết luận chờ), đăng ghi chú (bỏ qua ghi chú trùng).
// Ghi prod: sao lưu DB trước; thường do USER chạy (bộ phân loại an toàn của Claude Code chặn agent ghi dữ liệu dùng chung).
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const argv = process.argv.slice(2);
const opt = (k, d) => { const i = argv.indexOf(`--${k}`); return i >= 0 ? argv[i + 1] : d; };
const envFile = path.resolve(argv[0]);
const APPLY = argv.includes('--apply');
const LEAD_EMAIL = opt('lead', 'phamphu422@gmail.com');
const BOT_EMAIL = opt('bot', 'bot-qa@3q-portal.bot');
const SITE = opt('site', 'https://portal.wanmeivn.com');
const HERE = import.meta.dirname;
const APP = path.join(path.dirname(envFile), 'app');
const imp = (rel) => import(pathToFileURL(path.join(APP, rel)).href);
const { openD1 } = await imp('server/d1-sqlite-adapter.mjs');
const { openKv } = await imp('server/kv-file-adapter.mjs');
const { callRpcAs } = await imp('cloudflare/bug-portal-api-core.js');
const { storeEvidence } = await imp('cloudflare/bug-evidence-storage.js');
const { analyzeFix } = await imp('cloudflare/bot-room-rpc.js');
const cfg = JSON.parse(fs.readFileSync(envFile, 'utf8').replace(/^﻿/, ''));
const DATA = path.resolve(path.dirname(envFile), cfg.DATA_DIR || 'data');
const env = { DB: openD1(path.join(DATA, 'portal.sqlite')), EVIDENCE: openKv(path.join(DATA, 'evidence')), EVIDENCE_LIMIT_BYTES: Number(cfg.EVIDENCE_LIMIT_GB || 5) * 1024 ** 3 };
const raw = env.DB.raw;
const seed = JSON.parse(fs.readFileSync(path.join(HERE, 'room-seed.json'), 'utf8'));

const lead = raw.prepare("select id, email, name, role, reporter, active, session_ver from users where role = 'lead' and active = 1 and email = ?").get(LEAD_EMAIL);
if (!lead) throw new Error(`không thấy Lead ${LEAD_EMAIL}`);
const fixed = new Map(raw.prepare("select id, code from bugs where status = 'Đã sửa'").all().map((b) => [b.code, b.id]));
const problems = [...seed.verdicts, ...seed.notes].filter((x) => !fixed.has(x.code)).map((x) => `${x.code}: không ở Đã sửa`);
for (const v of seed.verdicts) for (const n of v.images) if (!fs.existsSync(path.join(HERE, 'img', n))) problems.push(`thiếu ảnh ${n}`);
let bot = raw.prepare('select id, email, name, role, reporter, active, session_ver from users where email = ?').get(BOT_EMAIL);
const used = new Set(raw.prepare('select reporter from users where reporter is not null and active = 1').all().map((u) => u.reporter));
const slot = bot?.reporter || ['QA-C', 'QA-D', 'QA-E'].find((s) => !used.has(s));
if (!slot) problems.push('không còn ô QA trống cho Bot QA');
console.log(`Đã sửa: ${fixed.size} | kết luận: ${seed.verdicts.length} | ghi chú: ${seed.notes.length} | Bot QA: ${bot ? 'có sẵn' : `tạo mới ô ${slot}`}`);
if (problems.length) { console.log(`DỪNG:\n${problems.join('\n')}`); process.exit(1); }
if (!APPLY) { console.log('Kiểm xong. Chạy lại với --apply.'); process.exit(0); }

if (!bot) {
  const c = await callRpcAs(env, lead, 'lead_create_user', { p_email: BOT_EMAIL, p_name: 'Bot QA', p_role: 'tester', p_reporter: slot });
  bot = raw.prepare('select id, email, name, role, reporter, active, session_ver from users where id = ?').get(c.id);
}
let tokenRow = raw.prepare('select id, name from api_tokens where user_id = ? and revoked_at is null order by created_at desc').get(bot.id);
if (!tokenRow) {
  const t = await callRpcAs(env, lead, 'lead_create_api_token', { p_user_id: bot.id, p_name: 'Bot QA' });
  const tokenFile = path.join(path.dirname(envFile), 'bot-qa-mcp-token.txt');
  fs.writeFileSync(tokenFile, t.token);
  tokenRow = raw.prepare('select id, name from api_tokens where user_id = ? and revoked_at is null order by created_at desc').get(bot.id);
  console.log(`token Bot QA đã ghi ra ${tokenFile}`);
}
const me = { ...bot, token_id: tokenRow.id, token_name: tokenRow.name };
const benv = { ...env, VIA: tokenRow.name };

let analyzed = 0;
for (const [, id] of fixed) {
  if (raw.prepare("select 1 from bot_messages where bug_id = ? and kind = 'analysis'").get(id)) continue;
  const ev = raw.prepare("select build, note from bug_events where bug_id = ? and to_status = 'Đã sửa' order by at desc limit 1").get(id) || {};
  await analyzeFix(env, id, ev.build, ev.note);
  analyzed++;
}
const day = new Date().toISOString().slice(0, 10).replace(/-/g, '');
async function upload(name) {
  const key = `${day}/${crypto.randomUUID()}.png`;
  const refused = await storeEvidence(env, me, key, 'image/png', async () => { const b = fs.readFileSync(path.join(HERE, 'img', name)); return b.buffer.slice(b.byteOffset, b.byteOffset + b.byteLength); });
  if (refused) throw new Error(`ảnh ${name}: ${refused.message}`);
  return `${SITE}/sb/storage/v1/object/public/bug-evidence/${key}`;
}
const hasPending = (code) => raw.prepare("select 1 from bot_verdicts where bug_id = ? and status = 'pending'").get(fixed.get(code));
const posted = (body) => raw.prepare("select 1 from bot_messages where kind = 'text' and body = ?").get(body);
let proposed = 0;
for (const v of seed.verdicts) {
  if (hasPending(v.code)) continue;
  const evidence = [];
  for (const n of v.images) evidence.push(await upload(n));
  await callRpcAs(benv, me, 'room_propose_verdict', { p_code: v.code, p_result: v.result, p_summary: v.summary, p_build: v.build || seed.build, p_evidence: evidence });
  proposed++;
}
let noted = 0;
for (const n of seed.notes) if (!posted(n.text)) { await callRpcAs(benv, me, 'room_post', { p_code: n.code, p_body: n.text }); noted++; }
console.log(`phân tích ${analyzed} bug, kết luận ${proposed}, ghi chú ${noted}`);
