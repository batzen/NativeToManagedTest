namespace Exporter
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using dnlib.DotNet;
    using dnlib.DotNet.MD;
    using dnlib.DotNet.Writer;
    using CallingConvention = System.Runtime.InteropServices.CallingConvention;

    public static class Program
    {
        private const string DllExportAttributeClassName = "DllExportAttribute";

        public enum Bitness
        {
            x86,
            x64
        }

        public static int Main(string[] args)
        {
            Console.WriteLine($"Commandline: {Environment.CommandLine}");

            foreach (var assembly in args)
            {
                Console.WriteLine($"Generating exported assembly for '{assembly}'...");

                if (File.Exists(assembly) == false)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));

                    if (File.Exists(assembly) == false)
                    {
                        Console.WriteLine($"Could not find '{assembly}'.");
                        return 1;
                    }
                }                

                CreateAssembly(assembly, Bitness.x86);
                CreateAssembly(assembly, Bitness.x64);

                Console.WriteLine($"Generating x86 version for '{assembly}'...");
                CreateAssemblyWithExports(assembly, Bitness.x86);

                Console.WriteLine($"Generating x64 version for '{assembly}'...");
                CreateAssemblyWithExports(assembly, Bitness.x64);

                Console.WriteLine($"Generated exported assembly for '{assembly}'.");
            }

            return 0;
        }

        private static void CreateAssembly(string assembly, Bitness bitness)
        {
            var creationOptions = new ModuleCreationOptions
                                  {
                                      TryToLoadPdbFromDisk = true
                                  };
            using var module = ModuleDefMD.Load(assembly, creationOptions);

            //module.Characteristics |= dnlib.PE.Characteristics.Dll;
            module.Cor20HeaderFlags &= ~dnlib.DotNet.MD.ComImageFlags.ILOnly;

            ChangeBitness(module, bitness);

            var path = Path.GetDirectoryName(module.Location);
            var filename = Path.GetFileNameWithoutExtension(module.Location);
            var extension = Path.GetExtension(module.Location);
            var saveFilename = $"{filename}.{bitness}{extension}";
            var moduleWriterOptions = new ModuleWriterOptions(module)
                                      {
                                          AddCheckSum = true,
                                          WritePdb = true
                                      };
            //Console.WriteLine(moduleWriterOptions.WritePdb);
            module.Write(Path.Combine(path, saveFilename), moduleWriterOptions);
        }

        private static void CreateAssemblyWithExports(string assembly, Bitness bitness)
        {
            var creationOptions = new ModuleCreationOptions
                                  {
                                      TryToLoadPdbFromDisk = true
                                  };
            using var module = ModuleDefMD.Load(assembly, creationOptions);

            //module.Characteristics |= dnlib.PE.Characteristics.Dll;
            module.Cor20HeaderFlags &= ~dnlib.DotNet.MD.ComImageFlags.ILOnly;

            ChangeBitness(module, bitness);

            ExportMethods(module);

            var path = Path.GetDirectoryName(module.Location);
            var filename = Path.GetFileNameWithoutExtension(module.Location);
            var extension = Path.GetExtension(module.Location);
            var saveFilename = $"{filename}.Exported.{bitness}{extension}";
            var moduleWriterOptions = new ModuleWriterOptions(module)
                                      {
                                          AddCheckSum = true,
                                          WritePdb = true
                                      };
            module.Write(Path.Combine(path, saveFilename), moduleWriterOptions);
        }

        private static void ChangeBitness(ModuleDefMD module, Bitness bitness)
        {
            switch (bitness)
            {
                case Bitness.x86:
                    module.Machine = dnlib.PE.Machine.I386;
                    module.Cor20HeaderFlags &= ~dnlib.DotNet.MD.ComImageFlags.Bit32Preferred;
                    module.Cor20HeaderFlags |= dnlib.DotNet.MD.ComImageFlags.Bit32Required;
                    break;

                case Bitness.x64:
                    module.Machine = dnlib.PE.Machine.AMD64;
                    module.Cor20HeaderFlags &= ~dnlib.DotNet.MD.ComImageFlags.Bit32Preferred;
                    module.Cor20HeaderFlags &= ~dnlib.DotNet.MD.ComImageFlags.Bit32Required;
                    break;
            }
        }

        private static void ExportMethods(ModuleDefMD module)
        {
            var methods = module.Types.SelectMany(x => x.Methods)
                .Where(x => x.CustomAttributes.Any(IsDllExportAttribute));

            foreach (var method in methods)
            {
                var exportInfo = new DllExportInfo(method);
                exportInfo.ApplyExport();
            }
        }

        private static bool IsDllExportAttribute(CustomAttribute attribute)
        {
            return attribute.AttributeType.Name == DllExportAttributeClassName;
        }

        private class DllExportInfo
        {
            private readonly CustomAttribute exportAttribute;
            private readonly MethodDef method;

            public DllExportInfo(MethodDef method)
            {
                this.method = method ?? throw new ArgumentNullException(nameof(method));
                this.exportAttribute = method.CustomAttributes.FirstOrDefault(IsDllExportAttribute) ?? throw new ArgumentException($"No DllExport attribute found on method '{method.FullName}'.");

                this.ExportName = GetExportNameFromAttribute(this.exportAttribute);
                
                this.CallingConvention = GetCallingConventionFromAttribute(this.exportAttribute);

                this.CallingConventionClass = GetCallingConventionClass((ModuleDefMD)method.Module, this.CallingConvention);
            }            

            public string ExportName { get; }

            public CallingConvention CallingConvention { get; }

            public TypeRef CallingConventionClass { get; }

            public MethodDef ApplyExport()
            {
                this.method.ExportInfo = new MethodExportInfo(this.ExportName) { Options = MethodExportInfoOptions.FromUnmanaged };

                SetCallingConventionForMethod(this.method, this.CallingConventionClass);

                return this.method;
            }

            private static UTF8String GetExportNameFromAttribute(ICustomAttribute exportAttribute)
            {
                var valueFromAttribute = exportAttribute?.Properties.FirstOrDefault(x => x.Name == "ExportName")?.Value as UTF8String;

                if (string.IsNullOrEmpty(valueFromAttribute))
                {
                    return null;
                }

                return valueFromAttribute;
            }

            private static CallingConvention GetCallingConventionFromAttribute(ICustomAttribute exportAttribute)
            {
                var valueFromAttribute = exportAttribute?.Properties.FirstOrDefault(x => x.Name == "CallingConvention")?.Value;
                return (CallingConvention)(valueFromAttribute ?? CallingConvention.Cdecl);
            }

            private static void SetCallingConventionForMethod(MethodDef method, TypeRef callingConvention)
            {
                //Console.WriteLine($"CallingConvention: {callingConvention.Name}");

                //var type = method.MethodSig.RetType;
                //type = new CModOptSig(callingConvention, type);
                //method.MethodSig.RetType = type;
            }

            private static TypeRef GetCallingConventionClass(ModuleDefMD module, CallingConvention callingConvention)
            {
                var corLibTypes = module.CorLibTypes;

                Console.WriteLine(corLibTypes.AssemblyRef.FullName);

                switch (corLibTypes.AssemblyRef.Name.ToLower())
                {
                    case "system.runtime":
                        return GetCallingConventionClassFromSystemRuntime(module, callingConvention);

                    case "mscorlib":
                        return GetCallingConventionClassFromMscorlib(module, callingConvention);

                    default:
                        throw new Exception($"Unmapped CorLibTypes {corLibTypes.AssemblyRef.FullName}");
                }
            }

            private static TypeRef GetCallingConventionClassFromSystemRuntime(ModuleDefMD module, CallingConvention callingConvention)
            {
                var callingConventionAssembly = "System.Runtime.InteropServices";

                var numAsmRefs = module.TablesStream.AssemblyRefTable.Rows;
                
                var moduleContext = new ModuleContext();
                var resolver = new AssemblyResolver(moduleContext);

                AssemblyRef callingConventionAssemblyRef = null;
                ModuleDefMD callingConventionAssemblyModule = null;
                
                for (uint i = 0; i < numAsmRefs; i++)
                {
                    var assemblyRef = module.ResolveAssemblyRef(i);

                    if (assemblyRef is null)
                    {
                        continue;
                    }

                    if (assemblyRef.Name == callingConventionAssembly)
                    {
                        var assembly = resolver.ResolveThrow(assemblyRef, module);

                        foreach (var assemblyModule in assembly.Modules.OfType<ModuleDefMD>())
                        {
                            if (assemblyModule.Assembly.Name == callingConventionAssembly)
                            {
                                callingConventionAssemblyRef = assemblyRef;
                                callingConventionAssemblyModule = assemblyModule;
                                
                                break;
                            }
                        }
                    }
                }

                switch (callingConvention)
                {
                    case CallingConvention.Winapi:
                        return GetTypeRef(callingConventionAssemblyModule, callingConventionAssembly, "CallConvStdcall", callingConventionAssemblyRef);

                    case CallingConvention.Cdecl:
                        return GetTypeRef(callingConventionAssemblyModule, callingConventionAssembly, "CallConvCdecl", callingConventionAssemblyRef);

                    case CallingConvention.StdCall:
                        return GetTypeRef(callingConventionAssemblyModule, callingConventionAssembly, "CallConvStdcall", callingConventionAssemblyRef);

                    case CallingConvention.ThisCall:
                        return GetTypeRef(callingConventionAssemblyModule, callingConventionAssembly, "CallConvThiscall", callingConventionAssemblyRef);

                    case CallingConvention.FastCall:
                        return GetTypeRef(callingConventionAssemblyModule, callingConventionAssembly, "CallConvFastcall", callingConventionAssemblyRef);

                    default:
                        throw new ArgumentOutOfRangeException(nameof(callingConvention), callingConvention, "Unhandled calling convention.");
                }
            }

            private static TypeRef GetTypeRef(ModuleDefMD assemblyModule, string callingConventionAssembly, string name, AssemblyRef assemblyRef)
            {
                return (TypeRef)assemblyModule.UpdateRowId<TypeRefUser>(new TypeRefUser(assemblyModule, callingConventionAssembly, name, assemblyRef));
            }

            private static TypeRef GetCallingConventionClassFromMscorlib(ModuleDefMD module, CallingConvention callingConvention)
            {
                const string callingConventionAssembly = "System.Runtime.CompilerServices";

                var corLibTypes = module.CorLibTypes;

                switch (callingConvention)
                {
                    case CallingConvention.Winapi:
                        return corLibTypes.GetTypeRef(callingConventionAssembly, "CallConvStdcall");

                    case CallingConvention.Cdecl:
                        return corLibTypes.GetTypeRef(callingConventionAssembly, "CallConvCdecl");

                    case CallingConvention.StdCall:
                        return corLibTypes.GetTypeRef(callingConventionAssembly, "CallConvStdcall");

                    case CallingConvention.ThisCall:
                        return corLibTypes.GetTypeRef(callingConventionAssembly, "CallConvThiscall");

                    case CallingConvention.FastCall:
                        return corLibTypes.GetTypeRef(callingConventionAssembly, "CallConvFastcall");

                    default:
                        throw new ArgumentOutOfRangeException(nameof(callingConvention), callingConvention, "Unhandled calling convention.");
                }
            }

            private static ICorLibTypes GetCorLibTypes(ModuleDef module)
            {
                foreach (var asmRef in module.GetAssemblyRefs())
                {
                    if (IsAssemblyRef(asmRef, systemRuntimeName, contractsPublicKeyToken))
                    {
                        return new CorLibTypes(module, asmRef);
                    }
                }

                return null;
            }

            AssemblyRef GetAlternativeCorLibReference(ModuleDefMD module)
            {
                foreach (var asmRef in module.GetAssemblyRefs())
                {
                    if (IsAssemblyRef(asmRef, systemRuntimeName, contractsPublicKeyToken))
                        return asmRef;
                }
                foreach (var asmRef in module.GetAssemblyRefs())
                {
                    if (IsAssemblyRef(asmRef, corefxName, contractsPublicKeyToken))
                        return asmRef;
                }
                return null;
            }

            static bool IsAssemblyRef(AssemblyRef asmRef, UTF8String name, PublicKeyToken token)
            {
                if (asmRef.Name != name)
                    return false;
                var pkot = asmRef.PublicKeyOrToken;
                if (pkot == null)
                    return false;
                return token.Equals(pkot.Token);
            }

            static readonly UTF8String systemRuntimeName = new UTF8String("System.Runtime");
            static readonly UTF8String corefxName = new UTF8String("corefx");
            static readonly PublicKeyToken contractsPublicKeyToken = new PublicKeyToken("b03f5f7f11d50a3a");
        }
    }
}