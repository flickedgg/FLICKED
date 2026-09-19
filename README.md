# FLICKED

<img src="./assests/icon.png" align="right" alt="FLICKED icon" title="FLICKED icon" width="120">

[![Latest release](https://img.shields.io/github/v/release/viix0dev/FLICKED?include_prereleases&label=download)](https://github.com/viix0dev/FLICKED/releases)
![Status](https://img.shields.io/badge/status-alpha-orange)

**FLICKED** is a free open-source self-hostable competitive platform for CS2 - matchmaking, dedicated-server orchestration, rankings, demos, statistics.

> **Alpha software.** Things will change and break. If something's off, that's worth
> reporting, not assuming is expected.

---

# Tech Stack:
Website: Next.js & Tailwind

Backend: C#, .NET

Database: Postagres SQL & Redies (Cache)

Launcher: Rust + Tauri

Anti-Cheat : Planned C++


# Stracture:
> Every folder contains documentation describing the full system architecture,
> including how it works, handles security, processes data and requests,
> and common error codes with solutions.

**./assests/** Assests used in this project like photos, logos etc...

**./website/** Public website source code.

**./backend/** FLICKED backend source code.

**./database/** FLICKED database files & settings.

**./launcher/** FLICKED launcher source code.
