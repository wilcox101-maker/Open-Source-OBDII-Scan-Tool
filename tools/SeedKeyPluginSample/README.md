# Seed/key plug-in sample

Some Ford modules only accept writes after UDS SecurityAccess (0x27). This project ships **no** seed/key algorithms or keys.
Instead the app can load a plug-in that you provide.

## Writing a plug-in
1. Copy this folder. Keep the `ObdLink.Core` reference with `Private="false"`.
2. Implement `ObdLink.Core.Uds.ISeedKeyProvider` in a public class with a parameterless constructor.
3. Return `null` for any module (`moduleTxId`) or `level` you do not handle. Never guess: each wrong key costs an attempt,
   and too many start a lockout delay (NRC 0x36/0x37).
4. `dotnet build -c Release`.

## Installing
Settings → **Security access (seed/key)** → turn on **Use seed/key plug-ins** → **Add plug-in…** → pick the `.dll` → type `TRUST`.
The file is copied to `Data\plugins\seedkey\` and approved by its SHA-256. Rebuilding changes the hash, so add it again after each build.
Set **As-Built level** (odd hex, for example `01` or `03`) to unlock before As-Built writes.

A plug-in runs inside the app with your user's rights. Only approve code you built or trust.

`DemoSeedKeyProvider` here only answers the DEMO simulator.
