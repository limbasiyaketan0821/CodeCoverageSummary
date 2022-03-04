using CommandLine;

namespace CodeCoverageSummary
{
    public class CommandLineOptions
    {
        [Value(index: 0, Required = true, HelpText = "Code coverage file to analyse.")]
        public string Files { get; set; }

        [Option(shortName: 'd', longName: "allow_coverage_diff", Required = false, HelpText = "Input allowed coverage difference between two branches in % - 0.1 /1 /10." , Default = -0.1)]
        public double AllowedCoverageDiff { get; set; }

        [Option(shortName: 'b', longName: "badge", Required = false, HelpText = "Include a badge in the output - true / false.", Default = false)]
        public bool Badge { get; set; }

        [Option(shortName: 'f', longName: "format", Required = false, HelpText = "Output Format - markdown or text.", Default = "text")]
        public string Format { get; set; }

        [Option(shortName: 'o', longName: "output", Required = false, HelpText = "Output Type - console, file or both.", Default = "console")]
        public string Output { get; set; }
    }
}
