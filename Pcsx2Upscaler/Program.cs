using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace dotHackUpscaler
{
    class Program
    {
        class Arguments
        {
            public string pcsx2Directory { get; set; }
            public string gameHash { get; set; }
            public string waifu2xCaffePath  { get; set; }
            public string waifu2xCaffeParameters  { get; set; }
            public string texConvPath  { get; set; }
        }

        static void RunProcess(string command, string arguments, string workingDirectory = "")
        {
            Console.WriteLine($"\n\n{command} {arguments}");
            var info = new ProcessStartInfo();
            info.FileName = command;
            info.Arguments = arguments;
            info.WorkingDirectory = workingDirectory;
            Process.Start(info).WaitForExit();
        }

        static void Main(string[] args)
        {
            var options = new JsonSerializerOptions
            {
                AllowTrailingCommas = true
            };

            if (args.Length == 0)
            {
                Console.WriteLine("Need a json args file.");
                return;
            }

            var text = File.ReadAllText(args[0]);
            var arguments = JsonSerializer.Deserialize<Arguments>(File.ReadAllText(args[0]), options);

            Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
            arguments.waifu2xCaffePath = Path.GetFullPath(arguments.waifu2xCaffePath);

            string textureSwapConfigFile = Path.Combine(arguments.pcsx2Directory, "txtconfig", $"{arguments.gameHash}.yaml");
            string origTexturesPath = Path.Combine(arguments.pcsx2Directory, "textures", "@DUMP", arguments.gameHash);
            string convertedTexturePath = Path.Combine(arguments.pcsx2Directory, "textures", "@REPLACE", arguments.gameHash);

            var textureReplacementYaml = new StringBuilder();
            textureReplacementYaml.AppendLine("ProcessTEX:");

            Directory.CreateDirectory(convertedTexturePath);

            foreach (var file in Directory.GetFiles(origTexturesPath))
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                var ddsToPng = Path.Combine(convertedTexturePath, fileNameWithoutExtension + ".png");
                var pngToUpscaled = Path.Combine(convertedTexturePath, fileNameWithoutExtension + "_Upscaled.png");
                var upscaledToDds = Path.Combine(convertedTexturePath, fileNameWithoutExtension + "_Upscaled.dds");

                if (!File.Exists(ddsToPng))
                    RunProcess(arguments.texConvPath, $"\"{file}\" -ft png -o \"{convertedTexturePath}\"");

                if (!File.Exists(pngToUpscaled))
                    RunProcess(arguments.waifu2xCaffePath, $" {arguments.waifu2xCaffeParameters} -i \"{ddsToPng}\" -o \"{pngToUpscaled}\"");

                if (!File.Exists(upscaledToDds))
                {
                    RunProcess(arguments.texConvPath, $"\"{pngToUpscaled}\" -ft dds -f R8G8B8A8_UNORM -o \"{convertedTexturePath}\"");
                }

                textureReplacementYaml.AppendLine($"  0x{fileNameWithoutExtension}: \"@REPLACE/{arguments.gameHash}/{fileNameWithoutExtension}_Upscaled.dds\"");
            }

            File.WriteAllText(textureSwapConfigFile, textureReplacementYaml.ToString());
        }
    }
}
