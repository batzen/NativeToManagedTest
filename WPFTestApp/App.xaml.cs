namespace WpfApp1
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;

    public partial class App
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectory(string lpPathName);

        public App()
        {
            var ownDirectoryName = Path.GetDirectoryName(this.GetType().Assembly.Location);
            var ownDirectoryInfo = new DirectoryInfo(ownDirectoryName);
            var assemblyPath = Path.GetFullPath(Path.Combine(ownDirectoryName, "../.."));

            //SetDllDirectory(assemblyPath);

#if NET462

#else
            if (Environment.Is64BitProcess)
            {
                Assembly.LoadFile(Path.Combine(assemblyPath, $"ManagedWithDllExport.{ownDirectoryInfo.Name}.x64.dll"));
            }
            else
            {
                Assembly.LoadFile(Path.Combine(assemblyPath, $"ManagedWithDllExport.{ownDirectoryInfo.Name}.x86.dll"));
            }
#endif
        }
    }
}