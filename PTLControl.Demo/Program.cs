using PTLControl.Compat;
using PTLControl.Compat.Models;

Console.WriteLine("=== PTLControl.Compat 最简调用示例 ===");

// 应用初始化时只调用一次。不需要枚举或选择 COM 口。
if (!PTLController.Connect())
{
    var message = "PTL 连接失败，请检查 HardwareHost 是否启动。\r\n\r\n"
        + PTLController.LastConnectionMessage;

    MessageBox.Show(message, "PTL 连接失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
    Console.WriteLine(message);
    return;
}

Console.WriteLine(PTLController.LastConnectionMessage);

try
{
    var keys = PTLController.GetAllKeys();
    if (keys.Count == 0)
    {
        const string message = "没有已配置的灯位 Key，请先完成灯位映射配置。";
        MessageBox.Show(message, "PTL Demo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        Console.WriteLine(message);
        return;
    }

    var key = keys[0];

    Console.WriteLine($"点亮 {key}");
    PTLController.SetLight(key, LedColor.Green);
    Thread.Sleep(1500);

    Console.WriteLine($"闪烁 {key}");
    PTLController.SetBlink(key, LedColor.Red, 500);
    Thread.Sleep(2000);

    Console.WriteLine($"熄灭 {key}");
    PTLController.TurnOff(key);

    Console.WriteLine("全部熄灭");
    PTLController.AllOff();
}
catch (Exception ex)
{
    MessageBox.Show(ex.Message, "PTL 操作失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
    Console.WriteLine("操作失败：" + ex.Message);
}
finally
{
    // 只断开当前调用方，HardwareHost 仍然持有物理串口。
    PTLController.Disconnect();
}

Console.WriteLine("演示完成，按任意键退出。");
Console.ReadKey();
