using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace PTLControl.Compat.Services
{
    /// <summary>
    /// 轻量文件日志服务：中文日志、等级过滤、按天滚动。
    /// </summary>
    internal static class LogService
    {
        private enum LogLevelValue
        {
            Off = 0,
            Error = 1,
            Warn = 2,
            Info = 3,
            Debug = 4
        }

        private static readonly object _sync = new object();
        private static readonly AsyncLocal<int> _apiCallDepth = new AsyncLocal<int>();
        private static readonly AsyncLocal<string> _traceId = new AsyncLocal<string>();
        private static bool _initialized;
        private static LogLevelValue _currentLevel = LogLevelValue.Info;

        private static readonly string NewLogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PTLControl",
            "logs");
        private static readonly string LegacyLogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PTLDemo",
            "logs");
        private static bool _logMigrationDone;

        public static void RefreshLevelFromConfig()
        {
            lock (_sync)
            {
                try
                {
                    var startup = ConfigService.LoadStartup();
                    _currentLevel = ParseLevel(startup.LogLevel);
                    _initialized = true;
                }
                catch
                {
                    // 配置读取异常时回退到 Info，避免影响主流程。
                    _currentLevel = LogLevelValue.Info;
                    _initialized = true;
                }
            }
        }

        public static void Debug(string message) => Write(LogLevelValue.Debug, message);
        public static void Info(string message) => Write(LogLevelValue.Info, message);
        public static void Warn(string message) => Write(LogLevelValue.Warn, message);
        public static void Error(string message) => Write(LogLevelValue.Error, message);

        public static void Error(string message, Exception ex)
        {
            var detail = ex == null ? string.Empty : " | 异常：" + ex.GetType().Name + " - " + ex.Message;
            Write(LogLevelValue.Error, message + detail);
        }

        /// <summary>
        /// 记录接口调用（仅最外层一次），避免内部转调重复记录。
        /// </summary>
        public static IDisposable BeginApiCall(string message)
        {
            if (_apiCallDepth.Value == 0)
            {
                _traceId.Value = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
                Debug(message);
            }

            _apiCallDepth.Value = _apiCallDepth.Value + 1;
            return new ApiCallScope();
        }

        private static void Write(LogLevelValue level, string message)
        {
            lock (_sync)
            {
                if (!_initialized)
                    RefreshLevelFromConfig();

                if (!ShouldWrite(level))
                    return;

                try
                {
                    EnsureLogDirectoryMigrated();
                    Directory.CreateDirectory(NewLogDirectory);
                    var filePath = Path.Combine(
                        NewLogDirectory,
                        "ptl-" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log");

                    var line = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} [{1}] {2}",
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                        ToChineseLevel(level),
                        AttachTrace(message ?? string.Empty));

                    File.AppendAllText(filePath, line + Environment.NewLine);
                }
                catch
                {
                    // 日志异常不能影响主业务流程。
                }
            }
        }

        private static bool ShouldWrite(LogLevelValue level)
        {
            if (_currentLevel == LogLevelValue.Off)
                return false;
            return level <= _currentLevel;
        }

        private static string ToChineseLevel(LogLevelValue level)
        {
            switch (level)
            {
                case LogLevelValue.Error:
                    return "错误";
                case LogLevelValue.Warn:
                    return "警告";
                case LogLevelValue.Info:
                    return "信息";
                case LogLevelValue.Debug:
                    return "调试";
                default:
                    return "关闭";
            }
        }

        private static string AttachTrace(string message)
        {
            var traceId = _traceId.Value;
            if (string.IsNullOrWhiteSpace(traceId))
                return message;
            return "[trace:" + traceId + "] " + message;
        }

        private static LogLevelValue ParseLevel(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return LogLevelValue.Info;

            switch (text.Trim().ToLowerInvariant())
            {
                case "off":
                case "none":
                case "关闭":
                    return LogLevelValue.Off;
                case "info":
                case "information":
                case "normal":
                case "日常":
                case "信息":
                    return LogLevelValue.Info;
                case "debug":
                case "调试":
                    return LogLevelValue.Debug;
                default:
                    return LogLevelValue.Info;
            }
        }

        private static void EnsureLogDirectoryMigrated()
        {
            if (_logMigrationDone)
                return;

            try
            {
                if (Directory.Exists(LegacyLogDirectory))
                {
                    Directory.CreateDirectory(NewLogDirectory);
                    var files = Directory.GetFiles(LegacyLogDirectory, "*.log");
                    for (int i = 0; i < files.Length; i++)
                    {
                        var source = files[i];
                        var target = Path.Combine(NewLogDirectory, Path.GetFileName(source));
                        if (!File.Exists(target))
                            File.Copy(source, target);
                    }
                }
            }
            catch
            {
                // 日志目录迁移失败不影响主流程。
            }
            finally
            {
                _logMigrationDone = true;
            }
        }

        private sealed class ApiCallScope : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                var depth = _apiCallDepth.Value;
                var next = depth > 0 ? depth - 1 : 0;
                _apiCallDepth.Value = next;
                if (next == 0)
                    _traceId.Value = null;
            }
        }

    }
}
