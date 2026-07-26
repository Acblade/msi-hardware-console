# MSI Hardware Console 0.1.2

## English

This maintenance release corrects how the main window interacts with the taskbar and notification area.

Highlights:

- The title-bar minimize button now minimizes to the Windows taskbar instead of hiding the app in the notification area
- Closing the window still hides it in the notification area
- A window closed while maximized reopens maximized instead of being forced back to the adaptive default size
- Normal-size windows also keep their existing size while hidden
- Fan-control behavior and the verified hardware allowlist are unchanged

Important: the executable is unsigned. Windows may display an unknown-publisher warning. Check the attached SHA-256 file before running it.

## 中文

这是一次窗口行为维护更新，修正任务栏最小化与系统托盘之间的区别。

主要内容：

- 点击标题栏最小化按钮时，应用现在只会进入 Windows 任务栏，不再隐藏到托盘
- 点击关闭按钮仍会把窗口隐藏到系统托盘
- 最大化窗口关闭到托盘后，再次打开时仍保持最大化，不再强制回到默认尺寸
- 普通窗口隐藏后也会保留现有尺寸
- 风扇控制行为与已验证硬件白名单没有变化

注意：当前可执行文件没有代码签名，Windows 可能显示未知发布者警告。运行前请核对随 Release 提供的 SHA-256 文件。
