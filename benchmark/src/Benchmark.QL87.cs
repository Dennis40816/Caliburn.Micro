namespace Caliburn.Micro.QL87
{
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Text;
    using System.Text.Json;

    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Exporters;
    using BenchmarkDotNet.Running;

    /// <summary>
    ///     Provides a set of constants used across the application.
    /// </summary>
    internal static class Const
    {
        /// <summary>
        ///     The lower bound for the length of the prefix "arg_".
        /// </summary>
        public const int ArgLenLowerBound = 2;

        /// <summary>
        ///     The upper bound for the length of the prefix "arg_".
        /// </summary>
        public const int ArgLenUpperBound = 21;

        /// <summary>
        ///     Indicates the maximum number of arguments a method can have.
        /// </summary>
        public const int ArgNumEnd = 15;

        /// <summary>
        ///     Indicates the minimum number of arguments a method can have.
        /// </summary>
        public const int ArgNumStart = 1;

        /// <summary>
        ///     The prefix used for the argument naming.
        /// </summary>
        public const string ArgPrefix = "arg";

        /// <summary>
        ///     The name of the data file.
        /// </summary>
        public const string DataFileName = "test_data.json";

        /// <summary>
        ///     The prefix used for method naming.
        /// </summary>
        public const string MethodPrefix = "RandomMethod";
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            // Generate test information
            MethodDescriptorGenerator.GenerateMethodDescriptionsAndSave(
                Const.ArgNumStart,
                Const.ArgNumEnd,
                Const.DataFileName);

            // Deserialize method from given json file
            // MethodInfo methodInfo = MethodInfoRebuilder.RebuildMethodFromJson(Const.DataFileName, "RandomMethod_2");

            // Start benchmark
            var config = DefaultConfig.Instance.AddExporter(PlainExporter.Default);

            var summary = BenchmarkRunner.Run<StringVsStringBuilderBenchmark>(config);
        }
    }

    /// <summary>
    ///     Provides functionality to rebuild method information based on JSON data.
    /// </summary>
    public class MethodInfoRebuilder
    {
        private static Dictionary<string, Type> typeMap = new Dictionary<string, Type>
                                                          {
                                                              { "string", typeof(string) },
                                                              { "int", typeof(int) },
                                                              { "double", typeof(double) },
                                                              { "object", typeof(object) }
                                                          };

        /// <summary>
        ///     Reconstructs method information from a provided JSON file.
        /// </summary>
        /// <param name="filepath">The path to the JSON file containing method details.</param>
        /// <param name="methodName">The name of the method to rebuild.</param>
        /// <returns>The <see cref="MethodInfo" /> of the rebuilt method.</returns>
        /// <exception cref="ArgumentException">Thrown when the specified method name is not found in the JSON data.</exception>
        public static MethodInfo RebuildMethodFromJson(string filepath, string methodName)
        {
            var jsonData = File.ReadAllText(filepath);
            var methodsData =
                JsonSerializer.Deserialize<Dictionary<string, MethodDescriptorGenerator.MethodDescriptor>>(jsonData);

            if (!methodsData.ContainsKey(methodName))
            {
                throw new ArgumentException("Method name not found in the provided JSON data.");
            }

            var methodDescriptor = methodsData[methodName];
            return GenerateMethod(methodDescriptor);
        }

        /// <summary>
        ///     Generates a method based on its descriptor.
        /// </summary>
        /// <param name="descriptor">The descriptor of the method.</param>
        /// <returns>The <see cref="MethodInfo" /> of the generated method.</returns>
        private static MethodInfo GenerateMethod(MethodDescriptorGenerator.MethodDescriptor descriptor)
        {
            AssemblyName assemblyName = new AssemblyName("DynamicAssembly");
            AssemblyBuilder assemblyBuilder =
                AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicModule");

            TypeBuilder typeBuilder = moduleBuilder.DefineType("DynamicType", TypeAttributes.Public);

            Type[] paramTypes = descriptor.Parameters
                .Select((MethodDescriptorGenerator.ParameterDescriptor p) => typeMap[p.ParameterType])
                .ToArray();
            string[] paramNames = descriptor.Parameters
                .Select((MethodDescriptorGenerator.ParameterDescriptor p) => p.ParameterName)
                .ToArray();

            MethodBuilder methodBuilder = typeBuilder.DefineMethod(
                descriptor.MethodName,
                MethodAttributes.Public | MethodAttributes.Static,
                null,
                paramTypes);

            // Assign names to the method parameters
            for (int i = 0; i < paramNames.Length; i++)
            {
                methodBuilder.DefineParameter(i + 1, ParameterAttributes.None, paramNames[i]);
            }

            ILGenerator ilGenerator = methodBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ret);

            Type type = typeBuilder.CreateType();
            return type.GetMethod(descriptor.MethodName);
        }
    }

    /// <summary>
    ///     Generates method descriptors with random parameter names and types.
    /// </summary>
    public class MethodDescriptorGenerator
    {
        /// <summary>
        ///     Array of possible parameter types to be used when generating method descriptors.
        /// </summary>
        private static readonly string[] PossibleTypes = { "string", "int", "double", "object" };

        /// <summary>
        ///     Random number generator for creating random parameter names and types.
        /// </summary>
        private static Random rand = new Random();

        /// <summary>
        ///     Generates a method descriptor with the given number of parameters.
        /// </summary>
        /// <param name="paramCount">The number of parameters for the method.</param>
        /// <returns>A method descriptor with random parameter names and types.</returns>
        public static MethodDescriptor GenerateMethodDescription(int paramCount)
        {
            var methodDescriptor = new MethodDescriptor
                                   {
                                       MethodName = $"{Const.MethodPrefix}_{paramCount}",
                                       Parameters = new List<ParameterDescriptor>()
                                   };

            for (int i = 0; i < paramCount; i++)
            {
                methodDescriptor.Parameters.Add(
                    new ParameterDescriptor
                    {
                        ParameterName =
                            Const.ArgPrefix + "_" + GetRandomString(
                                rand.Next(Const.ArgLenLowerBound, Const.ArgLenUpperBound)),
                        ParameterType = PossibleTypes[rand.Next(PossibleTypes.Length)]
                    });
            }

            return methodDescriptor;
        }

        /// <summary>
        ///     Generates multiple method descriptors and saves them to a file as JSON.
        /// </summary>
        /// <param name="start">The starting number of parameters.</param>
        /// <param name="end">The ending number of parameters.</param>
        /// <param name="filepath">The path to save the generated JSON data.</param>
        public static void GenerateMethodDescriptionsAndSave(int start, int end, string filepath)
        {
            Dictionary<string, MethodDescriptor> data = new Dictionary<string, MethodDescriptor>();

            for (int i = start; i <= end; i++)
            {
                var methodDescriptor = GenerateMethodDescription(i);
                data[methodDescriptor.MethodName] = methodDescriptor;
            }

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filepath, json);
        }

        /// <summary>
        ///     Generates a random string of the specified length.
        /// </summary>
        /// <param name="length">The desired length of the generated string.</param>
        /// <returns>A random string.</returns>
        public static string GetRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            return new string(
                Enumerable.Repeat(chars, length)
                    .Select((string s) => s[rand.Next(s.Length)])
                    .ToArray());
        }

        /// <summary>
        ///     Represents the description of a method, including its name and parameters.
        /// </summary>
        public class MethodDescriptor
        {
            public string MethodName { get; set; }

            public List<ParameterDescriptor> Parameters { get; set; }
        }

        /// <summary>
        ///     Represents the description of a method parameter, including its name and type.
        /// </summary>
        public class ParameterDescriptor
        {
            public string ParameterName { get; set; }

            public string ParameterType { get; set; }
        }
    }

    /// <summary>
    ///     A benchmarking class to compare the performance between using strings and StringBuilders.
    /// </summary>
    [MemoryDiagnoser]
    public class StringVsStringBuilderBenchmark
    {
        /// <summary>
        ///     Holds the MethodInfo object.
        /// </summary>
        private MethodInfo method;

        /// <summary>
        ///     Gets a range of values for the parameter.
        /// </summary>
        public static IEnumerable<int> ValuesForParam => Enumerable.Range(Const.ArgNumStart, Const.ArgNumEnd);

        /// <summary>
        ///     Gets or sets the parameter count.
        /// </summary>
        [ParamsSource(nameof(ValuesForParam))]
        public int ParameterCount { get; set; }

        /// <summary>
        ///     Setup method to initialize the benchmark.
        /// </summary>
        [GlobalSetup]
        public void Setup()
        {
            // Get method from json
            var correctFileDir = Directory.GetCurrentDirectory();
            for (int i = 0; i < 4; i++)
            {
                correctFileDir = Path.GetDirectoryName(correctFileDir);
            }

            var correctFilePath = Path.Combine(correctFileDir, Const.DataFileName);

            var methodName = $"{Const.MethodPrefix}_{ParameterCount}";
            MethodInfo methodInfo = MethodInfoRebuilder.RebuildMethodFromJson(correctFilePath, methodName);
            method = methodInfo;

            // Show information for user
            Console.WriteLine("###################################################################");
            Console.WriteLine("Generated Method: " + OriginalMethod(method));
            Console.WriteLine("###################################################################");
        }

        /// <summary>
        ///     Benchmarks the method using char delimeter with StringBuilder.
        /// </summary>
        /// <returns>The generated string.</returns>
        [Benchmark]
        public string UsingNewMethodWithCharDelimeter()
        {
            return NewMethodWithCharDelimeter(method);
        }

        /// <summary>
        ///     Benchmarks the method using string delimeter with StringBuilder.
        /// </summary>
        /// <returns>The generated string.</returns>
        [Benchmark]
        public string UsingNewMethodWithStringDelimeter()
        {
            return NewMethodWithStringDelimeter(method);
        }

        /// <summary>
        ///     Benchmarks the original method.
        /// </summary>
        [Benchmark]
        public string UsingOriginalMethod()
        {
            return OriginalMethod(method);
        }

        /// <summary>
        ///     Generates a string representation of the method using char delimeters.
        /// </summary>
        /// <param name="method">The method info object.</param>
        /// <returns>The generated string.</returns>
        private string NewMethodWithCharDelimeter(MethodInfo method)
        {
            var builder = new StringBuilder(method.Name);
            var parameters = method.GetParameters();

            if (parameters.Length > 0)
            {
                builder.Append('(');

                foreach (var parameter in parameters)
                {
                    builder.Append(parameter.Name);
                    builder.Append(',');
                }

                builder = builder.Remove(builder.Length - 1, 1);
                builder.Append(')');
            }

            return builder.ToString();
        }

        /// <summary>
        ///     Generates a string representation of the method using string delimeters.
        /// </summary>
        /// <param name="method">The method info object.</param>
        /// <returns>The generated string.</returns>
        private string NewMethodWithStringDelimeter(MethodInfo method)
        {
            var builder = new StringBuilder(method.Name);
            var parameters = method.GetParameters();

            if (parameters.Length > 0)
            {
                builder.Append("(");

                foreach (var parameter in parameters)
                {
                    builder.Append(parameter.Name);
                    builder.Append(",");
                }

                builder = builder.Remove(builder.Length - 1, 1);
                builder.Append(")");
            }

            return builder.ToString();
        }

        /// <summary>
        ///     Generates a string representation of the method using the original method.
        /// </summary>
        /// <param name="method">The method info object.</param>
        /// <returns>The generated string.</returns>
        private string OriginalMethod(MethodInfo method)
        {
            var message = method.Name;
            var parameters = method.GetParameters();

            if (parameters.Length > 0)
            {
                message += "(";

                foreach (var parameter in parameters)
                {
                    var paramName = parameter.Name;
                    message += paramName + ",";
                }

                message = message.Remove(message.Length - 1, 1);
                message += ")";
            }

            return message;
        }
    }
}
