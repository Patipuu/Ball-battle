# EOS config templates (PlayEveryWare EOS 6.1.0)

Placeholder copies of the JSON files the PlayEveryWare plugin reads from
`Assets/StreamingAssets/EOS/`. Key structure is identical to a working project; every
identifier, secret and name is replaced by a `<PLACEHOLDER>`. Non-secret flags keep the
values that ran in production (`isServer: false`, `authScopeOptionsFlags`,
`threadAffinity`, overlay settings, `schemaVersion`).

| Template | Copy to |
|---|---|
| `eos_product_config.template.json` | `Assets/StreamingAssets/EOS/eos_product_config.json` |
| `eos_android_config.template.json` | `Assets/StreamingAssets/EOS/eos_android_config.json` |
| `eos_ios_config.template.json` | `Assets/StreamingAssets/EOS/eos_ios_config.json` (iOS path is unverified) |

Other platforms (`windows`, `macos`, `linux`) use the same shape as the iOS file.

## Filling the placeholders

All values come from the Epic Developer Portal → your product → Product Settings.

| Placeholder | Where in the portal | Notes |
|---|---|---|
| `<PRODUCT_NAME>` | Product name | Free text |
| `<PRODUCT_ID>` | Product Settings → General | 32 hex chars |
| `<SANDBOX_ID>` | Sandboxes | Use the same id in `Sandboxes[].Value.Value`, `Deployments[].Value.SandboxId.Value` and every platform file's `deployment.SandboxId.Value` |
| `<SANDBOX_NAME>` | Sandboxes | Label only |
| `<DEPLOYMENT_ID>` | Deployments | Must belong to that sandbox |
| `<DEPLOYMENT_NAME>` | Deployments | Label only |
| `<CLIENT_ID>` | Clients | The *game client* credential, not a server one |
| `<CLIENT_SECRET>` | Clients | See security note below |
| `<CLIENT_NAME>` | Clients | Label only |
| `<ENCRYPTION_KEY_64_HEX>` | You generate it | 64 hex chars (32 random bytes), e.g. `openssl rand -hex 32`. The PEW config requires a valid 64-hex value. The EOS SDK uses it only for Player Data Storage and Title Storage (doc comment of `EOS_Platform_Options.EncryptionKey`); P2P and Lobby do not use it, so peers with different keys still connect. Use one value on every platform if you use those storages, so files written on one platform decrypt on another |

Every platform file must carry the same client/deployment as the product file. On Android,
`Assets/Plugins/Android/EOS/eos_dependencies.androidlib/` (including
`res/values/eos_values.xml`) is copied from the PEW package (`PlatformSpecificAssets~/EOS/Android/`)
by the plugin's Android pre-build step on every build, which then rewrites
`eos_login_protocol_scheme` to `eos.<client id in lowercase>` from the Android config. Do not
hand-edit it; just check after a build that the value matches your client id.

The Android file has two extra keys `GoogleLoginClientID` / `GoogleLoginNonce` (left `null`:
login uses an anonymous DeviceId, not Google).

## Never commit real values

- Keep the real files out of public repos and out of shared skill/template folders. If the game
  repo is private, committing them is the PlayEveryWare default; decide that per project.
- Before sharing anything derived from a real project, grep for 32-hex ids, the client id prefix
  and base64 secrets.

## The client secret is effectively public

A client build must contain `ClientId` + `ClientSecret` to log in, so anyone with the APK/IPA can
extract them. Treat them as public and limit what they can do:

- Give the game client a **least-privilege client policy** in the portal: only the interfaces the
  game uses (Connect, Lobby, P2P, RTC if voice). Never reuse a client whose policy allows
  server/trusted-server actions.
- Use separate sandboxes/deployments for dev and live; rotate the dev client if it leaks.
- Anything that must stay secret (economy, rewards, admin) belongs on a backend you control, not in
  EOS client calls.
