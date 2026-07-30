# MSI Hardware Console 0.1.5

## English

This release corrects the page hierarchy, curve scale, and adjustable thermal-guard ranges.

Highlights:

- Thermal guard is now a top-level page section after all Fan control modes and before Startup
- Ordinary fan-curve charts use their real 0–60% range and no longer display the unavailable 60–100% area
- Full Blast remains the only curve view that displays 0–100%
- Sustained heat is adjustable from 85–95°C
- Emergency heat is adjustable from 90–100°C
- Curve restoration is adjustable from 70–92°C
- Automatic 3°C separation still prevents conflicting or unsafe threshold combinations
- English and Simplified Chinese screenshots and documentation are updated

The verified hardware allowlist and firmware write boundaries are unchanged. The executable is unsigned; verify the attached SHA-256 file before running it.

## 中文

此版本修正页面层级、曲线纵轴和高温保护的可调范围。

主要内容：

- “高温保护”现在是与“性能概览”“硬盘空间”“风扇控制”同级的页面板块，位于全部风扇模式之后、“启动与托盘”之前
- 普通风扇曲线图只显示真实可用的 0–60%，不再显示无法调整的 60–100% 区域
- 只有“狂暴散热”的曲线图继续显示 0–100%
- “持续高温”可调范围扩大到 85–95°C
- “紧急高温”可调范围扩大到 90–100°C
- “恢复曲线”可调范围扩大到 70–92°C
- 系统仍会自动保持 3°C 安全间隔，避免阈值冲突
- 中英文截图与文档同步更新

已验证硬件白名单和固件写入边界没有变化。当前可执行文件没有代码签名，运行前请核对随 Release 提供的 SHA-256。
