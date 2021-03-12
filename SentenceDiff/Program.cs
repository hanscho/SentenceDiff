using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace SentenceDiff
{
    class Program
    {
        static void Main(string[] args)
        {
            var sourcePathA = args[0];
            var sourcePathB = args[1];

            var targetPathA = Path.GetTempFileName();
            var targetPathB = Path.GetTempFileName();

            SplitFile(sourcePathA, targetPathA);
            SplitFile(sourcePathB, targetPathB);

            var exitCode = RunDiff(targetPathA, targetPathB);
            Environment.Exit(exitCode);
        }

        private static int RunDiff(string pathA, string pathB)
        {
            var q = '"';
            var startInfo = new ProcessStartInfo
            {
                FileName = @"C:\Program Files\Perforce\p4merge.exe",
                Arguments = $"{q}{pathA}{q} {q}{pathB}{q}",
                WorkingDirectory = Environment.CurrentDirectory,
            };

            var process = Process.Start(startInfo);
            process.WaitForExit();

            return process.ExitCode;
        }

        private static void SplitFile(string sourcePath, string targetPath)
        {
            var sourceLines = File.ReadAllLines(sourcePath);
            var targetLines = SplitLines(sourceLines);
            File.WriteAllLines(targetPath, targetLines.ToArray());
        }

        private static List<string> SplitLines(string[] sourceLines)
        {
            var targetLines = new List<string>();
            foreach (var line in sourceLines)
            {
                SplitLine(targetLines, line);
            }

            return targetLines;
        }

        private static string[] Separators;

        private static void SplitLine(List<string> targetLines, string line)
        {
            // A sentence.
            // A sentence...
            // A sentence!
            // A sentence?
            // "A sentence," it is.
            // -- a sentence part.
            // -- a sentence part --
            // (a sentence part)

            if (line.StartsWith("---"))
            {
                targetLines.Add(line);
                return;
            }

            line = line.Trim();
            if (line.Length == 0)
            {
                targetLines.Add("");
                return;
            }

            int sentenceStartIndex = 0;
            bool mdashActive = false;
            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                var previousStartIndex = sentenceStartIndex;
                var lineLength = 0;
                var activateMdash = false;

                switch (c)
                {
                    case '!':
                    case '?':
                    case ')':
                        lineLength = 1 + i - sentenceStartIndex;
                        sentenceStartIndex = i + 1;
                        break;
                    case '(':
                        lineLength = i - sentenceStartIndex;
                        sentenceStartIndex = i;
                        break;
                    case '-':
                        if (i > 0 && line[i - 1] == '-')
                        {
                            if (mdashActive)
                            {
                                lineLength = 1 + i - sentenceStartIndex;
                                sentenceStartIndex = i + 1;
                            }
                            else
                            {
                                activateMdash = true;
                                lineLength = i - 1 - sentenceStartIndex;
                                sentenceStartIndex = i - 1;
                            }
                        }
                        break;
                    case '.':
                        while (i < line.Length && line[i] == '.')
                        {
                            i += 1;
                        }

                        lineLength = i - sentenceStartIndex;
                        sentenceStartIndex = i;
                        break;
                }
                
                if (lineLength > 0)
                {
                    var targetLine = line.Substring(previousStartIndex, lineLength);
                    targetLine = targetLine.Trim();
                    if (targetLine.Length > 0)
                    {
                        targetLines.Add(targetLine);
                    }

                    mdashActive = activateMdash;
                }
            }

            if (sentenceStartIndex < line.Length)
            {
                var targetLine = line.Substring(sentenceStartIndex, line.Length - sentenceStartIndex);
                if (targetLine.Trim().Length > 0)
                {
                    targetLines.Add(targetLine);
                }
            }
        }
    }
}
