# 内存卫士 (Memory Guardian; formerly 地球卫士) — Attribution and modification notice

Version 0.6.0, modified 2026-09-26 by 宋域强: renamed the product to 内存卫士,
added memory-load-dependent blue-to-red globe textures and download-page links
in the context menu and About dialog. Kept application/installer identities for
compatibility. Added an independent download website with a developer story and
developer-supplied portrait. Website artwork was composed locally from the
supplied photograph and existing Natural Earth map; no AI image service was used.

Version 0.5.1: visible automatic-cleaning countdown/cooldown status and isolated
WPF test host; cleanup timing rules remain unchanged.

Installer packaging added 2026-09-26: Chinese setup wizard, destination selection,
desktop/start-menu shortcuts, .NET 4.8.1 preflight, uninstall registration and
startup-task cleanup restricted to tasks targeting the installed executable.
The installer is built with Inno Setup 6.4.3 by Jordan Russell and Martijn Laan
(https://jrsoftware.org/). Its original runtime copyright notices are preserved.
`installer/ChineseSimplified.isl` comes from the Inno Setup `is-6_4_3` source tree;
translation author notices remain in the file. Our installer scripts are GPL v3.

Version 0.5.0, modified 2026-09-26: added an automatic-cleaning checkbox,
draggable 60–95 percent threshold slider, persisted cooldown and strength options,
automatic status and trigger reporting. New installs enable balanced automatic
cleaning at 80 percent sustained for 30 seconds with a 10-minute cooldown.
Existing enabled/disabled preferences are preserved. Missing samples reset the
sustained-pressure observation; failures retain the cooldown.

Version 0.4.0, modified 2026-09-26: added one-click full cleanup across seven
upstream memory areas, step progress and continued execution after individual
failures. Full standby replaces low-priority standby in manual full mode;
automatic mode remains gentle. System file cache uses the public API already
used by upstream. Volume flushing uses FlushFileBuffers without upstream's
optional cache-discard/write-order IOCTLs. Native errors are reported per step;
volume errors include each failed drive. FullMemoryBackend.cs is adapted from
upstream ComputerService.cs under GPL v3.

Version 0.3.0, modified 2026-09-26: manual optimization now also invokes upstream's
MemoryEmptyWorkingSets command (2); automatic optimization remains limited to
low-priority standby cache (5). Added step-by-step success/failure reporting,
before/after system memory measurements, a local report, and visible cooldown feedback.
Working-set trimming does not free committed allocations or guarantee performance gains.

Version 0.2.1, modified 2026-09-26: restored pointer hit testing with a transparent
circular input surface above the non-interactive 3D layers. Added regression checks
for the center and multiple visible points on the globe. This fixes mouse clicks,
drag gestures and context-menu input that failed in version 0.2.0.

Version 0.2.0, modified 2026-09-26: renamed the MemoryOrb front end to 地球卫士,
added a textured WPF 3D Earth, globe icon and optimization rotation, and added
the author homepage https://blog.csdn.net/syq10086?type=blog in the context menu
and About dialog. The original upstream attribution below remains applicable.

The land geometry in `orb/Assets/natural-earth-land.geojson` is Natural Earth
1:110m land data, public domain: https://www.naturalearthdata.com/about/terms-of-use/
Source: https://github.com/nvkelso/natural-earth-vector/blob/master/geojson/ne_110m_land.geojson
The texture and globe icon were generated from that geometry by
`scripts/create-earth-assets.ps1`. The globe is a decorative visualization, not
a political-boundary map. No runtime network download is used for the globe.

MemoryOrb is a modified/derived work based on **Windows Memory Cleaner** by
**Igor Mundstein and contributors**, distributed under GNU GPL version 3.

- Upstream: https://github.com/IgorMundstein/WinMemoryCleaner
- Source revision: `a19f25719c9b9a58a0acea99cb8adb4737d2d318` (version 3.0.8)
- Modification date: **2026-09-26**
- License: [GNU GPL v3](LICENSE)

The original `src/` and documentation are retained. MemoryOrb links the original
`src/Interop/NativeMethods.cs` and `src/Core/Structs.cs`. Its memory backend adapts
the low-priority standby-list method from `src/Service/ComputerService.cs`, adds
correct NTSTATUS reporting and privilege checks/restoration, and replaces the
application front end with a transparent WPF orb in `orb/`.

New work includes the orb UI and icon, asynchronous optimization, status feedback,
drag and position persistence, tray recovery, optional pressure-triggered cleaning,
per-user startup task handling, tests, and build/release documentation.

The original upstream CI/release workflows are archived under `docs/upstream/github/`
so a new repository does not run the original publisher's deployment workflows.
The original README is preserved in `docs/upstream/README.md` (its relative image
links refer to the original repository layout).

MemoryOrb is an independent derivative, not an official upstream release or endorsement.
No upstream publisher signing identity is used. The resulting EXE is unsigned.

When distributing the EXE, retain this notice and LICENSE and provide complete
corresponding source for that exact build under GPL v3, including changes and
build scripts. Recipients retain the right to modify and redistribute under GPL v3.
The software is provided without warranty as described in LICENSE.
