# Compatibility / 兼容性

## Verified configuration

- Model: MSI Cyborg 15 A13VE
- Operating system: Windows x64
- MSI ACPI WMI interface: 2.8
- Fan layout: one controllable fan exposed by this implementation
- Verified operations: temperature read, RPM read, Automatic, seven-point curve write/read-back, fan-off, 30–60% normal duty, Full Blast, and elevated startup

This allowlist is intentionally strict. A similar product name, the same WMI class, or a successful read does not prove that writes are safe.

## Adding a model

A model should not be added until all of the following are recorded:

1. Exact manufacturer and model strings from Windows.
2. BIOS and EC firmware versions.
3. MSI ACPI WMI interface version.
4. Number of physical fans and every RPM value exposed by `Get_Fan`.
5. Automatic-mode value and successful restoration.
6. Curve write followed by byte-for-byte read-back.
7. RPM response at several safe temperature/duty points.
8. Full Blast enable, RPM response, disable, and recovery.
9. Reboot behavior and interaction with MSI Center if it is installed.

Open a compatibility report; do not submit unverified guesses directly to the allowlist.

## 已验证配置

- 机型：MSI Cyborg 15 A13VE
- 操作系统：Windows x64
- MSI ACPI WMI 接口：2.8
- 风扇结构：当前实现只验证了一个可控风扇
- 已验证操作：温度读取、RPM 读取、自动模式、七点曲线写入与回读、关闭风扇、30–60% 普通转速、狂暴散热、最高权限开机自启

白名单刻意保持严格。相似的产品名称、相同的 WMI 类或成功读取数据，都不能证明写入一定安全。

## 增加新机型

加入白名单前至少需要记录：Windows 完整机型字符串、BIOS/EC 版本、WMI 版本、物理风扇数量、自动模式恢复、曲线写入回读、多档 RPM 响应、狂暴散热启停恢复、重启行为，以及与 MSI Center 的交互情况。

请先提交兼容性报告，不要把未经验证的猜测直接加入白名单。
