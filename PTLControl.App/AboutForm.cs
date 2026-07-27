using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace PTLControl;

public sealed class AboutForm : Form
{
    private const string RepositoryUrl = "https://github.com/EzioGuevara/PTL_Control";

    public AboutForm()
    {
        Text = "关于 PTL Control";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(430, 300);
        Font = new Font("Microsoft YaHei UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 22, 28, 18),
            ColumnCount = 1,
            RowCount = 8
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        root.Controls.Add(new Label
        {
            Text = "PTL Control",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 20F, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 78, 121)
        }, 0, 0);

        root.Controls.Add(new Label
        {
            Text = "版本 " + GetVersion(),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.DimGray
        }, 0, 1);

        root.Controls.Add(new Label
        {
            Text = "串口 / MQTT 二选一的 PTL 灯带控制软件",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        }, 0, 2);

        root.Controls.Add(CreateInfoLabel("作者：Ezio Li"), 0, 3);
        root.Controls.Add(CreateInfoLabel("公司：IDEMIA"), 0, 4);

        var repositoryLink = new LinkLabel
        {
            Text = RepositoryUrl,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            LinkColor = Color.FromArgb(31, 78, 121)
        };
        repositoryLink.LinkClicked += (_, _) => OpenRepository();
        root.Controls.Add(repositoryLink, 0, 5);

        var copyright = new Label
        {
            Text = "Copyright © 2026 Ezio Li / IDEMIA",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomCenter,
            ForeColor = Color.Gray
        };
        root.Controls.Add(copyright, 0, 6);

        var okButton = new Button
        {
            Text = "确定",
            Width = 90,
            Height = 30,
            Anchor = AnchorStyles.None,
            DialogResult = DialogResult.OK
        };
        root.Controls.Add(okButton, 0, 7);

        AcceptButton = okButton;
        CancelButton = okButton;
        Controls.Add(root);
    }

    private static Label CreateInfoLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };
    }

    private static string GetVersion()
    {
        var assembly = typeof(AboutForm).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (!string.IsNullOrWhiteSpace(informational?.InformationalVersion))
            return informational.InformationalVersion.Split('+')[0];

        return assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    }

    private static void OpenRepository()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = RepositoryUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            // 打不开浏览器时不影响关于页面。
        }
    }
}
