# MSI Hardware Console 0.1.4

## English

This release refines the thermal-guard layout and fan-mode explanations introduced in 0.1.3.

Highlights:

- Thermal-guard controls now live in a dedicated panel below the Fan control heading
- Sustained heat, emergency heat, and curve restoration each show their timing, action, safe range, and current temperature clearly
- The panel explains the automatic 3°C safety separation without mixing it into fan-curve overlays
- Automatic, Custom, Silent, Balanced, Fixed, and Boost descriptions now explain their real behavior and recommended workloads
- Full Blast now clearly states that it runs at 100% and restores the previous mode when clicked again
- English and Simplified Chinese screenshots and documentation are updated

The verified hardware allowlist and fan-control safety limits are unchanged. The executable is unsigned; verify the attached SHA-256 file before running it.

## 中文

此版本优化了 0.1.3 新增的高温保护布局与风扇模式说明。

主要内容：

- 高温保护设置现在位于“风扇控制”标题下方的独立板块中
- “持续高温”“紧急高温”“恢复曲线”分别清楚显示等待时间、执行动作、安全范围和当前温度
- 独立板块会说明自动保持 3°C 安全间隔的规则，风扇曲线浮层仍只显示曲线
- 自动、自定义、静音、均衡、固定、强冷的说明改为明确描述实际行为和适用场景
- 狂暴散热明确说明会立即以 100% 运行，再次点击恢复此前模式
- 中英文截图与文档同步更新

已验证硬件白名单和风扇控制安全限制没有变化。当前可执行文件没有代码签名，运行前请核对随 Release 提供的 SHA-256。
