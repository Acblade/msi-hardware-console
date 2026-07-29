# MSI Hardware Console 0.1.3

## English

This release separates ordinary fan curves from emergency 100% cooling and makes the thermal guard configurable.

Highlights:

- Ordinary Silent, Balanced, Boost, Fixed, and Custom curves are capped at 60%
- 100% fan speed is reserved for manual Full Blast or the independent thermal guard
- The Fan control heading now includes adjustable sustained, immediate, and release temperatures
- Safe ranges are enforced, with at least 3°C between the sustained threshold and the other two thresholds
- Fan-curve overlays now focus only on their temperature-to-duty curves
- The verified hardware allowlist is unchanged

Defaults are 92°C for 20 seconds, 97°C immediately, and 87°C for 20 seconds to release. The executable is unsigned; verify the attached SHA-256 file before running it.

## 中文

此版本把普通风扇曲线与紧急 100% 散热分离，并允许用户调整高温保护温度。

主要内容：

- 静音、均衡、强冷、固定和自定义的普通曲线最高为 60%
- 100% 只保留给手动“狂暴散热”或独立高温保护
- “风扇控制”标题旁新增持续触发、立即触发和退出保护三个温度设置
- 设置带有安全范围，并自动保证持续阈值与另外两个阈值至少相差 3°C
- 风扇曲线浮层只显示温度—转速曲线，不再混入高温保护说明
- 已验证硬件白名单没有变化

默认值为：92°C 持续 20 秒开启、97°C 立即开启、降到 87°C 持续 20 秒退出。当前可执行文件没有代码签名，运行前请核对随 Release 提供的 SHA-256。
