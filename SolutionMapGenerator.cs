using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SolutionMapper
{
    internal class SolutionMapGenerator
    {
        public enum OutputFormat
        {
            Text,
            Markdown,
            Html,
            Json,
            Yaml,
            Mermaid
        }

        private readonly string[] _excludedExtensions = { ".user", ".suo", ".csproj", ".json", ".sln" };

        private readonly string[] _excludedFolders =
            { ".vs", "bin", "obj", "packages", "node_modules", "wwwroot", "properties", ".git", ".svn", ".hg", ".bzr", "_darcs" };

        private readonly bool _includeCodeDetails;
        private readonly StringBuilder _stringBuilder = new StringBuilder();

        public SolutionMapGenerator(bool includeCodeDetails = false)
        {
            _includeCodeDetails = includeCodeDetails;
        }

        public string GenerateStructure(string rootPath, OutputFormat format)
        {
            _stringBuilder.Clear();
            switch (format)
            {
                case OutputFormat.Markdown:
                    ProcessDirectoryMarkdown(rootPath, 0);
                    break;
                case OutputFormat.Html:
                    GenerateHtmlStructure(rootPath);
                    break;
                case OutputFormat.Json:
                    return GenerateJsonStructure(rootPath);
                case OutputFormat.Yaml:
                    return GenerateYamlStructure(rootPath);
                case OutputFormat.Mermaid:
                    GenerateMermaidStructure(rootPath);
                    break;
                case OutputFormat.Text:
                default:
                    ProcessDirectory(rootPath, 0);
                    break;
            }

            return _stringBuilder.ToString();
        }

        private void ProcessCodeFile(string filePath, int level, OutputFormat format)
        {
            if (!_includeCodeDetails) return;
            if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                var code = File.ReadAllText(filePath);
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetCompilationUnitRoot();

                foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    var indent = format == OutputFormat.Markdown
                        ? new string('#', level + 2)
                        : new string(' ', (level + 1) * 3);
                    var prefix = format == OutputFormat.Markdown ? "" : "* ";
                    _stringBuilder.AppendLine($"{indent} {prefix}Class: {classDecl.Identifier}");

                    foreach (var methodDecl in classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>())
                    {
                        var methodIndent = format == OutputFormat.Markdown
                            ? new string('#', level + 3)
                            : new string(' ', (level + 2) * 3);
                        _stringBuilder.AppendLine($"{methodIndent} {prefix}Method: {methodDecl.Identifier}");
                    }
                }
            }
            catch (Exception ex)
            {
                _stringBuilder.AppendLine($"{new string(' ', (level + 1) * 3)}* Error parsing file: {ex.Message}");
            }
        }

        private void ProcessDirectory(string path, int level)
        {
            var directoryInfo = new DirectoryInfo(path);
            _stringBuilder.AppendLine($"{new string(' ', level * 3)}* {directoryInfo.Name}");

            foreach (var dir in directoryInfo.GetDirectories())
                if (!Array.Exists(_excludedFolders, x => x.Equals(dir.Name, StringComparison.OrdinalIgnoreCase)))
                    ProcessDirectory(dir.FullName, level + 1);

            foreach (var file in directoryInfo.GetFiles())
                if (!Array.Exists(_excludedExtensions,
                        x => x.Equals(file.Extension, StringComparison.OrdinalIgnoreCase)))
                {
                    _stringBuilder.AppendLine($"{new string(' ', (level + 1) * 3)}* {file.Name}");
                    ProcessCodeFile(file.FullName, level + 1, OutputFormat.Markdown);
                }
        }

        private void ProcessDirectoryMarkdown(string path, int level)
        {
            var directoryInfo = new DirectoryInfo(path);
            _stringBuilder.AppendLine($"{new string('#', level + 1)} {directoryInfo.Name}");

            foreach (var dir in directoryInfo.GetDirectories())
                if (!Array.Exists(_excludedFolders, x => x.Equals(dir.Name, StringComparison.OrdinalIgnoreCase)))
                    ProcessDirectoryMarkdown(dir.FullName, level + 1);

            foreach (var file in directoryInfo.GetFiles())
                if (!Array.Exists(_excludedExtensions,
                        x => x.Equals(file.Extension, StringComparison.OrdinalIgnoreCase)))
                {
                    _stringBuilder.AppendLine($"- {file.Name}");
                    ProcessCodeFile(file.FullName, level, OutputFormat.Markdown);
                }
        }

        private void GenerateHtmlStructure(string rootPath)
        {
            _stringBuilder.AppendLine("<!DOCTYPE html><html><head>");
            _stringBuilder.AppendLine("<style>");
            _stringBuilder.AppendLine(".tree-node { margin-left: 20px; }");
            _stringBuilder.AppendLine(".folder { cursor: pointer; }");
            _stringBuilder.AppendLine(".collapsed .tree-node { display: none; }");
            _stringBuilder.AppendLine(".code-details { margin-left: 40px; color: #0066cc; }");
            _stringBuilder.AppendLine(".method { margin-left: 20px; color: #006600; }");
            _stringBuilder.AppendLine("</style>");
            _stringBuilder.AppendLine("<script>");
            _stringBuilder.AppendLine("function toggle(elem) { elem.classList.toggle('collapsed'); }");
            _stringBuilder.AppendLine("</script></head><body>");

            ProcessDirectoryHtml(new DirectoryInfo(rootPath));
            _stringBuilder.AppendLine("</body></html>");
        }

        private void ProcessDirectoryHtml(DirectoryInfo directoryInfo, int level = 0)
        {
            _stringBuilder.AppendLine("<div class='folder' onclick='toggle(this)'>");
            _stringBuilder.AppendLine($"📁 {directoryInfo.Name}");
            _stringBuilder.AppendLine("<div class='tree-node'>");

            foreach (var dir in directoryInfo.GetDirectories())
                if (!Array.Exists(_excludedFolders, x => x.Equals(dir.Name, StringComparison.OrdinalIgnoreCase)))
                    ProcessDirectoryHtml(dir, level + 1);

            foreach (var file in directoryInfo.GetFiles())
                if (!Array.Exists(_excludedExtensions,
                        x => x.Equals(file.Extension, StringComparison.OrdinalIgnoreCase)))
                {
                    _stringBuilder.AppendLine($"📄 {file.Name}<br>");

                    if (_includeCodeDetails && file.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
                        try
                        {
                            var code = File.ReadAllText(file.FullName);
                            var tree = CSharpSyntaxTree.ParseText(code);
                            var root = tree.GetCompilationUnitRoot();

                            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                            {
                                _stringBuilder.AppendLine(
                                    $"<div class='code-details'>🔷 Class: {classDecl.Identifier}</div>");

                                foreach (var methodDecl in
                                         classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>())
                                    _stringBuilder.AppendLine(
                                        $"<div class='method'>🔸 Method: {methodDecl.Identifier}</div>");
                            }
                        }
                        catch (Exception ex)
                        {
                            _stringBuilder.AppendLine(
                                $"<div class='code-details'>Error parsing file: {ex.Message}</div>");
                        }
                }

            _stringBuilder.AppendLine("</div></div>");
        }

        private string GenerateJsonStructure(string rootPath)
        {
            return JsonSerializer.Serialize(
                GenerateJsonNode(new DirectoryInfo(rootPath)),
                new JsonSerializerOptions { WriteIndented = true }
            );
        }

        private object GenerateJsonNode(DirectoryInfo directory)
        {
            var children = new List<object>();

            foreach (var dir in directory.GetDirectories())
                if (!Array.Exists(_excludedFolders, x => x.Equals(dir.Name, StringComparison.OrdinalIgnoreCase)))
                    children.Add(GenerateJsonNode(dir));

            foreach (var file in directory.GetFiles())
                if (!Array.Exists(_excludedExtensions,
                        x => x.Equals(file.Extension, StringComparison.OrdinalIgnoreCase)))
                {
                    var fileNode = new Dictionary<string, object>
                    {
                        { "name", file.Name },
                        { "type", "file" }
                    };

                    if (_includeCodeDetails && file.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
                        try
                        {
                            var code = File.ReadAllText(file.FullName);
                            var tree = CSharpSyntaxTree.ParseText(code);
                            var root = tree.GetCompilationUnitRoot();

                            var classes = root.DescendantNodes()
                                .OfType<ClassDeclarationSyntax>()
                                .Select(classDecl => new
                                {
                                    name = classDecl.Identifier.ToString(),
                                    methods = classDecl.DescendantNodes()
                                        .OfType<MethodDeclarationSyntax>()
                                        .Select(m => m.Identifier.ToString())
                                        .ToList()
                                })
                                .ToList();

                            fileNode["classes"] = classes;
                        }
                        catch
                        {
                        }

                    children.Add(fileNode);
                }

            return new
            {
                name = directory.Name,
                type = "directory",
                children
            };
        }

        private void GenerateMermaidStructure(string rootPath)
        {
            _stringBuilder.AppendLine("graph TD");
            ProcessDirectoryMermaid(new DirectoryInfo(rootPath), "");
        }

        private void ProcessDirectoryMermaid(DirectoryInfo directory, string parentId)
        {
            var currentId = parentId + directory.Name.Replace(" ", "_");

            if (!string.IsNullOrEmpty(parentId))
                _stringBuilder.AppendLine($"    {parentId}-->{currentId}[{directory.Name}]");
            else
                _stringBuilder.AppendLine($"    {currentId}[{directory.Name}]");

            foreach (var dir in directory.GetDirectories())
                if (!Array.Exists(_excludedFolders, x => x.Equals(dir.Name, StringComparison.OrdinalIgnoreCase)))
                    ProcessDirectoryMermaid(dir, currentId);

            foreach (var file in directory.GetFiles())
                if (!Array.Exists(_excludedExtensions,
                        x => x.Equals(file.Extension, StringComparison.OrdinalIgnoreCase)))
                {
                    var fileId = currentId + file.Name.Replace(" ", "_");
                    _stringBuilder.AppendLine($"    {currentId}-->{fileId}(({file.Name}))");

                    if (_includeCodeDetails && file.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
                        try
                        {
                            var code = File.ReadAllText(file.FullName);
                            var tree = CSharpSyntaxTree.ParseText(code);
                            var root = tree.GetCompilationUnitRoot();

                            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                            {
                                var classId = fileId + classDecl.Identifier;
                                _stringBuilder.AppendLine($"    {fileId}-->|class|{classId}[{classDecl.Identifier}]");

                                foreach (var methodDecl in classDecl.DescendantNodes()
                                             .OfType<MethodDeclarationSyntax>())
                                {
                                    var methodId = classId + methodDecl.Identifier;
                                    _stringBuilder.AppendLine(
                                        $"    {classId}-->|method|{methodId}({methodDecl.Identifier})");
                                }
                            }
                        }
                        catch
                        {
                        }
                }
        }

        private string GenerateYamlStructure(string rootPath)
        {
            var stringBuilder = new StringBuilder();
            ProcessDirectoryYaml(new DirectoryInfo(rootPath), stringBuilder, 0);
            return stringBuilder.ToString();
        }

        private void ProcessDirectoryYaml(DirectoryInfo directory, StringBuilder sb, int level)
        {
            var indent = new string(' ', level * 2);
            sb.AppendLine($"{indent}{directory.Name}:");

            foreach (var dir in directory.GetDirectories())
                if (!Array.Exists(_excludedFolders, x => x.Equals(dir.Name, StringComparison.OrdinalIgnoreCase)))
                    ProcessDirectoryYaml(dir, sb, level + 1);

            foreach (var file in directory.GetFiles())
                if (!Array.Exists(_excludedExtensions,
                        x => x.Equals(file.Extension, StringComparison.OrdinalIgnoreCase)))
                {
                    sb.AppendLine($"{indent}  - {file.Name}");

                    if (_includeCodeDetails && file.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
                        try
                        {
                            var code = File.ReadAllText(file.FullName);
                            var tree = CSharpSyntaxTree.ParseText(code);
                            var root = tree.GetCompilationUnitRoot();

                            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                            {
                                sb.AppendLine($"{indent}    class: {classDecl.Identifier}");

                                foreach (var methodDecl in
                                         classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>())
                                    sb.AppendLine($"{indent}      method: {methodDecl.Identifier}");
                            }
                        }
                        catch
                        {
                        }
                }
        }
    }
}