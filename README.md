<div align="center">

<img src="HNice/Assets/HNice.png" alt="HNice logo" width="96" />

# HNice

**A packet logger and manipulator for Habbo Origins**, heir to the old SnG Fun v1 (VB6).

[![CI](https://github.com/Juanru9/HNice/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Juanru9/HNice/actions/workflows/ci.yml)
[![Coverage](https://raw.githubusercontent.com/Juanru9/HNice/badges/coverage.svg)](https://github.com/Juanru9/HNice/actions/workflows/ci.yml)
[![Downloads](https://img.shields.io/github/downloads/Juanru9/HNice/total?label=downloads)](https://github.com/Juanru9/HNice/releases)
[![Latest release](https://img.shields.io/github/v/release/Juanru9/HNice?include_prereleases&label=release)](https://github.com/Juanru9/HNice/releases/latest)
<br />
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform: Windows](https://img.shields.io/badge/platform-Windows%20x64-0078D4?logo=windows)](#download)
[![License: MIT](https://img.shields.io/github/license/Juanru9/HNice)](LICENSE)
[![Last commit](https://img.shields.io/github/last-commit/Juanru9/HNice)](https://github.com/Juanru9/HNice/commits/main)

[Download](#download) · [Features](#features) · [Usage](#usage) · [Build from source](#build-from-source)

</div>

---

HNice sits between the Habbo Origins client and the game server. It decrypts the traffic, shows every packet in both directions and lets you inject packets toward your client or the server. Small built-in tools sit next to the log, so you never lose sight of the traffic.

> [!NOTE]
> HNice is in an early stage. Expect rough edges and breaking changes between versions.

## Download

1. Open the [latest release](https://github.com/Juanru9/HNice/releases/latest) and download `HNice-<version>-win-x64.zip`.
2. Unzip it anywhere and run `HNice.exe`.

The download is self-contained: no .NET install is needed. It runs on 64-bit Windows 10 and 11.

## Features

**Packet log**
- Live log of both directions, with packet names and control characters shown as `[2]`, `[9]`, `[13]`.
- Decrypts the Origins key exchange.
- Resend or edit any packet, toward the client or the server.

**Tools** (one panel beside the log)

| Group | Tool | What it does |
| --- | --- | --- |
| Hotel | Credits | Show any balance in your purse (your screen only). |
| | Badges | Add badges to your badge list (your screen only). |
| Room | Drinks | Get a real drink from the server, or drop a drink machine at your feet. |
| | Furni | Browse the furni catalogue and place any item next to you (your screen only). |
| | Posters | Hang a poster on a wall (your screen only). |
| | Warp | Move your avatar to any tile. |
| | Rejoin | Go straight back into a room when you get kicked from it. |
| | Wobble | Follow Wobble Squabble rounds and play yours automatically. |
| People | Imitate | Change how your own avatar looks, or save the look for real through the wardrobe. |
| | Mime | Copy another Habbo's walking, gestures and chat. Everyone sees it. |
| | Spawn user | Add a fake user or bot to the room (your screen only). |
| Staff | Messages | Fake hotel alerts and mod warnings (your screen only). |
| | Fuse rights | Unlock staff UI in your client. The server still checks real rights. |
| Utilities | Diagnostics | Check the redirect, the server, the proxy and the key exchange. |
| | Encoder | Convert numbers to B64 and VL64 and back. |

Every tool says whether it only changes your own screen or sends a real request to the server.

## Usage

1. Run HNice **as administrator**: it points the game host (e.g. `game-oes.habbo.com`) at itself through the Windows `hosts` file.
2. Pick the hotel, check the resolved server address and click **Connect**.
3. Log into Habbo Origins through the official launcher. The traffic shows up in the log.

If nothing appears, open **Diagnostics** to check the redirect, the server, the proxy and the key exchange.

<details>
<summary>Developer flags</summary>

| Flag | Effect |
| --- | --- |
| `--packet-trace <file>` | Writes every packet, both directions, one line each, to a file (or set `HNICE_PACKET_TRACE`). |
| `--command-file <file>` | Runs each line appended to the file: `server <packet>`, `client <packet>`, `catalog fetch [pages]`. |

The packet trace includes your login packet, so keep trace files private.

</details>

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.

```sh
git clone https://github.com/Juanru9/HNice
cd HNice
dotnet build HNice.sln -c Release
dotnet run --project HNice -c Release
```

Run the tests, with coverage:

```sh
dotnet test HNice.Test --collect:"XPlat Code Coverage"
```

### Releases

Pushing a tag such as `v0.1.0` runs the [Release workflow](.github/workflows/release.yml). It tests, publishes a self-contained single-file build and attaches the zip to a GitHub release. Tags with a suffix (`v0.1.0-beta.1`) become pre-releases.

## Disclaimer

HNice is a fan project, not affiliated with or endorsed by Sulake. Habbo and Habbo Origins are trademarks of Sulake Oy. Modifying game traffic may break the Habbo Way or the terms of service; use it on your own account and at your own risk.

## License

[MIT](LICENSE) © Juanru9
