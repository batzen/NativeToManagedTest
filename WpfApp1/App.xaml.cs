namespace WpfApp1
{
    using System;
    using System.IO;
    using System.Reflection;

    public partial class App
    {
        public App()
        {
            var ownDirectoryName = Path.GetDirectoryName(this.GetType().Assembly.Location);
            var ownDirectoryInfo = new DirectoryInfo(ownDirectoryName);
            var assemblyPath = Path.GetFullPath(Path.Combine(ownDirectoryName, "../.."));

            if (Environment.Is64BitProcess)
            {
                Assembly.LoadFile(Path.Combine(assemblyPath, $"ManagedWithDllExport.{ownDirectoryInfo.Name}.x64.dll"));
                Assembly.LoadFile(Path.Combine(assemblyPath, $"ManagedWithDllExport.{ownDirectoryInfo.Name}.Exported.x64.dll"));
            }
            else
            {
                Assembly.LoadFile(Path.Combine(assemblyPath, $"ManagedWithDllExport.{ownDirectoryInfo.Name}.x86.dll"));
                Assembly.LoadFile(Path.Combine(assemblyPath, $"ManagedWithDllExport.{ownDirectoryInfo.Name}.Exported.x86.dll"));
            }
        }
    }
}