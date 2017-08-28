namespace DotaLoadScreenExport
{
    using CommandLine;
    using System.IO;

    public class Options
    {
        [Option(shortName: 'd', longName: "dotaPath", Required = true, HelpText = "Path to the Dota 2 directory.")]
        public string Dota2DirPath { get; set; }

        [Option(shortName: 'v', longName: "verbose", DefaultValue = false, HelpText = "Prints all messages to standard output.")]
        public bool Verbose { get; set; }
        [ValueOption(0)]
        public string OutDirPath { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return "dotals -d <path to dota2> <outdir>";
        }
    }
}
