# MSI Hardware Console 1.0.0

## English

Version 1.0.0 completes the Thermal guard and fan-curve interaction design.

- The complete Thermal guard group is left-aligned and capped at 820 px
- The safety constraint remains visible beneath the section title
- Every fan-curve chart shows the configured restore, sustained, and emergency guard temperatures as colored dashed lines
- Ordinary curves keep compact broken-axis bands for 0–30% and 60–100%
- Custom points allow 0%, 30–60%, or the discrete 100% Full Blast value
- Reaching a custom 100% point actively enables MSI Full Blast instead of relying only on the stored firmware curve value
- Custom Full Blast releases after remaining 3°C below its selected temperature for 20 seconds
- The independent Thermal guard remains active at the same time
- Protection ranges, compatibility allowlist, and firmware read-back checks remain enforced

The executable is unsigned; verify the attached SHA-256 file before running it.

## 中文

1.0.0 完成了高温保护与风扇曲线之间的交互设计。

- 高温保护整体左对齐，最大宽度收窄至 820 像素
- 安全约束继续显示在标题下方
- 所有风扇曲线都用彩色虚线显示当前设置的恢复、持续和紧急高温保护温度
- 普通曲线继续以压缩断轴显示 0–30% 和 60–100%
- 自定义节点可以选择 0%、30–60% 或独立的 100% 全速值
- 到达自定义 100% 节点温度时，应用会主动启用 MSI 全速散热，不再只依赖固件曲线数值
- 温度低于该节点 3°C 并持续 20 秒后退出自定义全速
- 独立高温保护仍会同时生效
- 保护范围、兼容机型白名单和固件回读校验均保持不变

当前可执行文件没有代码签名，运行前请核对随 Release 提供的 SHA-256。
