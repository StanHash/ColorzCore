﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ColorzCore.DataTypes;
using ColorzCore.Lexer;
using ColorzCore.Parser;
using ColorzCore.Parser.AST;
using System.IO;

namespace ColorzCore.Preprocessor.Directives
{
    class IncludeToolEventDirective : IDirective
    {
        public int MinParams { get { return 1; } }
        public int? MaxParams { get { return null; } }
        public bool RequireInclusion { get { return true; } }

        public Maybe<ILineNode> Execute(EAParser parse, Token self, IList<IParamNode> parameters, MergeableGenerator<Token> tokens)
        {
            Maybe<string> validFile = IO.IOUtility.FindFile(self.FileName, GetFileName(parameters[0].ToString()));
            if (validFile.IsNothing)
            {
                parse.Error(parameters[0].MyLocation, "Tool " + parameters[0].ToString() + " not found.");
                return new Nothing<ILineNode>();
            }

            //from http://stackoverflow.com/a/206347/1644720
            // Start the child process.
            System.Diagnostics.Process p = new System.Diagnostics.Process();
            // Redirect the output stream of the child process.
            p.StartInfo.WorkingDirectory = Path.GetDirectoryName(self.FileName);
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = validFile.FromJust;
            StringBuilder argumentBuilder = new StringBuilder();
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].Type == ParamType.STRING)
                {
                    argumentBuilder.Append('"');
                    argumentBuilder.Append(parameters[i].ToString());
                    argumentBuilder.Append('"');
                }
                else
                {
                    argumentBuilder.Append(parameters[i].ToString());
                }
                argumentBuilder.Append(' ');
            }
            argumentBuilder.Append("--to-stdout");
            p.StartInfo.Arguments = argumentBuilder.ToString();
            p.Start();
            // Do not wait for the child process to exit before
            // reading to the end of its redirected stream.
            // p.WaitForExit();
            // Read the output stream first and then wait.
            MemoryStream outputBytes = new MemoryStream();
            MemoryStream errorStream = new MemoryStream();
            p.StandardOutput.BaseStream.CopyTo(outputBytes);
            p.StandardError.BaseStream.CopyTo(errorStream);
            p.WaitForExit();

            byte[] output = outputBytes.GetBuffer().Take((int)outputBytes.Length).ToArray();
            if(errorStream.Length > 0)
            {
                parse.Error(self.Location, Encoding.ASCII.GetString(errorStream.GetBuffer().Take((int)errorStream.Length).ToArray()));
            }
            else if (output.Length >= 7 && Encoding.ASCII.GetString(output.Take(7).ToArray()) == "ERROR: ")
            {
                parse.Error(self.Location, Encoding.ASCII.GetString(output.Take(7).ToArray()));
            }
            else
            {
                Tokenizer t = new Tokenizer();
                tokens.PrependEnumerator(t.Tokenize(new BufferedStream(outputBytes), self.FileName + ":" + parameters[0].ToString()).GetEnumerator());
            }
            return new Nothing<ILineNode>();
        }

        private string GetFileName(string toolName)
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                case PlatformID.MacOSX:
                    return "\"./Tools/" + toolName + "\"";
                default:
                    return "\".\\Tools\\" + toolName + ".exe\"";
            }
        }
    }
}