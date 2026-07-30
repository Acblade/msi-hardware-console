# MSI Hardware Console 1.0.1

## English

Version 1.0.1 refines the relationship between Custom fan curves and Thermal guard.

- Restore, sustained, and emergency protection are now green, orange, and red points connected directly into every displayed fan curve
- The three Thermal guard controls are compact cards arranged in one row
- Their sliders are limited to 200 px to avoid excessive empty space
- Custom temperature points can be edited through 90°C, the verified MSI WMI2 firmware limit
- Custom points still allow 0%, 30–60%, or the discrete 100% Full Blast value
- Thermal guard remains independent and takes priority; a Custom setting cannot cancel active high-temperature protection
- Compatibility allowlisting, 3°C safety separation, timing rules, and firmware read-back checks remain enforced

The executable is unsigned; verify the attached SHA-256 file before running it.

## 中文

1.0.1 进一步梳理了自定义风扇曲线与高温保护之间的关系。

- 恢复、持续和紧急保护改为绿、橙、红三种节点，并直接接入每条曲线
- 三项高温保护恢复为同一行的三张紧凑卡片
- 滑动条限制为 200 像素，减少两端多余留白
- 自定义温度节点可编辑到 90°C，这是已经验证的 MSI WMI2 固件上限
- 自定义节点仍可选择 0%、30–60% 或独立的 100% 全速值
- 高温保护保持独立且拥有更高优先级，自定义设置不能解除正在生效的高温保护
- 兼容机型白名单、3°C 安全间隔、延迟规则和固件回读校验均保持不变

当前可执行文件没有代码签名，运行前请核对随 Release 提供的 SHA-256。
