# Unity templates (assets/unity)

Code mẫu cho stack Unity 6 + FishNet 4.7.2 + FishyEOS (đã vá) + PlayEveryWare EOS 6.1.0. Mô tả từng file, game phải cung cấp gì, thứ tự tích hợp: `references/unity/templates-guide.md`.

## Cấu trúc

| Thư mục | Assembly | Nội dung |
|---|---|---|
| `Core/` | `TeamNet.Multiplayer.Core` (`noEngineReferences: true`) | Logic thuần C#: guard, epoch, quorum, admission, codec thuộc tính lobby |
| `Eos/` | `TeamNet.Multiplayer.Eos` | `EosLobbyService`, `EosLobbyOperationOwnership`, `EosBootstrap` (boot init + Connect login), `DevelopmentEosAuth` (cần PEW EOS + FishyEOS) |
| `FishNetEos/` | `TeamNet.Multiplayer.FishNetEos` | Probe, observer condition, spawn skeleton, `AddressTransport`, `EosP2PBoundary` |
| `Tests/Core/` | `TeamNet.Multiplayer.Core.Tests` (Unity) + `.csproj` (dotnet) | 113 test NUnit cho Core |
| `Tests/Wiring/` | `TeamNet.Multiplayer.Wiring.Tests` (Editor) | Test EditMode abstract kiểm wiring prefab NetworkManager (Multipass/Tugboat/FishyEOS, observer condition, không PlayerSpawner) |
| `Editor/`, `FishyEOS-patches/`, `config-templates/` | | Xem README riêng của từng phần |

Cách dùng: copy `Core/`, `Eos/`, `FishNetEos/` vào `Assets/` của dự án (giữ asmdef). Các asmdef tham chiếu `Fishnet.Plugins.FishyEOS`, `com.Epic.OnlineServices`, `com.playeveryware.eos`, `com.playeveryware.eos.core`, `FishNet.Runtime` theo tên asmdef mặc định của các package đó. Nếu dự án đổi tên asmdef thì sửa `references`.

## Trạng thái kiểm chứng

- **Compile-checked** (Roslyn của Unity 6000.5.1f1, 0 lỗi): cả 3 thư viện, kèm test Core và wiring test.
- **Test chạy thật**: 113/113 test Core pass (`dotnet test`, ngoài Unity).
- **CHƯA runtime**: `EosBootstrap` và wiring test chỉ compile (cần EOS thật / prefab NetworkManager thật). `Eos/` và `FishNetEos/` chưa được chạy trong dự án mới. Compile-check chỉ kiểm cú pháp và kiểu.
  - `ConnectionProbe` là `NetworkBehaviour` có RPC: FishNet codegen (IL weaving) chỉ chạy khi Unity import thật. Compile ở đây không chứng minh RPC được weave.
  - Thuật toán lease/fence/deferred-leave/watchdog trong `Eos/` giữ nguyên từ bản đã chạy trên thiết bị thật; chỉ đổi kiểu, namespace, phụ thuộc.

## Lệnh compile-check (đã chạy, macOS)

Biến dùng chung:

```bash
UNITY=/Volumes/FanxiangTBOS/SoftwareInstalled/6000.5.1f1/Unity.app/Contents/Resources/Scripting
CSC="$UNITY/DotNetSdk/dotnet $UNITY/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll -nologo -noconfig -nostdlib+ -langversion:9 -target:library -debug- -warn:4"
NS=$UNITY/NetStandard/ref/2.1.0/netstandard.dll        # netstandard 2.1 reference
UE=$UNITY/Managed/UnityEngine
SA=<project>/Library/ScriptAssemblies                    # DLL do Unity build ra khi project đã import FishNet/FishyEOS/PEW
OUT=<scratch>/out                                        # ngoài thư mục skill
COMMON="-r:$NS -r:$UE/UnityEngine.CoreModule.dll -r:$UE/UnityEngine.dll -r:$UE/Unity.Scripting.dll"
```

1. Core (tách riêng, KHÔNG có tham chiếu UnityEngine, chứng minh nó thuần):

```bash
$CSC -out:$OUT/TeamNet.Multiplayer.Core.dll -r:$NS Core/*.cs
```

2. Eos:

```bash
$CSC -out:$OUT/TeamNet.Multiplayer.Eos.dll $COMMON -r:$OUT/TeamNet.Multiplayer.Core.dll \
  -r:$SA/FishNet.Runtime.dll -r:$SA/Fishnet.Plugins.FishyEOS.dll -r:$SA/com.Epic.OnlineServices.dll \
  -r:$SA/com.playeveryware.eos.dll -r:$SA/com.playeveryware.eos.core.dll Eos/*.cs
# nhánh Editor của DevelopmentEosAuth:
$CSC -out:$OUT/Eos-editor.dll $COMMON -define:UNITY_EDITOR -r:$UE/UnityEditor.dll -r:$UE/UnityEditor.CoreModule.dll \
  -r:$OUT/TeamNet.Multiplayer.Core.dll -r:$SA/FishNet.Runtime.dll -r:$SA/Fishnet.Plugins.FishyEOS.dll \
  -r:$SA/com.Epic.OnlineServices.dll -r:$SA/com.playeveryware.eos.dll -r:$SA/com.playeveryware.eos.core.dll Eos/*.cs
```

3. FishNet:

```bash
$CSC -out:$OUT/TeamNet.Multiplayer.FishNetEos.dll $COMMON -r:$OUT/TeamNet.Multiplayer.Core.dll \
  -r:$SA/FishNet.Runtime.dll -r:$SA/Fishnet.Plugins.FishyEOS.dll FishNetEos/*.cs
```

4. Wiring test (cần UnityEditor + NUnit; NUnit của package là net472 nên dùng profile 4.8 của Unity thay vì netstandard, không trộn hai profile):

```bash
NUNIT=<project>/Library/PackageCache/com.unity.ext.nunit@44f7d31723bd/net472/unity-custom/nunit.framework.dll
R48=$UNITY/UnityReferenceAssemblies/unity-4.8-api
$CSC -out:$OUT/Wiring.dll -r:$R48/mscorlib.dll -r:$R48/System.dll -r:$R48/System.Core.dll \
  -r:$R48/Facades/netstandard.dll -r:$R48/Facades/System.Runtime.dll \
  -r:$UE/UnityEngine.CoreModule.dll -r:$UE/UnityEngine.dll -r:$UE/UnityEditor.dll -r:$UE/UnityEditor.CoreModule.dll \
  -r:$NUNIT -r:$OUT/TeamNet.Multiplayer.Core.dll -r:$OUT/TeamNet.Multiplayer.FishNetEos.dll \
  -r:$SA/FishNet.Runtime.dll -r:$SA/Fishnet.Plugins.FishyEOS.dll Tests/Wiring/*.cs
```

(zsh: gọi `${=CSC}` để tách từ; đừng bọc trong `bash -c`.) Phải chạy sau bước 1 và 3 (cần Core.dll và FishNetEos.dll). Lớp test là `abstract`: trong Unity, thêm lớp con đặt `NetworkManagerPrefabPath` thì test mới chạy.

5. Test (ngoài Unity, cần .NET SDK; build output đặt ngoài cây skill):

```bash
cd Tests/Core
dotnet test TeamNet.Multiplayer.Core.Tests.csproj --artifacts-path <scratch>/artifacts
```

`<project>/Library/ScriptAssemblies` chỉ có sau khi dự án mới mở trong Unity ít nhất một lần.

## Kết quả lần chạy gần nhất

```
== Core     : 0 error, 0 warning                      (exit 0)
== Eos      : 0 error; 7 warning CS0649/CS0414        (exit 0)  (gồm EosBootstrap.cs, không thêm warning)
              (5 field test-seam internal chưa gán; 2 field của DevelopmentEosAuth khi không define UNITY_EDITOR)
== Eos +UNITY_EDITOR : 0 error                        (exit 0)
== FishNet  : 0 error; 1 warning CS0649 (playerPrefab là [SerializeField], gán trong Inspector)  (exit 0)
== Wiring   : 0 error                                  (exit 0)
dotnet test: Passed! Failed: 0, Passed: 113, Skipped: 0, Total: 113  (net10.0, NUnit 3)
```

Namespace đặt là `TeamNet.Multiplayer.FishNetEos` (không phải `.FishNet`) để không che namespace `FishNet` của thư viện; tương tự `.EditorTools` thay `.Editor` để không che `UnityEditor.Editor`. Một số file vẫn dùng `global::FishNet...` — vô hại.
