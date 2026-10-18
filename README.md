# AirBridge for Windows

A Windows desktop controller for [UxPlay](https://github.com/FDH2/UxPlay), supporting compatible iPhone screen mirroring and audio.

## Download

GitHub Actions builds a **self-contained AirBridge UI** for Windows x64 and attaches it to tagged Releases. **This is not yet a turnkey AirPlay receiver installer:** UxPlay and its required DLLs are not included. Do not advertise the ZIP as standalone AirPlay support.

## Use

1. Install/build a working Windows UxPlay receiver separately (MSYS2 UCRT64 is one option).
2. Download the release ZIP and extract it.
3. Run `AirBridge.exe` and browse to `uxplay.exe` if needed.
4. Ensure the UxPlay runtime DLLs are available in its folder or `C:\msys64\ucrt64\bin`.
5. Connect the PC and iPhone to a network allowing multicast discovery and local device traffic.
6. Start receiver, then select AirBridge in iPhone Control Centre > Screen Mirroring.

The app defaults to a future portable `backend/uxplay.exe` folder beside `AirBridge.exe`. The backend is not bundled by this workflow. If using MSYS2, select your actual built UxPlay executable in the app.

## Build locally

Install .NET 8 SDK and run `powershell -ExecutionPolicy Bypass -File .\build.ps1`.

## Publishing

Push a tag such as `v0.2.1`. The `Build Windows app` workflow creates the UI release asset. You may also trigger it manually from Actions.

## Redistributing UxPlay

UxPlay is licensed under GPL-3.0. Before bundling it and third-party libraries, comply with applicable source-code, copyright-notice, and licence requirements. This repository package includes only the AirBridge UI source, not UxPlay or DLLs.

## Known limitations

- Native AirDrop is not supported.
- iPhone control from Windows is not supported.
- Protected/DRM video may not play.
- Release builds have not been validated on a clean Windows machine.
