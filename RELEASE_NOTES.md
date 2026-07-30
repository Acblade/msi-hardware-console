# MSI Hardware Console 0.1.6

## English

This release redesigns the Thermal guard section without changing its protection logic.

Highlights:

- The large nested panel has been removed; the section now uses the page background directly
- Sustained heat, Emergency heat, and Restore curve are independent horizontal cards
- Each card presents its purpose, slider range, and current threshold in one clear row
- Detailed behavior and safety guidance now appears from the small `?` icon beside the Thermal guard title
- Existing ranges, 20-second confirmation periods, and automatic 3°C threshold separation are unchanged
- English and Simplified Chinese screenshots and documentation are updated

The verified hardware allowlist and firmware write boundaries are unchanged. The executable is unsigned; verify the attached SHA-256 file before running it.

## 中文

此版本重新设计“高温保护”界面，不改变任何保护逻辑。

主要内容：

- 移除包住全部内容的大型嵌套面板，板块直接使用页面背景
- “持续高温”“紧急高温”“恢复曲线”改为三张独立的横向卡片
- 每张卡片在同一行中清晰呈现用途、滑条范围和当前温度
- 完整行为说明与安全规则移到“高温保护”标题旁的小问号悬停提示中
- 原有可调范围、20 秒确认时间和自动 3°C 安全间隔保持不变
- 中英文截图与文档同步更新

已验证硬件白名单和固件写入边界没有变化。当前可执行文件没有代码签名，运行前请核对随 Release 提供的 SHA-256。
