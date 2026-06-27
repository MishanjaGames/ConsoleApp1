namespace BlockChain_01.Services
{
    public class LoggingService
    {
        private readonly string _logFile;
        private readonly object _lock = new();

        public LoggingService(string logFile = "debug.log")
        {
            _logFile = logFile;
        }

        public void Log(string category, string message, LogLevel level = LogLevel.Info)
        {
            string line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{level.ToString().ToUpper(),-5}] [{category}] {message}";
            lock (_lock)
            {
                File.AppendAllText(_logFile, line + Environment.NewLine);
            }
        }

        public void Info(string category, string message) => Log(category, message, LogLevel.Info);
        public void Warn(string category, string message) => Log(category, message, LogLevel.Warn);
        public void Error(string category, string message) => Log(category, message, LogLevel.Error);
        public void Debug(string category, string message) => Log(category, message, LogLevel.Debug);
    }

    public enum LogLevel { Debug, Info, Warn, Error }
}