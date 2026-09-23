# STVB UIHost Build

Repository này dùng để build và kiểm thử hồi quy `STVB.UIHost.dll` trên GitHub Actions Windows runner.

- Không lưu core VBA/STVB nội bộ.
- Target: .NET Framework 4.6 / WPF / AnyCPU.
- COM ProgID: `STVB.UIHost`.
- CLSID: `{14762D8B-9DC4-49BD-B33F-739BFA5D21BE}`.
- Artifact build: `STVB.UIHost.dll` + SHA-256 + báo cáo kiểm tra.
