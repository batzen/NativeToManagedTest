using System;

namespace ManagedWithDllExport
{
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Media;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Windows;

    public static class ManagedClass
    {
        private static uint messageId;

        [DllImport("user32.dll", SetLastError=true, CharSet=CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);

        [StructLayout(LayoutKind.Sequential)]
        private struct CWPSTRUCT
        {
            public IntPtr lparam;
            public IntPtr wparam;
            public int message;
            public IntPtr hwnd;
        }

        [DllImport("user32.dll")]
        private static extern int CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        static ManagedClass()
        {
            messageId = RegisterWindowMessage("Injector_GOBABYGO!");
        }

        [DllExport(ExportName = "Blubb")]
        public static void XX()
        {
            Console.WriteLine(GetHelloString());
        }

        [DllExport]
        public static string ManagedMethod()
        {
            var helloString = GetHelloString();
            Console.WriteLine(helloString);
            
            return helloString;
        }

        [DllExport]
        public static int MessageHookProc(int code, IntPtr wparam, IntPtr lparam)
        {
            if (code == 0)
            {
                var msg = (CWPSTRUCT)Marshal.PtrToStructure(lparam, typeof(CWPSTRUCT));

                if (msg.message == messageId)
                {
                    Trace.WriteLine($"MessageHookProc in .NET {code} {wparam} {lparam}");
                    Application.Current.MainWindow.Activate();
                    Application.Current.MainWindow.WindowState = WindowState.Maximized;
                    MessageBox.Show(GetHelloString());
                }
            }

            return CallNextHookEx(IntPtr.Zero, code, wparam, lparam);
        }

        private static string GetHelloString([CallerMemberName] string caller = null)
        {
            return $"Hi from method {caller}.{Environment.NewLine}Assembly:{Environment.NewLine}{Assembly.GetExecutingAssembly().FullName}{Environment.NewLine}VersionInfo:{Environment.NewLine}{new VersionInfo()}";
        }
    }

    [Serializable]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class DllExportAttribute : Attribute
    {
        //public DllExportAttribute(string exportName, CallingConvention callingConvention) 
        //{
        //    this.ExportName = exportName;
        //    this.CallingConvention = callingConvention;
        //}

        //public DllExportAttribute(string exportName) 
        //{ 
        //    this.ExportName = exportName;
        //}

        //public DllExportAttribute(CallingConvention callingConvention) 
        //{
        //    this.CallingConvention = callingConvention;
        //}

        //public DllExportAttribute()
        //{ 
        //}

        public string ExportName { get; set; }

        public CallingConvention CallingConvention { get; set; } = CallingConvention.Cdecl;        
    }

    public class VersionInfo
    {
        /// <inheritdoc />
        public override string ToString()
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine(".NET version:");
            var targetFrameworkAttribute = Assembly.GetExecutingAssembly().GetCustomAttributes<TargetFrameworkAttribute>().SingleOrDefault();
            stringBuilder.AppendLine($"TargetFrameworkAttribute.FrameworkName: {targetFrameworkAttribute.FrameworkName}");
            stringBuilder.AppendLine($"TargetFrameworkAttribute.FrameworkDisplayName: {targetFrameworkAttribute.FrameworkDisplayName}");
            stringBuilder.AppendLine($"Environment.Version: {Environment.Version}");
#if NET462
#else
            stringBuilder.AppendLine($"RuntimeInformation.FrameworkDescription: {RuntimeInformation.FrameworkDescription}");

            stringBuilder.AppendLine($"CoreCLR Build: {((AssemblyInformationalVersionAttribute[])typeof(object).Assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false))[0].InformationalVersion.Split('+')[0]}");
            stringBuilder.AppendLine($"CoreCLR Hash: {((AssemblyInformationalVersionAttribute[])typeof(object).Assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false))[0].InformationalVersion.Split('+')[1]}");
            stringBuilder.AppendLine($"CoreFX Build: {((AssemblyInformationalVersionAttribute[])typeof(Uri).Assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false))[0].InformationalVersion.Split('+')[0]}");
            stringBuilder.AppendLine($"CoreFX Hash: {((AssemblyInformationalVersionAttribute[])typeof(Uri).Assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false))[0].InformationalVersion.Split('+')[1]}");
#endif
            stringBuilder.AppendLine();

            stringBuilder.AppendLine("OS Version");
            stringBuilder.AppendLine($"Environment.OSVersion: {Environment.OSVersion}");
#if NET462
#else
            stringBuilder.AppendLine($"RuntimeInformation.OSDescription: {RuntimeInformation.OSDescription}");
#endif
            stringBuilder.AppendLine();

            stringBuilder.AppendLine("Bitness");
#if NET462
#else
            stringBuilder.AppendLine($"RuntimeInformation.OSArchitecture: {RuntimeInformation.OSArchitecture}");
            stringBuilder.AppendLine($"RuntimeInformation.ProcessArchitecture: {RuntimeInformation.ProcessArchitecture}");
#endif
            stringBuilder.AppendLine($"Environment.Is64BitOperatingSystem: {Environment.Is64BitOperatingSystem}");
            stringBuilder.AppendLine($"Environment.Is64BitProcess: {Environment.Is64BitProcess}");

            return stringBuilder.ToString();
        }
    }
}