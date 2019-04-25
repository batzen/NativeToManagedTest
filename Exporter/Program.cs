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

                //CreateAssembly(assembly, Bitness.x86);
                //CreateAssembly(assembly, Bitness.x64);

                Console.WriteLine($"Generating x86 version for '{assembly}'...");
                CreateAssembly(assembly, Bitness.x86, ExportMethods);

                Console.WriteLine($"Generating x64 version for '{assembly}'...");
                CreateAssembly(assembly, Bitness.x64, ExportMethods);

                Console.WriteLine($"Generated exported assembly for '{assembly}'.");
            }

            return 0;
        }

        private static void CreateAssembly(string assembly, Bitness bitness, Action<ModuleDefMD> modifyAction = null)
        {
            var creationOptions = new ModuleCreationOptions
                                  {
                                      TryToLoadPdbFromDisk = true
                                  };
            using var module = ModuleDefMD.Load(assembly, creationOptions);

            //module.Characteristics |= dnlib.PE.Characteristics.Dll;
            module.Cor20HeaderFlags &= ~ComImageFlags.ILOnly;

            ChangeBitness(module, bitness);

            modifyAction?.Invoke(module);

            DisableEditAndContinueForModule(module);

            var path = Path.GetDirectoryName(module.Location);
            var filename = Path.GetFileNameWithoutExtension(module.Location);
            var extension = Path.GetExtension(module.Location);
            var saveFilename = $"{filename}.{bitness}{extension}";

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
                    module.Cor20HeaderFlags &= ~ComImageFlags.ILOnly;
                    module.Cor20HeaderFlags &= ~ComImageFlags.Bit32Preferred;
                    module.Cor20HeaderFlags |= ComImageFlags.Bit32Required;
                    break;

                case Bitness.x64:
                    module.Machine = dnlib.PE.Machine.AMD64;
                    module.Cor20HeaderFlags &= ~ComImageFlags.ILOnly;
                    module.Cor20HeaderFlags &= ~ComImageFlags.Bit32Preferred;
                    module.Cor20HeaderFlags &= ~ComImageFlags.Bit32Required;
                    break;
            }
        }

        private static void DisableEditAndContinueForModule(ModuleDefMD module)
        {
            var ca = module.Assembly.CustomAttributes.Find("System.Diagnostics.DebuggableAttribute");
            if (ca == null
                || ca.ConstructorArguments.Count != 1)
            {
                return;
            }

            var arg = ca.ConstructorArguments[0];

            var editAndContinueValue = (int)DebuggableAttribute.DebuggingModes.EnableEditAndContinue;

            // VS' debugger crashes if value == 0x107, so clear EnC bit
            if (arg.Type.FullName == "System.Diagnostics.DebuggableAttribute/DebuggingModes" 
                && arg.Value is int value 
                && (value & editAndContinueValue) != 0)
            {
                arg.Value = value & ~editAndContinueValue;
                ca.ConstructorArguments[0] = arg;
            }
        }

        private static void ExportMethods(ModuleDefMD module)
        {
            var methods = module.GetTypes()
                                .SelectMany(x => x.Methods)
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

                this.EntryPoint = GetEntryPointFromAttribute(this.exportAttribute);
                
                this.CallingConvention = GetCallingConventionFromAttribute(this.exportAttribute);

                this.CallingConventionClass = GetCallingConventionClass((ModuleDefMD)method.Module, this.CallingConvention);
            }            

            public string EntryPoint { get; }

            public CallingConvention CallingConvention { get; }

            public TypeRef CallingConventionClass { get; }

            public MethodDef ApplyExport()
            {
                this.method.ExportInfo = new MethodExportInfo(this.EntryPoint) { Options = MethodExportInfoOptions.FromUnmanaged };

                SetCallingConventionForMethod(this.method, this.CallingConventionClass);

                return this.method;
            }

            private static UTF8String GetEntryPointFromAttribute(ICustomAttribute exportAttribute)
            {
                var valueFromAttribute = exportAttribute?.Properties.FirstOrDefault(x => x.Name == "EntryPoint")?.Value as UTF8String;

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

                var type = method.MethodSig.RetType;
                type = new CModOptSig(callingConvention, type);
                method.MethodSig.RetType = type;

                //method.MethodSig.CallingConvention = dnlib.DotNet.CallingConvention.C;
            }

            private static TypeRef GetCallingConventionClass(ModuleDefMD module, CallingConvention callingConvention)
            {
                var corLibTypes = module.CorLibTypes;

                switch (corLibTypes.AssemblyRef.Name.ToLower())
                {
                    case "system.runtime":
                        return GetCallingConventionClassForNetCore(module, callingConvention);

                    case "mscorlib":
                        return GetCallingConventionClassForNetFramework(module, callingConvention);

                    default:
                        throw new Exception($"Unmapped CorLibTypes {corLibTypes.AssemblyRef.FullName}");
                }
            }

            private static TypeRef GetCallingConventionClassForNetCore(ModuleDefMD module, CallingConvention callingConvention)
            {
                const string callingConventionNamespace = "System.Runtime.CompilerServices";
                const string callingConventionAssemblyName = "System.Runtime.CompilerServices.VisualC";

                var callingConventionAssemblyRef = GetCallingConventionAssemblyRefForNetCore(module, callingConventionAssemblyName);

                if (callingConventionAssemblyRef == null)
                {
                    Debugger.Launch();
                    throw new Exception($"Could not find assembly reference for {callingConventionAssemblyName}.");
                }

                switch (callingConvention)
                {
                    case CallingConvention.Winapi:
                        return GetTypeRef(module, callingConventionNamespace, "CallConvStdcall", callingConventionAssemblyRef);

                    case CallingConvention.Cdecl:
                        return GetTypeRef(module, callingConventionNamespace, "CallConvCdecl", callingConventionAssemblyRef);

                    case CallingConvention.StdCall:
                        return GetTypeRef(module, callingConventionNamespace, "CallConvStdcall", callingConventionAssemblyRef);

                    case CallingConvention.ThisCall:
                        return GetTypeRef(module, callingConventionNamespace, "CallConvThiscall", callingConventionAssemblyRef);

                    case CallingConvention.FastCall:
                        return GetTypeRef(module, callingConventionNamespace, "CallConvFastcall", callingConventionAssemblyRef);

                    default:
                        throw new ArgumentOutOfRangeException(nameof(callingConvention), callingConvention, "Unhandled calling convention.");
                }
            }

            private static AssemblyRef GetCallingConventionAssemblyRefForNetCore(ModuleDefMD module, string callingConventionAssemblyName)
            {
                var numAsmRefs = module.TablesStream.AssemblyRefTable.Rows;

                foreach (var assemblyRef in module.GetAssemblyRefs())
                {
                    if (assemblyRef is null)
                    {
                        continue;
                    }

                    if (assemblyRef.Name == callingConventionAssemblyName)
                    {
                        return assemblyRef;
                    }
                }

                var compilerServicesAssembly = new AssemblyRefUser(module.Context.AssemblyResolver.ResolveThrow(new AssemblyRefUser(callingConventionAssemblyName), module));
                module.UpdateRowId(compilerServicesAssembly);

                return compilerServicesAssembly;
            }

            private static TypeRef GetTypeRef(ModuleDefMD assemblyModule, string @namespace, string name, IResolutionScope assemblyRef)
            {
                var typeRefUser = new TypeRefUser(assemblyModule, @namespace, name, assemblyRef);

                if (typeRefUser.ResolutionScope == null)
                {
                    throw new InvalidOperationException("ResolutionScope must not be null.");
                }

                return assemblyModule.UpdateRowId<TypeRefUser>(typeRefUser);
            }

            private static TypeRef GetCallingConventionClassForNetFramework(ModuleDefMD module, CallingConvention callingConvention)
            {
                const string callingConventionNamespace = "System.Runtime.CompilerServices";

                var corLibTypes = module.CorLibTypes;

                switch (callingConvention)
                {
                    case CallingConvention.Winapi:
                        return corLibTypes.GetTypeRef(callingConventionNamespace, "CallConvStdcall");

                    case CallingConvention.Cdecl:
                        return corLibTypes.GetTypeRef(callingConventionNamespace, "CallConvCdecl");

                    case CallingConvention.StdCall:
                        return corLibTypes.GetTypeRef(callingConventionNamespace, "CallConvStdcall");

                    case CallingConvention.ThisCall:
                        return corLibTypes.GetTypeRef(callingConventionNamespace, "CallConvThiscall");

                    case CallingConvention.FastCall:
                        return corLibTypes.GetTypeRef(callingConventionNamespace, "CallConvFastcall");

                    default:
                        throw new ArgumentOutOfRangeException(nameof(callingConvention), callingConvention, "Unhandled calling convention.");
                }
            }
        }
    }
}