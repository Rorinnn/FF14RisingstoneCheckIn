namespace FF14RisingstoneCheckIn;

static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    static void Main(string[] args)
    {
        const string mutexName = "FF14RisingstoneCheckIn_SingleInstance";
        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();
            
            bool startSilent = args.Contains("--silent") || args.Contains("-s");
            
            Application.Run(new MainForm(startSilent));
        }
        finally
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
    }
}
