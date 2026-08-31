namespace OogL2Client;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogStartupException(args.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception."));
        Application.ThreadException += (_, args) =>
            LogStartupException(args.Exception);

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            LogStartupException(ex);
            throw;
        }
    }

    private static void LogStartupException(Exception ex)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "startup-error.log");
            var content = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(path, content);
        }
        catch
        {
            // Best-effort logging only.
        }
    }
}