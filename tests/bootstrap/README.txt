These are isolated developer tests and MUST NOT be included in the Workshop payload.

BootstrapHarness.cs runs the actual init.lua with Noita's installed x86 LuaJIT DLL.
Compile this harness as noita.exe in a fixture directory, and put a compiled copy
of HarmlessCompanion.cs at fixture/mods/3835902155/companion/NoitaF5F8.bin with a
matching SHA256.txt. The harness redirects LOCALAPPDATA into fixture/profile and
the harmless companion records its arguments rather than interacting with Noita.

Verified on 2026-11-08:
- Complete bootstrap executes with Windows BCrypt and CreateProcessW.
- Unicode paths and spaces survive extraction and process arguments.
- Companion arguments are exactly --launch-game and the fixture executable.
- A second initialization reuses the existing verified executable unchanged.
- An invalid SHA256 manifest rejects the payload before process creation.
- Gameplay readiness stays unset until an alive player has enabled controls,
  then removes/adds its own marker exactly once with no repeated writes.

The real game process, saves, settings and Steam are never modified by these tests.
