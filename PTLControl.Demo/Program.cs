// ============================================================
// PTL LED Matrix Control System - Demo / Usage Example
// Developer: Ezio @ IDEMIA
// Description: Demonstrates how to call PTLController API
//              for material pick-to-light scenarios.
// ============================================================
using System;
using System.Threading;
using PTLControl.Compat;
using PTLControl.Compat.Models;

// ============================================================
// PTL 灯控调用示例
//
// 推荐接口（使用 LedColor 枚举，无需关心 RGB）：
//   PTLController.SetLight(key, LedColor.Green)           常亮
//   PTLController.SetBlink(key, LedColor.Red, 500)        闪烁
//   PTLController.TurnOff(key)                            熄灭单灯
//   PTLController.AllOff()                                全部熄灭
//   PTLController.Marquee(LedColor.Blue, 100)             跑马灯
//
// 预定义颜色（8色）：Red, Orange, Yellow, Green,
//                   Cyan, Blue, Purple, White
// ============================================================

Console.WriteLine("=== PTL 灯控调用示例 ===\n");

// 1. 选择串口
var ports = PTLController.GetPortNames();
if (ports.Length == 0)
{
    Console.WriteLine("未找到串口，请连接 Arduino 后重试。");
    Console.ReadKey();
    return;
}
Console.WriteLine("可用串口：");
for (int i = 0; i < ports.Length; i++)
    Console.WriteLine($"  [{i}] {ports[i]}");
Console.Write("\n请选择串口编号：");
var input = Console.ReadLine();
if (!int.TryParse(input, out int portIdx) || portIdx < 0 || portIdx >= ports.Length)
{
    Console.WriteLine("输入无效。");
    return;
}

// 2. 连接
PTLController.Connect(ports[portIdx]);
Console.WriteLine($"已连接 {ports[portIdx]}\n");

try
{
    var keys = PTLController.GetAllKeys();
    if (keys.Count < 2)
    {
        Console.WriteLine("演示至少需要 2 个已配置 Key，请先在映射中配置后重试。");
        return;
    }

    var keyA = keys[0];
    var keyB = keys[1];

    // ── 场景1：指示取料（绿色常亮）──────────────────────────────────
    Console.WriteLine($"[场景1] 指示取料 → 绿色常亮 Key={keyA}");
    PTLController.SetLight(keyA, LedColor.Green);
    Thread.Sleep(2000);

    // ── 场景2：同时指示另一个格口取料 ────────────────────────────────
    Console.WriteLine($"[场景2] 同时指示另一格口 → 绿色常亮 Key={keyB}");
    PTLController.SetLight(keyB, LedColor.Green);
    Thread.Sleep(2000);

    // ── 场景3：取错了！红色闪烁警告 ──────────────────────────────────
    Console.WriteLine($"[场景3] 取错物料 → 红色闪烁 Key={keyB}");
    PTLController.SetBlink(keyB, LedColor.Red, 500);
    Thread.Sleep(3000);

    // ── 场景4：纠正错误，关闭红灯 ────────────────────────────────────
    Console.WriteLine($"[场景4] 纠正完毕 → 关闭红灯 Key={keyB}");
    PTLController.TurnOff(keyB);
    Thread.Sleep(1000);

    // ── 场景5：取料正确，绿灯→蓝灯（表示已借走）─────────────────────
    Console.WriteLine($"[场景5] 取料正确 → 绿灯变蓝灯 Key={keyA}");
    PTLController.SetLight(keyA, LedColor.Blue);
    Thread.Sleep(2000);

    // ── 场景6：还回物料，熄灭蓝灯 ───────────────────────────────────
    Console.WriteLine($"[场景6] 还回物料 → 熄灭 Key={keyA}");
    PTLController.TurnOff(keyA);
    Thread.Sleep(1000);

    // ── 场景7：按 Key 操作（需要配置文件有数据）─────────────────────
    if (keys.Count > 0)
    {
        Console.WriteLine($"\n[场景7] 按 Key 操作，共 {keys.Count} 个 Key");
        Console.WriteLine($"  SetLight(\"{keys[0]}\", LedColor.Green)");
        PTLController.SetLight(keys[0], LedColor.Green);
        Thread.Sleep(1500);

        if (keys.Count > 1)
        {
            Console.WriteLine($"  SetBlink(\"{keys[1]}\", LedColor.Red, 400)");
            PTLController.SetBlink(keys[1], LedColor.Red, 400);
            Thread.Sleep(2000);
        }
    }

    // ── 场景8：跑马灯 ───────────────────────────────────────────────
    Console.WriteLine("\n[场景8] 跑马灯（蓝色，3秒后停止）");
    PTLController.Marquee(LedColor.Blue, 80);
    Thread.Sleep(3000);

    // ── 全部熄灭 ─────────────────────────────────────────────────────
    Console.WriteLine("\n[结束] 全部熄灭");
    PTLController.AllOff();
}
finally
{
    PTLController.AllOff();
    PTLController.Disconnect();
    Console.WriteLine("串口已断开。");
}

Console.WriteLine("\n按任意键退出...");
Console.ReadKey();
