using System.Linq;
using System.Windows;
using System.Reflection;
using System.Runtime.Versioning;

namespace WpfApp1
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            var targetFrameworkAttribute = Assembly.GetExecutingAssembly()
                                                   .GetCustomAttributes<TargetFrameworkAttribute>()
                                                   .SingleOrDefault();

            this.TextBox.Text = $"Hello from:{Environment.NewLine}{new VersionInfo()}";
        }

        private void ButtonExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            //CallViaProcAddress();

            CallViaDllImport();
        }

        private static void CallViaProcAddress()
        {
            var ownDirectoryName = Path.GetDirectoryName(typeof(MainWindow).Assembly.Location);
            var ownDirectoryInfo = new DirectoryInfo(ownDirectoryName);
            var assemblyPath = Path.GetFullPath(Path.Combine(ownDirectoryName, "../.."));

            var bitnessString = Environment.Is64BitProcess
                                    ? "x64"
                                    : "x86";

            var nativeLibraryPath = Path.Combine(assemblyPath, $"ManagedWithDllExport.{ownDirectoryInfo.Name}.{bitnessString}.dll");

            var pDll = NativeMethods.LoadLibrary(nativeLibraryPath);
            //oh dear, error handling here
            //if (pDll == IntPtr.Zero)

            var pAddressOfFunctionToCall = NativeMethods.GetProcAddress(pDll, nameof(ManagedMethod));
            //oh dear, error handling here
            //if(pAddressOfFunctionToCall == IntPtr.Zero)

            var managedMethod = (ManagedMethod)Marshal.GetDelegateForFunctionPointer(pAddressOfFunctionToCall, typeof(ManagedMethod));

            var theResult = managedMethod();

            var result = NativeMethods.FreeLibrary(pDll);

            MessageBox.Show(theResult);
        }

        private static void CallViaDllImport()
        {
            var theResult = ManagedMethodFromDllImport();

            MessageBox.Show(theResult);
        }

        //[DllImport("ManagedWithDllExport.netcoreapp3.0.x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ManagedMethod")]
        //[DllImport("ManagedWithDllExport.net462.x64.dll", EntryPoint = "ManagedMethod")]
#if NET462
        [DllImport(@"ManagedWithDllExport.net462.x64.dll", EntryPoint = "ManagedMethod", CallingConvention = CallingConvention.Cdecl)]
#else
        [DllImport(@"ManagedWithDllExport.netcoreapp3.0.x64.dll", EntryPoint = "ManagedMethod", CallingConvention = CallingConvention.Cdecl)]
#endif
        private static extern string ManagedMethodFromDllImport();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate string ManagedMethod();
    }

    static class NativeMethods
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);


        [DllImport("kernel32.dll")]
        public static extern bool FreeLibrary(IntPtr hModule);
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