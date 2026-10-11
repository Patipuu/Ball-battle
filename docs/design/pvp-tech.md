# PvP — đánh giá kỹ thuật (Dev - Code review, 11/10/2026)

Đi kèm `pvp-research.md` của Game Designer (thể thức, cân bằng). File này chỉ bàn phần kỹ thuật. Mọi ước lượng là sơ bộ, chưa có prototype.

## 1. Điểm mạnh sẵn có
- **Sim tất định, không phụ thuộc Unity** (`BallBattle.Sim.asmdef`, `noEngineReferences: true`). Cùng loadout + arena + seed + `SimVersion.Rules` thì ra cùng một trận.
- **State có hash** (`WeaponRule/TraitRule.HashInto`, `SimHash`): so hash cuối trận để phát hiện lệch hoặc gian lận.
- **Dữ liệu một trận rất nhỏ:** id vũ khí, danh sách trait + cấp, id arena, seed, version. Khoảng < 100 byte, không cần gửi replay vì replay = chạy lại sim.

## 2. Mô hình đề xuất: PvP bất đồng bộ (ghost)
Giống Super Auto Pets / The Bazaar / Backpack Battles: người chơi đấu với **bản chụp loadout** của người khác, không cần cả hai online.
1. Người chơi khoá loadout → client gửi `GhostRecord { simVersion, weaponId, traits[], rating, createdAt }` lên kho ghost.
2. Ghép trận: server chọn ghost cùng `simVersion` và rating gần, chọn arena (sau bước cấm arena) và seed **do server sinh** (client không chọn được seed có lợi).
3. Client chạy sim, hiển thị trận, gửi kết quả + hash cuối.
4. Server chạy lại sim headless (.NET, đã chứng minh chạy được trên máy dùng chung) để xác nhận kết quả trước khi cộng rating. Không khớp → huỷ trận, ghi log.

Không làm PvP realtime: không có gì để đồng bộ trong trận vì trận tự đánh.

## 3. Lưu loadout ở đâu
- Game hiện **chưa có backend**; save chỉ ở máy (`SaveStore`, `SaveData.CurrentVersion = 1`).
- Đề xuất BaaS (Supabase: Postgres + Edge Functions, hoặc Firebase: Firestore + Cloud Functions) cho kho ghost, rating, đăng nhập ẩn danh. Bước xác nhận kết quả cần một worker chạy .NET (Sim là C# thuần) — container nhỏ, gọi từ Edge/Cloud Function.
- Bộ sưu tập vũ khí/trait của người chơi (phần "thu thập") phải nằm trên server, nếu không thì sửa file save là mở khoá hết. Tối thiểu: server lưu danh sách mở khoá và từ chối ghost dùng thứ chưa mở.

## 4. Lệch `SimVersion`
- Mỗi ghost và mỗi trận gắn `SimVersion.Rules`. Chỉ ghép cùng version.
- Client cũ hơn version hiện hành của server → bắt cập nhật trước khi vào PvP (không cho đấu chéo).
- Sau mỗi lần bump: ghost cũ chuyển sang lưu trữ (không xoá, dùng để xem lại replay bằng build cũ nếu cần), kho ghost mới được "mồi" bằng ghost bot (`RunBot`) để không trống lúc đầu.
- Tách rõ: `SimVersion` (luật trận) ≠ `SaveData.CurrentVersion` (định dạng save) ≠ phiên bản giao thức PvP.

## 5. Rủi ro kỹ thuật chính
1. **Tất định float giữa các nền tảng — rủi ro lớn nhất.** Sim dùng `float` và `MathF` (Sin/Cos/Atan2/Sqrt… trong `Vec2.cs`, `MatchSim.Collisions.cs`, `MatchSim.Rules.cs`, `MatchSim.Obstacles.cs`, `VolleyRule.cs`). Trong cùng một build thì tất định, nhưng **giữa Windows/Linux/Android/WebGL, Mono vs IL2CPP vs .NET server** thì không đảm bảo bit-giống-nhau. Hệ quả: hash lệch dù không ai gian lận.
   - Việc đầu tiên của phase PvP: test chéo — chạy cùng 10.000 trận trên .NET (server), Unity Windows, Linux build, WebGL, so hash. Nếu lệch: (a) server là nguồn sự thật, client chỉ hiển thị, hoặc (b) chuyển Sim sang fixed-point (công lớn, phải bump version và đo lại toàn bộ cân bằng).
2. **Tuning là `static` có thể sửa** (`WeaponTuning`, 57 trường `public static float`, có đoạn dùng Reflection). Server chạy nhiều trận song song trong một process thì không được sửa tuning lúc chạy; cần đóng băng hoặc chuyển thành dữ liệu theo version.
3. **Gian lận bộ sưu tập** (mục 3) — chặn ở server.
4. **Rating bị phá bởi combo mạnh** — phụ thuộc ngưỡng chặn trong `bot-sweep-v4`.

## 6. Ước lượng công sức (sơ bộ, theo phase)
| Phase | Nội dung | Ước lượng |
|---|---|---|
| P0 | Test tất định chéo nền tảng + quyết định float/fixed-point | 2–4 ngày (thêm 2–3 tuần nếu phải chuyển fixed-point) |
| P1 | Điều kiện tiên quyết đã chốt: Art 2, màn kết quả (Prompt 4), replay từ seed | 1–2 tuần (phần lớn đã có prompt) |
| P2 | Chế độ "đấu ghost offline": ghost lấy từ bot, chọn/cấm arena, UI loadout từ bộ sưu tập — chưa cần server | 1–2 tuần |
| P3 | Backend: đăng nhập ẩn danh, kho ghost, ghép trận theo version + rating, seed do server sinh | 2–3 tuần |
| P4 | Worker .NET xác nhận kết quả, chống gian lận bộ sưu tập, rating | 1–2 tuần |
| P5 | Mùa giải / bảng xếp hạng / xem lại trận người khác | sau |

Thứ tự đề xuất: P0 trước tiên (rủi ro lớn nhất, rẻ để kiểm), sau đó P1 → P2 có thể làm song song với đợt v4. Chỉ đụng backend (P3) khi P2 chơi thật thấy vui.

## 7. Câu hỏi còn mở
- Nền tảng phát hành mục tiêu (PC/Web/Mobile)? Ảnh hưởng trực tiếp tới P0.
- Có chấp nhận chi phí BaaS hằng tháng không, hay muốn tự host?
- PvP có tính vào tiến trình Run (mở khoá) không, hay tách riêng?

## 8. Kết quả đo đầu tiên cho P0 (11/10, 2:27 sáng)
- Chạy `SimTests` bằng .NET 8 trên Linux x64 (máy dùng chung): 168/169 pass. `VersusGoldenTests.VersusOutcomesMatchGolden` fail: golden ghi trên Windows `0x2E92A642D4E02B1A`, Linux ra `0xD82531CC135DBA09` — lặp lại ổn định (Debug/Release/tắt SIMD).
- Nguyên nhân đã khoanh vùng: `MathF.Sin/Cos` trong `Vec2.FromAngleDeg` (dùng mỗi tick cho đầu lưỡi, hướng bắn Volley, hướng spawn). Thay bằng trig double thì digest đổi tiếp → kết quả phụ thuộc thư viện toán của nền tảng.
- **Kết luận: Sim hiện KHÔNG tất định giữa nền tảng.** Hai hướng: (a) server là nguồn sự thật, client chỉ hiển thị (+ replay xem trên client có thể lệch nhẹ); (b) thay sin/cos bằng bảng tra / xấp xỉ đa thức cố định (rẻ hơn fixed-point toàn bộ, nhưng vẫn phải bump `SimVersion` và đo lại cân bằng), rồi test lại chéo Mono/IL2CPP/WebGL/ARM.
- Bộ test chéo: `/workspace/ball-battle/simrunner/hashtest/` (46 case, `hashes-dotnet.txt`). Bước tiếp: build Unity có chế độ in hash cùng danh sách seed để so.
- Rủi ro phụ tìm thấy: tuning `static` không readonly (`WeaponTuning`, `RunTuning`, `ArenaTuning`); `Card.GetHashCode` dùng `string.GetHashCode` (ngẫu nhiên theo process trên .NET Core) — chưa ảnh hưởng nhưng là bẫy nếu sau này có container duyệt theo Card.

## Đã chốt 11/10 (Phú duyệt 14:51 ICT)

- **Hướng PvP FINAL: "no-cost" (không tốn tiền).**
  - **Giai đoạn 1 không có server.** Người chơi xuất loadout thành **mã chia sẻ ngắn** (vũ khí, trait, seed, `SimVersion`).
  - Đối thủ dán mã và **đấu ngay trên máy mình**, kết quả tất định.
  - **Bộ thẻ chung** với **ngân sách điểm** (tối đa **1 trait sustain**). **Không lưu đồ sở hữu**.
  - **Chưa làm** ranked, 2v2, FFA.
  - Sau này nếu cần: backend **gói miễn phí** (tuỳ chọn).
- **Điều kiện bắt buộc:** v4 chuyển `MathF.Sin/Cos` sang **bảng tra lượng giác** để tất định giữa các nền tảng. Mã có `SimVersion` lệch thì báo không tương thích.
- Các nội dung về ranked, server là nguồn sự thật, BaaS trả phí ở trên chỉ còn là tham khảo cho sau này.
