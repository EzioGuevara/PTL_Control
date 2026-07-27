' ============================================================
' PTL LED Matrix Control System - VB.NET 4.7.2 调用示例
' Developer: Ezio Li @ IDEMIA
' 
' 前置条件：
'   1. 将 PTLControl.Compat.dll（及依赖项）复制到你的 VB 项目 bin 目录
'   2. 在 VB 项目中"添加引用" → 浏览 → 选择 PTLControl.Compat.dll
'      （同时添加 Newtonsoft.Json.dll；.NET Framework 下 System.IO.Ports 为内置）
'   3. 目标框架：.NET Framework 4.7.2 或更高
' ============================================================
Imports PTLControl.Compat
Imports PTLControl.Compat.Models

Module PTLLightDemo

    Private Sub OnTagEvent(sender As Object, ev As TagEventArgs)
        Dim key As String = PTLController.GetKeyByTagId(ev.TagId, ev.Group)
        If String.IsNullOrWhiteSpace(key) Then key = "(未映射)"

        If ev.EventType = TagEventType.Heartbeat Then
            Console.WriteLine(String.Format(
                "[回传-心跳] time={0:HH:mm:ss} eStationId={1} raw={2}",
                ev.ReceivedAtUtc.ToLocalTime(),
                ev.EStationId,
                ev.RawPayload))
            Return
        End If

        Console.WriteLine(String.Format(
            "[回传-{0}] time={1:HH:mm:ss} tagId={2} group={3} key={4} rgb={5}{6}{7} off={8} battery={9:0.0}V raw={10}",
            ev.EventType.ToString(),
            ev.ReceivedAtUtc.ToLocalTime(),
            ev.TagId,
            ev.Group,
            key,
            If(ev.R, 1, 0),
            If(ev.G, 1, 0),
            If(ev.B, 1, 0),
            ev.IsOff,
            ev.BatteryVoltage,
            ev.RawPayload))
    End Sub

    Sub Main()
        Console.WriteLine("=== PTL 灯控 VB.NET 调用示例 ===")
        Console.WriteLine()
        Console.WriteLine("已开启 TagEventReceived 监听（按键/通信/心跳回传将实时打印）")
        Console.WriteLine("可用颜色枚举：Red / Orange / Yellow / Green / Cyan / Blue / Purple / White")
        AddHandler PTLController.TagEventReceived, AddressOf OnTagEvent

        ' ── 1. 列出可用串口 ──────────────────────────────────────────
        Dim ports As String() = PTLController.GetPortNames()
        If ports.Length = 0 Then
            Console.WriteLine("未找到串口，请连接 Arduino 后重试。")
            Console.ReadKey()
            Return
        End If

        Console.WriteLine("可用串口：")
        For i As Integer = 0 To ports.Length - 1
            Console.WriteLine(String.Format("  [{0}] {1}", i, ports(i)))
        Next

        Console.Write(vbCrLf & "请选择串口编号：")
        Dim input As String = Console.ReadLine()
        Dim portIdx As Integer
        If Not Integer.TryParse(input, portIdx) OrElse portIdx < 0 OrElse portIdx >= ports.Length Then
            Console.WriteLine("输入无效。")
            Return
        End If

        ' ── 2. 连接串口 ──────────────────────────────────────────────
        PTLController.Connect(ports(portIdx))
        Console.WriteLine(String.Format("已连接 {0}", ports(portIdx)))
        Console.WriteLine()

        Try
            ' ── 场景1：绿色常亮（指示取料）──────────────────────────
            Console.WriteLine("[场景1] 绿色常亮 Layer=1, Index=0")
            PTLController.SetLight(1, 0, LedColor.Green)
            Threading.Thread.Sleep(2000)

            ' ── 场景2：同时点亮另一颗灯 ─────────────────────────────
            Console.WriteLine("[场景2] 同时点亮另一颗 Layer=1, Index=3")
            PTLController.SetLight(1, 3, LedColor.Green)
            Threading.Thread.Sleep(2000)

            ' ── 场景3：红色闪烁警告 ─────────────────────────────────
            Console.WriteLine("[场景3] 红色闪烁 Layer=1, Index=5，间隔500ms")
            PTLController.SetBlink(1, 5, LedColor.Red, 500)
            Threading.Thread.Sleep(3000)

            ' ── 场景4：关闭红灯 ──────────────────────────────────────
            Console.WriteLine("[场景4] 关闭红灯")
            PTLController.TurnOff(1, 5)
            Threading.Thread.Sleep(1000)

            ' ── 场景5：蓝色（已借走）────────────────────────────────
            Console.WriteLine("[场景5] 蓝色常亮 Layer=1, Index=0")
            PTLController.SetLight(1, 0, LedColor.Blue)
            Threading.Thread.Sleep(2000)

            ' ── 场景6：橙色常亮 ─────────────────────────────────────
            Console.WriteLine("[场景6] 橙色常亮 Layer=1, Index=2")
            PTLController.SetLight(1, 2, LedColor.Orange)
            Threading.Thread.Sleep(2000)

            ' ── 场景7：根据 Key 操作 ─────────────────────────────────
            Dim keys As IList(Of String) = PTLController.GetAllKeys()
            If keys.Count > 0 Then
                Console.WriteLine(String.Format(vbCrLf & "[场景7] 按 Key 操作，共 {0} 个 Key", keys.Count))
                Console.WriteLine(String.Format("  SetLight(""{0}"", LedColor.Green)", keys(0)))
                PTLController.SetLight(keys(0), LedColor.Green)
                Threading.Thread.Sleep(1500)

                If keys.Count > 1 Then
                    Console.WriteLine(String.Format("  SetBlink(""{0}"", LedColor.Red, 400)", keys(1)))
                    PTLController.SetBlink(keys(1), LedColor.Red, 400)
                    Threading.Thread.Sleep(2000)
                End If
            End If

            ' ── 场景8：跑马灯 ────────────────────────────────────────
            Console.WriteLine(vbCrLf & "[场景8] 跑马灯（蓝色，3秒后停止）")
            PTLController.Marquee(LedColor.Blue, 80)
            Threading.Thread.Sleep(3000)

            ' ── 场景9：蜂鸣（仅 MQTT 模式有效）──────────────────────────
            Console.WriteLine(vbCrLf & "[场景9] 蜂鸣示例（仅 MQTT 模式有效）")
            PTLController.BeepOn(1, 0)
            Threading.Thread.Sleep(1000)
            PTLController.BeepBlink(1, 0, 300)
            Threading.Thread.Sleep(1200)
            PTLController.BeepOff(1, 0)

            ' ── 全部熄灭 ─────────────────────────────────────────────
            Console.WriteLine(vbCrLf & "[结束] 全部熄灭")
            PTLController.AllOff()

        Finally
            PTLController.AllOff()
            PTLController.Disconnect()
            RemoveHandler PTLController.TagEventReceived, AddressOf OnTagEvent
            Console.WriteLine("串口已断开。")
        End Try

        Console.WriteLine(vbCrLf & "按任意键退出...")
        Console.ReadKey()
    End Sub

End Module
