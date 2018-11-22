using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextTemplating.VSHost;
using Scripty.Core;
using Scripty.Core.Output;

namespace Scripty
{
    /// <summary>
    /// This is the generator class. 
    /// When setting the 'Custom Tool' property of a C# or VB project item to "Scripty", 
    /// the GenerateCode function will get called and will return the contents of the generated file 
    /// to the project system
    /// </summary>
    //[ComVisible(true)]
    [Guid("1B8589A2-58FF-4413-9EA3-A66A1605F1E3")]
    //[CodeGeneratorRegistration(typeof(ScriptyGenerator), "C# Scripty Generator", "{FAE04EC1-301F-11D3-BF4B-00C04F79EFBC}", GeneratesDesignTimeSource = true)]
    //[CodeGeneratorRegistration(typeof(ScriptyGenerator), "VB.NET Scripty Generator", "{164B10B9-B200-11D0-8C61-00A0C91E29D5}", GeneratesDesignTimeSource = true)]
    //[ProvideObject(typeof(ScriptyGenerator))]
    public class ScriptyGenerator : BaseCodeGeneratorWithSite
    {
        public ScriptyGenerator()
        {

        }
#pragma warning disable 0414, 169
        //The name of this generator (use for 'Custom Tool' property of project item)
        // ReSharper disable once InconsistentNaming
        internal static string name = "ScriptyGenerator";
#pragma warning restore 0414, 169
        
        public override string GetDefaultExtension()
        {
            return ".log";
        }

        /// <summary>
        /// Function that builds the contents of the generated file based on the contents of the input file.
        /// </summary>
        /// <param name="inputFileContent">Content of the input file</param>
        /// <returns>Generated file as a byte array</returns>
        protected override byte[] GenerateCode(string inputFileName, string inputFileContent)
        {
            // Some good examples: https://t4toolbox.svn.codeplex.com/svn/Source/DteProcessor.cs
            // And https://github.com/madskristensen/ExtensibilityTools/blob/master/src/VsixManifest/Generator/ResxFileGenerator.cs

            try
            {
                ProjectItem projectItem = GetProjectItem();
                string inputFilePath = projectItem.Properties.Item("FullPath").Value.ToString();
                Project project = projectItem.ContainingProject;
                Solution solution = projectItem.DTE.Solution;

                // Run the generator and get the results
                ScriptSource source = new ScriptSource(inputFilePath, inputFileContent);
                ScriptEngine engine = new ScriptEngine(project.FullName, solution.FullName, null);
                ScriptResult result = engine.Evaluate(source).Result;

                // Report errors
                if (result.Messages.Count > 0)
                {
                    foreach (ScriptMessage error in result.Messages)
                    {
                        switch (error.MessageType) {
                            case MessageType.Error:
                                GeneratorError(4, error.Message, error.Line, error.Column);
                                break;
                            case MessageType.Warning:
                                GeneratorWarning(4, error.Message, error.Line, error.Column);
                                break;
                        }
                    }
                    return null;
                }
                
                // Add generated files to the project
                foreach (IOutputFileInfo outputFile in result.OutputFiles.Where(x => x.BuildAction != BuildAction.GenerateOnly))
                {
                    ProjectItem outputItem = projectItem.ProjectItems.Cast<ProjectItem>()
                        .FirstOrDefault(x => x.Properties.Item("FullPath")?.Value?.ToString() == outputFile.FilePath)
                        ?? projectItem.ProjectItems.AddFromFile(outputFile.FilePath);
                    outputItem.Properties.Item("ItemType").Value = outputFile.BuildAction.ToString();
                }

                // Remove/delete files from the last generation but not in this one
                string logPath = Path.ChangeExtension(inputFilePath, ".log");
                if (File.Exists(logPath))
                {
                    string[] logLines = File.ReadAllLines(logPath);
                    foreach (string fileToRemove in logLines.Where(x => result.OutputFiles.All(y => y.FilePath != x)))
                    {
                        solution.FindProjectItem(fileToRemove)?.Delete();
                    }
                }

                // Create the log file
                return Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, result.OutputFiles.Select(x => x.FilePath)));
            }
            catch (Exception ex)
            {
                GeneratorError(4, ex.ToString(), 0, 0);
                return null;
            }
        }

        /// <summary>
        /// Returns the EnvDTE.ProjectItem object that corresponds to the project item the code 
        /// generator was called on
        /// </summary>
        /// <returns>The EnvDTE.ProjectItem of the project item the code generator was called on</returns>
        protected ProjectItem GetProjectItem()
        {
            object p = GetService(typeof(ProjectItem));
            Debug.Assert(p != null, "Unable to get Project Item.");
            return (ProjectItem)p;
        }

        /// <summary>
        /// Method that will communicate an error via the shell callback mechanism
        /// </summary>
        /// <param name="level">Level or severity</param>
        /// <param name="message">Text displayed to the user</param>
        /// <param name="line">Line number of error</param>
        /// <param name="column">Column number of error</param>
        protected virtual void GeneratorError(int level, string message, int line, int column)
        {
            GeneratorErrorCallback(false, level, message, line, column);
        }

        /// <summary>
        /// Method that will communicate a warning via the shell callback mechanism
        /// </summary>
        /// <param name="level">Level or severity</param>
        /// <param name="message">Text displayed to the user</param>
        /// <param name="line">Line number of warning</param>
        /// <param name="column">Column number of warning</param>
        protected virtual void GeneratorWarning(int level, string message, int line, int column)
        {
            GeneratorErrorCallback(true, level, message, line, column);
        }
    }
}
