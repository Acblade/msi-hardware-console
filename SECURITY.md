# Security and hardware safety / 安全与硬件风险

## English

This application communicates with laptop firmware. A bad model mapping or unsafe value can cause excessive heat, fan stoppage, unexpected firmware state, or other hardware behavior.

- Use releases only on explicitly verified hardware for fan control.
- Keep the computer attended during first-time testing.
- Return to Automatic mode immediately if RPM or temperature behavior is unexpected.
- Do not disable thermal protection, patch the allowlist blindly, or test while the system is under critical load.
- Unknown hardware remains monitoring-only by design.
- Security vulnerabilities should be reported privately through GitHub's security advisory feature, not as a public issue.

## 中文

本程序会与笔记本固件通信。错误的机型映射或不安全数值可能导致温度过高、风扇停止、固件状态异常或其他硬件问题。

- 只有明确列入已验证清单的硬件才能使用风扇控制。
- 首次测试时不要让电脑无人看管。
- RPM 或温度行为异常时立即恢复自动模式。
- 不要关闭温度保护、盲目修改白名单，也不要在系统处于临界高负载时测试。
- 未知硬件按设计保持仅监控模式。
- 安全漏洞请通过 GitHub Security Advisory 私下报告，不要公开提交 issue。
