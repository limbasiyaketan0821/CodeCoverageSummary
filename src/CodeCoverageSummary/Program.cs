using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace CodeCoverageSummary
{
    internal static class Program
    {
        // test file: /Dev/Csharp/CodeCoverageSummary/coverage.cobertura.xml
        static List<CodeSummary> codeSummaries = new List<CodeSummary>();
        static List<string> files = new List<string>();
        private static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<CommandLineOptions>(args)
                                       .MapResult(o =>
                                       {
                                           try
                                           {
                                               if (o.Files != null)
                                               {
                                                   if (o.Files.Contains(','))
                                                   {
                                                       files = o.Files.Split(',').ToList();
                                                   }
                                                   else
                                                   {
                                                       files.Add(o.Files);
                                                   }

                                                   Console.WriteLine("Files:" + files.Count);
                                               }
                                               if (files.Count > 0)
                                               {
                                                   foreach (var file in files)
                                                   {
                                                       Console.WriteLine($"Code Coverage File: {file}");
                                                   }
                                                   for (int i = 0; i < files.Count; i++)
                                                   {
                                                       string fileName = files[i].Trim();
                                                       Console.WriteLine($"Trimmed Filename: {fileName}");
                                                       // parse code coverage file
                                                       CodeSummary summary = ParseTestResults(fileName);
                                                       if (summary == null)
                                                       {
                                                           Console.WriteLine("Error: Parsing code coverage file.");
                                                           return -2; // error
                                                       }
                                                       codeSummaries.Add(summary);
                                                   }
                                               }
                                               else
                                               {
                                                   Console.WriteLine("Error:no files found.");
                                                   return -2; // error
                                               }

                                               if (codeSummaries.Count > 0)
                                               {
                                                   // generate badge
                                                   string badgeUrl = o.Badge ? GenerateBadge(codeSummaries[1]) : null;

                                                   // generate output
                                                   string output;
                                                   double diff;
                                                   string fileExt;
                                                   if (o.Format.Equals("text", StringComparison.OrdinalIgnoreCase))
                                                   {
                                                       fileExt = "txt";
                                                       output = GenerateTextOutput(codeSummaries.ToArray(), badgeUrl);
                                                   }
                                                   else if (o.Format.Equals("md", StringComparison.OrdinalIgnoreCase) || o.Format.Equals("markdown", StringComparison.OrdinalIgnoreCase))
                                                   {
                                                       fileExt = "md";
                                                       (output, diff) = GenerateMarkdownOutput(codeSummaries.ToArray(), badgeUrl, o.AllowedCoverageDiff);
                                                       Console.WriteLine($"diff: {String.Format("{0:P2}", diff):N0}");
                                                       if (diff*100 < o.AllowedCoverageDiff)
                                                       {
                                                           Console.WriteLine($"Error: Code Coverage decreased by more than {o.AllowedCoverageDiff}%");
                                                           return -2; // error
                                                       }
                                                   }
                                                   else
                                                   {
                                                       Console.WriteLine("Error: Unknown output format.");
                                                       return -2; // error
                                                   }

                                                   // output
                                                   if (o.Output.Equals("console", StringComparison.OrdinalIgnoreCase))
                                                   {
                                                       Console.WriteLine(output);
                                                   }
                                                   else if (o.Output.Equals("file", StringComparison.OrdinalIgnoreCase))
                                                   {
                                                       File.WriteAllText($"code-coverage-results.{fileExt}", output);
                                                   }
                                                   else if (o.Output.Equals("both", StringComparison.OrdinalIgnoreCase))
                                                   {
                                                       Console.WriteLine(output);
                                                       File.WriteAllText($"code-coverage-results.{fileExt}", output);
                                                   }
                                                   else
                                                   {
                                                       Console.WriteLine("Error: Unknown output type.");
                                                       return -2; // error
                                                   }
                                               }
                                               else
                                               {
                                                   Console.WriteLine("No CodeSummaries found.");
                                                   return -2; // error
                                               }

                                               return 0;
                                           }

                                           catch (Exception ex)
                                           {
                                               Console.WriteLine($"Error: {ex.GetType()} - {ex.Message}");
                                               return -3; // unhandled error
                                           }
                                       },
                                       errs => -1); // invalid arguments
        }
        private static CodeSummary ParseTestResults(string filename)
        {
            CodeSummary summary = new();
            try
            {
                string rss = File.ReadAllText(filename);

                var xdoc = XDocument.Parse(rss);

                // test coverage for solution
                var coverage = from item in xdoc.Descendants("coverage")
                               select item;

                var lineR = from item in coverage.Attributes()
                            where item.Name == "line-rate"
                            select item;
                summary.LineRate = ((double)lineR.First());
                Console.WriteLine("LineRate:" + summary.LineRate);

                var linesCovered = from item in coverage.Attributes()
                                   where item.Name == "lines-covered"
                                   select item;
                summary.LinesCovered = int.Parse(linesCovered.First().Value);

                var linesValid = from item in coverage.Attributes()
                                 where item.Name == "lines-valid"
                                 select item;
                summary.LinesValid = int.Parse(linesValid.First().Value);

                var branchR = from item in coverage.Attributes()
                              where item.Name == "branch-rate"
                              select item;
                summary.BranchRate = double.Parse(branchR.First().Value);

                var branchesCovered = from item in coverage.Attributes()
                                      where item.Name == "branches-covered"
                                      select item;
                summary.BranchesCovered = int.Parse(branchesCovered.First().Value);

                var branchesValid = from item in coverage.Attributes()
                                    where item.Name == "branches-valid"
                                    select item;
                summary.BranchesValid = int.Parse(branchesValid.First().Value);

                summary.Complexity = 0;

                // test coverage for individual packages
                var packages = from item in coverage.Descendants("package")
                               select item;

                foreach (var item in packages)
                {
                    CodeCoverage packageCoverage = new()
                    {
                        Name = item.Attribute("name").Value,
                        LineRate = ((double)item.Attribute("line-rate")),
                        // BranchRate = double.Parse(item.Attribute("branch-rate").Value),
                        //Complexity = int.Parse(item.Attribute("complexity").Value)
                    };

                    summary.Packages.Add(packageCoverage);
                    //summary.Complexity += packageCoverage.Complexity;
                }

                return summary;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Parse Error: {ex.Message}");
                return null;
            }
        }

        private static string GenerateBadge(CodeSummary summary)
        {
            string colour;
            if (summary.LineRate < 0.5)
            {
                colour = "critical";
            }
            else if (summary.LineRate < 0.75)
            {
                colour = "yellow";
            }
            else
            {
                colour = "success";
            }
            return $"https://img.shields.io/badge/Code%20Coverage-{summary.LineRate*100:N0}%25-{colour}?style=flat";
        }

        private static string GenerateTextOutput(CodeSummary[] summaries, string badgeUrl)
        {
            StringBuilder textOutput = new();

            if (!string.IsNullOrWhiteSpace(badgeUrl))
            {
                textOutput.AppendLine(badgeUrl);
            }

            var diff_LineRate = summaries[0].LineRate - summaries[1].LineRate;
            var diff_LineCovered = summaries[0].LinesCovered - summaries[1].LinesCovered;
            var diff_LineValid = summaries[0].LinesValid - summaries[1].LinesValid;

            textOutput.AppendLine($"Base Coverage Rate = {summaries[0].LineRate * 100:N0}%")
                      .AppendLine($"Current Coverage Rate = {summaries[1].LineRate * 100:N0}%")
                      .AppendLine($"Differnece Rate = {diff_LineRate * 100:N0}%")
                      .AppendLine($"status = {summaries[1].Status}");

            for (int i = 0; i < summaries.Length; i++)
            {
                foreach (var package in summaries[i].Packages)
                {
                    textOutput.AppendLine($"{package.Name}: Line Rate = {package.LineRate * 100:N0}%");
                }
            }

            return textOutput.ToString();
        }

        private static Tuple<string, double> GenerateMarkdownOutput(CodeSummary[] summaries, string badgeUrl, double diff_coverage)
        {
            string status = string.Empty;
            StringBuilder markdownOutput = new();
            double diffLineRate = 0;

            if (!string.IsNullOrWhiteSpace(badgeUrl))
            {
                markdownOutput.AppendLine($"![Code Coverage]({badgeUrl})");
                markdownOutput.AppendLine("");
            }

            diffLineRate = summaries[1].LineRate - summaries[0].LineRate;
            if (diffLineRate*100 < 0)
            {
                status = ":small_red_triangle_down:";
                markdownOutput.AppendLine($"Code coverage **decreased 🔻** by **{String.Format("{0:P2}", diffLineRate):N0}**");
                markdownOutput.AppendLine("");
            }
            else if (diffLineRate == 0)
            {
                status = "\uFF1D";
                markdownOutput.AppendLine($"Code coverage has been not changed ==.");
                markdownOutput.AppendLine("");
            }
            else
            {
                status = ":white_check_mark:";
                markdownOutput.AppendLine($"Code coverage **increased 👍** by **{String.Format("{0:P2}", diffLineRate):N0}**");
                markdownOutput.AppendLine("");
            }

            var diff_LineCovered = summaries[1].LinesCovered - summaries[0].LinesCovered;
            var diff_LineValid = summaries[1].LinesValid - summaries[0].LinesValid;

           
            markdownOutput.AppendLine("Package | Base Branch | Current Branch | Difference | Status")
                          .AppendLine("-------- | --------- | ----------- | ---------- | ----------");

            markdownOutput.Append($"**Summary** | **{String.Format("{0:P2}", summaries[0].LineRate):N0}** | ")
                          .AppendLine($"**{String.Format("{0:P2}", summaries[1].LineRate):N0}** |  **{String.Format("{0:P2}", diffLineRate):N0}** | {status}");


            var dict1 = getLineRate(summaries[0].Packages.ToArray());

            var dict2 = getLineRate(summaries[1].Packages.ToArray());

            var dict3 = new Dictionary<string, double>();

            if (dict1.Keys.Count > dict2.Keys.Count)
            {
                foreach (var key in dict1.Keys)
                {
                    if (dict2.Keys.Contains(key))
                    {
                        diffLineRate = dict2[key] - dict1[key];
                        if (diffLineRate < 0)
                        {
                            status = ":small_red_triangle_down:";
                        }
                        else if (diffLineRate == 0)
                        {
                            status = "\uFF1D";
                        }
                        else
                        {
                            status = ":white_check_mark:";
                        }
                        markdownOutput.AppendLine($"{key} | {String.Format("{0:P2}", dict1[key]):N0} | {String.Format("{0:P2}", dict2[key]):N0} | {String.Format("{0:P2}", diffLineRate):NO} | {status} ");
                    }
                    else
                    {
                        dict3.Add(key, dict1[key]);
                        Console.WriteLine("dict3 key count:" + dict3.Keys.Count);
                        markdownOutput.AppendLine($"{key} | - | {String.Format("{0:P2}", dict3[key]):N0} | {String.Format("{0:P2}", dict3[key]):N0}  | ");
                    }
                }
            }
            else
            {
                foreach (var key in dict2.Keys)
                {
                    if (dict1.Keys.Contains(key))
                    {
                        diffLineRate= dict2[key] - dict1[key];
                        if (diffLineRate < 0)
                        {
                            status = ":small_red_triangle_down:";
                        }
                        else if (diffLineRate == 0)
                        {
                            status = "\uFF1D";
                        }
                        else
                        {
                            status = ":white_check_mark:";
                        }
                        markdownOutput.AppendLine($"{key} | {String.Format("{0:P2}", dict1[key]):N0} | {String.Format("{0:P2}", dict2[key]):N0} | {String.Format("{0:P2}", diffLineRate):NO} | {status} "); 
                    }
                    else
                    {
                        dict3.Add(key, dict2[key]);
                        Console.WriteLine("dict3----------------- key count:" + dict3.Keys.Count);
                        markdownOutput.AppendLine($"{key} | - | {String.Format("{0:P2}", dict3[key]):N0} | {String.Format("{0:P2}", dict3[key]):N0}  | ");
                    }
                }
            }
            return Tuple.Create(markdownOutput.ToString(), diffLineRate);
        }
        private static Dictionary<string, double> getLineRate(CodeCoverage[] packages)
        {
            var packageDict = new Dictionary<string, double>();
            var status = string.Empty;

            foreach (var package in packages)
            {
                packageDict.Add(package.Name, package.LineRate);
            }
            Console.WriteLine("Dictionary count:" + packageDict.Count);
            return packageDict;
        }

        private static string AddCheck(CodeSummary[] summaries, string badgeUrl)
        {
            string status = string.Empty;
            StringBuilder markdownOutput = new();

            if (!string.IsNullOrWhiteSpace(badgeUrl))
            {
                markdownOutput.AppendLine($"![Code Coverage]({badgeUrl})");
                markdownOutput.AppendLine("");
            }

            var diff_LineRate = summaries[1].LineRate - summaries[0].LineRate;

            status = diff_LineRate >= 0 ? ":white_check_mark:" : ":small_red_triangle_down:";
            var diff_LineCovered = summaries[1].LinesCovered - summaries[0].LinesCovered;
            var diff_LineValid = summaries[1].LinesValid - summaries[0].LinesValid;

            markdownOutput.AppendLine("Package | Base Branch | Current Branch | Difference | Status")
                          .AppendLine("-------- | --------- | ----------- | ---------- | ----------");

            markdownOutput.Append($"**Summary** | **{String.Format("{0:P2}", summaries[0].LineRate):N0}** | ")
                          .AppendLine($"**{String.Format("{0:P2}", summaries[1].LineRate):N0}** |  **{String.Format("{0:P2}", diff_LineRate):N0}** | {status}");


            var dict1 = getLineRate(summaries[0].Packages.ToArray());
            Console.WriteLine("dict1 key count:" + dict1.Keys.Count);
            var dict2 = getLineRate(summaries[1].Packages.ToArray());
            Console.WriteLine("dict2 key count:" + dict2.Keys.Count);
            var dict3 = new Dictionary<string, double>();

            if (dict1.Keys.Count >= dict2.Keys.Count)
            {
                foreach (var key in dict1.Keys)
                {
                    if (dict2.Keys.Contains(key) == false)
                    {
                        dict3.Add(key, dict2[key]);
                        Console.WriteLine("dict3 key count:" + dict2.Keys.Count);
                        markdownOutput.AppendLine($"{key} | - | {String.Format("{0:P2}", dict3[key]):N0} | {String.Format("{0:P2}", dict3[key]):N0}  | ");
                    }
                    else
                    {
                        var diff = dict2[key] - dict1[key];
                        if (diff < 0)
                        {
                            status = ":small_red_triangle_down:";
                        }
                        else
                        {
                            status = ":white_check_mark:";
                        }
                        markdownOutput.AppendLine($"{key} | {String.Format("{0:P2}", dict1[key]):N0} | {String.Format("{0:P2}", dict2[key]):N0} | {String.Format("{0:P2}", diff):NO} | {status} ");
                    }
                }
            }
            else
            {
                foreach (var key in dict2.Keys)
                {
                    if (dict1.Keys.Contains(key) == false)
                    {
                        dict3.Add(key, dict2[key]);
                        Console.WriteLine("dict3 key count:" + dict2.Keys.Count);
                        markdownOutput.AppendLine($"{key} | - | {String.Format("{0:P2}", dict3[key]):N0} | {String.Format("{0:P2}", dict3[key]):N0}  | ");
                    }
                    else
                    {
                        var diff = dict2[key] - dict1[key];
                        if (diff < 0)
                        {
                            status = ":small_red_triangle_down:";
                        }
                        else
                        {
                            status = ":white_check_mark:";
                        }
                        markdownOutput.AppendLine($"{key} | {String.Format("{0:P2}", dict1[key]):N0} | {String.Format("{0:P2}", dict2[key]):N0} | {String.Format("{0:P2}", diff):NO} | {status} ");
                    }
                }
            }
            return markdownOutput.ToString();
        }
    }
}

