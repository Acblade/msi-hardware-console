# MSI Hardware Console 0.1.1

## English

This maintenance release makes the fan controls more truthful and less prone to unnecessary noise.

Highlights:

- Fixed mode's button now turns the fan off and back on
- The Fixed slider is visibly disabled while off and restores the previous duty when re-enabled
- Automatic mode no longer presents guessed percentage points as a firmware curve
- Normal modes require 10 seconds at their final high-temperature point before Full Blast
- Extreme heat still triggers immediate protection; release also uses a 10-second cool-down guard
- English-default UI with instant Simplified Chinese switching remains available
- Fan writes remain locked to the verified MSI Cyborg 15 A13VE with MSI WMI 2.8

Important: the executable is unsigned. Windows may display an unknown-publisher warning. Check the attached SHA-256 file before running it.

## 中文

这是一次风扇控制维护更新，重点是让行为更真实，并减少不必要的突然满速噪音。

主要内容：

- 固定模式按钮现在可以关闭风扇，也可以重新开启
- 关闭时固定转速滑条禁用变灰，重新开启后恢复上次转速
- 自动模式不再把猜测的百分比显示成固件曲线
- 普通模式到达末端高温点持续 10 秒后才进入狂暴散热
- 极高温仍会立即保护，退出全速也加入 10 秒降温确认
- 继续保持默认英文和即时简体中文切换
- 风扇写入仍只对已验证的 MSI Cyborg 15 A13VE、MSI WMI 2.8 开放

注意：当前可执行文件没有代码签名，Windows 可能显示未知发布者警告。运行前请核对随 Release 提供的 SHA-256 文件。
