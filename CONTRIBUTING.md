# Contributing / 参与贡献

## English

Bug fixes, translations, UI improvements, and evidence-backed compatibility reports are welcome.

Before opening a pull request:

1. Build with `.\build.ps1`.
2. Test English and Simplified Chinese layouts.
3. Keep unknown hardware in monitoring-only mode.
4. Do not broaden the fan-control allowlist without the evidence listed in `docs/COMPATIBILITY.md`.
5. Explain hardware impact and validation in the pull request.

## 中文

欢迎提交错误修复、翻译、界面改进和有证据支持的兼容性报告。

提交 Pull Request 前请运行 `.\build.ps1`，检查英文与简体中文界面，确保未知硬件仍处于仅监控模式，并且不要在缺少 `docs/COMPATIBILITY.md` 所列证据时扩大风扇控制白名单。
