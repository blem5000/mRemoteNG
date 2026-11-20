using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Forms;
using mRemoteNG.Messages;
using mRemoteNG.Resources.Language;

namespace mRemoteNG.App.Initialization
{
    [SupportedOSPlatform("windows")]
    public class StartupDataLogger(MessageCollector messageCollector)
    {
        private readonly MessageCollector _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));

        public void LogStartupData()
        {
            LogApplicationData();
            LogCmdLineArgs();
            LogSystemData();
            LogClrData();
            LogCultureData();
        }

        private void LogSystemData()
        {
            string osData = GetOperatingSystemData();
            string architecture = GetArchitectureData();
            string[] nonEmptyData = Array.FindAll(new[] {osData, architecture}, s => !string.IsNullOrEmpty(s));
            string data = string.Join(" ", nonEmptyData);
            _messageCollector.AddMessage(MessageClass.InformationMsg, data, true);
        }

        private string GetOperatingSystemData()
        {
            // WMI (Win32_OperatingSystem) took ~200-300 ms on every start just to log a line.
            // Environment.OSVersion gives the equivalent information without spawning WMI.
            return Environment.OSVersion.VersionString.Trim();
        }

        private string GetArchitectureData()
        {
            // WMI (Win32_Processor AddressWidth) was another ~100-200 ms; this is instantaneous.
            return Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
        }

        private void LogApplicationData()
        {
            string data = $"{Application.ProductName} {Application.ProductVersion}";
            if (Runtime.IsPortableEdition)
                data += $" {Language.PortableEdition}";
            data += " starting.";
            _messageCollector.AddMessage(MessageClass.InformationMsg, data, true);
        }

        private void LogCmdLineArgs()
        {
            string data = $"Command Line: {string.Join(" ", Environment.GetCommandLineArgs())}";
            _messageCollector.AddMessage(MessageClass.InformationMsg, data, true);
        }

        private void LogClrData()
        {
            string data = $"Microsoft .NET CLR {Environment.Version}";
            _messageCollector.AddMessage(MessageClass.InformationMsg, data, true);
        }

        private void LogCultureData()
        {
            string data = $"System Culture: {Thread.CurrentThread.CurrentUICulture.Name}/{Thread.CurrentThread.CurrentUICulture.NativeName}";
            _messageCollector.AddMessage(MessageClass.InformationMsg, data, true);
        }
    }
}