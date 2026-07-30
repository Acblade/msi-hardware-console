# MSI Hardware Console 0.1.8

## English

This release corrects the scope of the previous Thermal guard width refinement.

- The safety constraint is now shown as small text directly beneath the Thermal guard title
- Hover help contains only the relationship between Thermal guard and ordinary fan curves
- The complete three-card control group is capped at 960 px, rather than limiting only the sliders
- The cards still shrink responsively on narrower windows
- Ordinary fan-curve charts show compact broken-axis bands for 0–30% and 60–100%, while keeping 30–60% as the main editing area
- Editable points remain limited to 0% or 30–60%
- Protection logic and hardware compatibility are unchanged

The executable is unsigned; verify the attached SHA-256 file before running it.

## 中文

此版本修正上个版本对“高温保护”宽度要求的理解。

- 安全约束以小字直接显示在“高温保护”标题下方
- 问号悬停提示只保留高温保护与普通风扇曲线之间的关系
- 限制为最大 960 像素的是三张设置卡片整体，而不只是其中的滑条
- 较窄窗口下仍会自适应缩短
- 普通风扇曲线以压缩断轴显示 0–30% 和 60–100%，主要绘图区仍保留给 30–60%
- 可编辑节点仍只能设为 0% 或 30–60%
- 高温保护逻辑和硬件兼容范围均未改变

当前可执行文件没有代码签名，运行前请核对随 Release 提供的 SHA-256。
