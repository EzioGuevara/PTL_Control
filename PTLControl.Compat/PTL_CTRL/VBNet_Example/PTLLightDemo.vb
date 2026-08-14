Option Strict On

Imports System.Windows.Forms
Imports PTLControl.Compat
Imports PTLControl.Compat.Models

' VB.NET / .NET Framework 4.7.2+
' 只需引用 PTLControl.Compat.dll。
' HardwareHost 和其他 DLL 放在应用程序同一输出目录。
Module PTLLightDemo

    Sub Main()
        ' 应用初始化时只调用一次。
        If Not PTLController.Connect() Then
            MessageBox.Show(
                "PTL连接失败，请检查HardwareHost是否启动。" & vbCrLf &
                PTLController.LastConnectionMessage,
                "PTL连接失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error)
            Return
        End If

        Try
            ' 连接成功后直接调用，不需要枚举串口或自己加锁。
            PTLController.SetLight("A1", LedColor.Green)
            PTLController.SetBlink("A2", LedColor.Red, 500)
            PTLController.TurnOff("A1")
            PTLController.AllOff()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "PTL操作失败", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            ' 只断开当前调用方，不会关闭HardwareHost的物理串口。
            PTLController.Disconnect()
        End Try
    End Sub

End Module
