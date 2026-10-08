---
phase: 1
title: Project setup
status: completed
priority: P1
dependencies: []
effort: 0.5 ngày
---

# Phase 1: Project setup

## Overview
Cấu hình project Unity trống thành khung 2D pixel dọc, chia assembly Sim/View/Editor, dựng dự án dotnet SimTests.

## Requirements
- Packages (bản giống Boom Unity): `com.unity.render-pipelines.universal` 17.5.0 (2D Renderer), `com.unity.2d.pixel-perfect` 6.0.0, `com.unity.2d.sprite`, `com.unity.test-framework` 1.7.0, `com.unity.ugui` 2.5.0, `com.coplaydev.unity-mcp`.
- Player: portrait cố định, tham chiếu 1080x1920, target Android + Standalone Windows.
- Không input system (trận tự chạy; menu dùng uGUI EventSystem mặc định).

## Architecture
```
Assets/Scripts/Sim/      BallBattle.Sim.asmdef   (noEngineReferences: true)
Assets/Scripts/View/     BallBattle.View.asmdef  -> Sim
Assets/Scripts/Editor/   BallBattle.Editor.asmdef (Editor only) -> Sim, View
Assets/Tests/EditMode/   BallBattle.Tests.EditMode.asmdef
Assets/Art/Generated/    sprite placeholder (sinh ở phase 4)
Assets/Audio/Generated/  âm placeholder (phase 5)
Assets/Scenes/           Menu.unity, Arena.unity
SimTests/SimTests.csproj (net10.0, LangVersion 9.0, NUnit 4.3.2 + NUnit3TestAdapter 5.0.0 + Microsoft.NET.Test.Sdk 17.14.1 như Boom, <Compile Include="../Assets/Scripts/Sim/**/*.cs" />) <!-- Updated: Validation Session 1 - xUnit -> NUnit -->
```

## Related Code Files
- Modify: `BallBattleUnity/Packages/manifest.json`, `ProjectSettings/ProjectSettings.asset` (orientation), URP assets
- Create: 4 asmdef ở trên, `SimTests/SimTests.csproj`, `SimTests/SmokeTests.cs`, `Assets/Settings/URP-2D.asset` + renderer

## Implementation Steps
1. Sửa manifest, mở Unity batch để resolve packages; kiểm log không lỗi.
2. Tạo URP Asset + 2D Renderer, gán Graphics/Quality.
3. Đặt orientation Portrait, độ phân giải mặc định.
4. Tạo thư mục + asmdef; file `SimVersion.cs` rỗng để Sim compile.
5. Dựng SimTests (NUnit, copy cấu hình csproj từ BoomOnlineUnity/SimTests) link nguồn Sim; 1 smoke test.
6. Cập nhật `docs/code-standards.md` (Sim không dùng UnityEngine, tick, tên file kebab-case cho tài liệu, PascalCase cho C# theo chuẩn Unity).

## Success Criteria
- [ ] Unity batch mở project, 0 lỗi compile.
- [ ] `dotnet test SimTests` xanh.
- [ ] Unity MCP kết nối được Editor.

## Risk Assessment
- Bản URP/pixel-perfect không khớp 6000.5.1f1 → lấy đúng version Boom Unity đang chạy trên cùng editor.
- C# trong Sim dùng tính năng ngôn ngữ mới hơn Unity hỗ trợ → SimTests đặt `LangVersion 9.0`.
