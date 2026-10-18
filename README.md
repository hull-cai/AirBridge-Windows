# AirBridge for Windows

AirBridge is an experimental Windows desktop AirPlay receiver application built on [UxPlay](https://github.com/FDH2/UxPlay). Mirror an iPhone or iPad screen and audio onto a Windows PC.

## Download the portable version

Go to **Actions → Build standalone Windows AirBridge → Run workflow → Run workflow**, then download the `AirBridge-Windows-Portable-x64` artifact once the build succeeds.

For published releases, see [Releases](../../releases).

1. Download `AirBridge-Windows-Portable-x64.zip`.
2. Extract the **entire ZIP** to a regular folder (do not run from inside the ZIP).
3. Double-click `AirBridge.exe`.
4. Click **Start receiver**. Permit local network access in Windows Firewall if prompted.
5. Connect your iPhone and Windows PC to the same trusted local network, with client-to-client traffic and multicast discovery allowed.
6. On iPhone: Control Centre → Screen Mirroring → AirBridge.

You **do not need MSYS2 or the .NET SDK** on the target PC for the portable build if the bundled runtime passes checks.

**Status:** Experimental; the CI bundle and playback have not yet been validated on a clean Windows installation. Please report errors with app logs and Actions build results.

## Cast to TV (experimental beta)

The portable build adds **Find TVs**, **Cast screen**, and **Stop casting**.
Supports discovery of **classic Chromecast** devices as well as newer Google Cast receivers via pychromecast; **Google TV is not required**.

1. Launch AirBridge and start mirroring your iPhone.
2. Maximise the iPhone mirror window on your Windows desktop.
3. Ensure Chromecast and Windows PC are on a trusted network that allows device discovery and local HTTP traffic.
4. Click **Find TVs**, select the Chromecast, then **Cast screen**.
5. Click **Stop casting** to end the stream.

**Beta limitations:** This first implementation casts the whole Windows desktop at up to 720p/25fps with H.264/HLS (not just the UxPlay window). iPhone audio is not relayed to the Chromecast by default. Audio routing, screen-only capture, and reducing latency are future work. Expect several seconds of delay. Older Chromecast compatibility is a goal, not a guarantee; live HLS support varies, and discovery can be blocked by guest Wi-Fi isolation. Keep the PC on a private/trusted network; the bridge hosts an unencrypted local HTTP stream accessible to devices on the LAN. This is not a way to cast DRM-protected content.

## Files

- `Program.cs`: AirBridge WinForms UI and UxPlay launcher.
- `AirBridge.csproj`: .NET 8 Windows desktop app project.
- `packaging/bundle-uxplay.ps1`: copies UxPlay, curated GStreamer plugins, and transitive MSYS2 DLL dependencies.
- `.github/workflows/release.yml`: Windows CI build, runtime smoke tests and portable ZIP packaging.
- `build.ps1`: local UI-only build.

The portable folder has this structure:

```text
AirBridge.exe
backend/
  uxplay.exe
  *.dll
  gstreamer-1.0/
    libgst*.dll
  gst-plugin-scanner.exe
  gst-inspect-1.0.exe
licenses/
```

## Local UI-only build

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

This **only** compiles the AirBridge UI; it will require an existing UxPlay installation. The GitHub Actions build includes the backend.

## Building a tagged GitHub Release

Push a tag starting with `v` to trigger the workflow and attach the portable ZIP to a Release. Run workflow manually to test before publishing. Workflow failures mean the bundle is not ready to distribute.

## Technical and legal notes

- UxPlay 1.74+ includes its own mDNS implementation, avoiding the older Bonjour SDK requirement. <https://github.com/FDH2/UxPlay>
- UxPlay is GPL-3.0 licensed; the build retrieves its source from upstream. Before public redistribution, make sure to satisfy UxPlay and third-party GStreamer/MSYS2 licence requirements, preserving notices and providing corresponding source for the exact binaries.
- AirBridge is not affiliated with Apple.
- DRM-protected content may not mirror.
- There is no native AirDrop or iPhone remote-control functionality.
- AirPlay discovery may not work on guest/client-isolated Wi-Fi networks.
- Windows x64 builds only.
