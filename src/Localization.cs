using System;
using System.Collections.Generic;

namespace MsiHardwareConsole
{
    internal static class Localization
    {
        public static bool Chinese { get; set; }

        public static string T(string english, string chinese)
        {
            return Chinese ? chinese : english;
        }

        public static string Error(string message)
        {
            if (Chinese || string.IsNullOrEmpty(message)) return message;
            var replacements = new Dictionary<string, string>
            {
                { "MSI WMI 接口没有返回成功状态。", "The MSI WMI interface did not return a success status." },
                { "MSI WMI 尚未连接。", "The MSI WMI interface is not connected." },
                { "读取失败。", " read failed." },
                { "写入失败。", " write failed." },
                { "风扇曲线必须包含七个速度点。", "The fan curve must contain seven speed points." },
                { "自定义曲线必须包含七个温度和转速点。", "The custom curve must contain seven temperature and speed points." },
                { "未知的 MSI 风扇模式。", "Unknown MSI fan mode." },
                { "固件没有确认狂暴散热状态。", "Firmware did not confirm Full Blast." },
                { "固件回读状态与所选模式不一致。", "Firmware read-back does not match the selected mode." },
                { "固件没有完整保存风扇曲线。", "Firmware did not preserve the complete fan curve." },
                { "固件没有完整保存温度节点。", "Firmware did not preserve all temperature points." }
            };
            foreach (var pair in replacements) message = message.Replace(pair.Key, pair.Value);
            return message;
        }
    }
}
