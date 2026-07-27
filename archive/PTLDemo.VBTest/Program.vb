Imports PTLControl.Compat
Imports PTLControl.Compat.Models

Module Program
    Sub Main()
        ' 验证能正确引用 PTLController 和 LedColor 枚举
        Dim ports As String() = PTLController.GetPortNames()
        Console.WriteLine("可用串口数量：" & ports.Length.ToString())

        ' 验证 LedColor 枚举可以访问
        Dim c As LedColor = LedColor.Green
        Console.WriteLine("颜色枚举：" & c.ToString())

        ' 验证 GetAllKeys 可以调用
        Dim keys As IList(Of String) = PTLController.GetAllKeys()
        Console.WriteLine("Key 数量：" & keys.Count.ToString())

        Console.WriteLine("编译和引用验证通过！（未连接串口属正常，不影响引用测试）")
        Console.ReadKey()
    End Sub
End Module
