using System;
using System.IO;
using System.Linq;
using System.Text;

namespace ProjectStructureExporter
{
    internal class SolutionMapGenerator
    {
        private readonly StringBuilder _stringBuilder = new StringBuilder();
        private readonly string[] _excludedFolders = new[] { ".vs", "bin", "obj", "packages", "node_modules", "wwwroot", "properties" };
        private readonly string[] _excludedExtensions = new[] { ".user", ".suo", ".csproj", ".json", ".sln" };

        public enum OutputFormat
        {
            Text,
            Markdown,
            Html,
            Json,
            Yaml,
            Mermaid
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
                default:
                    ProcessDirectory(rootPath, 0);
                    break;
            }
            return _stringBuilder.ToString();
        }

        private void ProcessDirectory(string path, int level)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            _stringBuilder.AppendLine($"{new string(' ', level * 3)}* {directoryInfo.Name}");

            foreach (DirectoryInfo dir in directoryInfo.GetDirectories())
            {
                if (!Array.Exists(_excludedFolders, x => x.Equals(dir.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    ProcessDirectory(dir.FullName, level + 1);
                }
            }

            foreach (FileInfo file in directoryInfo.GetFiles())
            {
                if (!Array.Exists(_excludedExtensions, x => x.Equals(file.Extension, StringComparison.OrdinalIgnoreCase)))
                {
                    _stringBuilder.AppendLine($"{new string(' ', (level + 1) * 3)}* {file.Name}");
                }
            }
        }

        private void ProcessDirectoryMarkdown(string path, int level)
        {
            var directoryInfo = new DirectoryInfo(path);
            _stringBuilder.AppendLine($"{new string('#', level + 1)} {directoryInfo.Name}");

            foreach (var dir in directoryInfo.GetDirectories())
            {
                if (!Array.Exists(_excludedFolders, x => x.Equals(dir.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    ProcessDirectoryMarkdown(dir.FullName, level + 1);
                }
            }

            foreach (var file in directoryInfo.GetFiles())
            {
                if (!Array.Exists(_excludedExtensions, x => x.Equals(file.Extension, StringComparison.OrdinalIgnoreCase)))
                {
                    _stringBuilder.AppendLine($"- {file.Name}");
                }
            }
        }

        private void GenerateHtmlStructure(string rootPath)
        {
            _stringBuilder.AppendLine("<!DOCTYPE html><html><head>");
            _stringBuilder.AppendLine("<style>");
            _stringBuilder.AppendLine(".tree-node { margin-left: 20px; }");
            _stringBuilder.AppendLine(".folder { cursor: pointer; }");
            _stringBuilder.AppendLine(".collapsed .tree-node { display: none; }");
            _stringBuilder.AppendLine("</style>");
            _stringBuilder.AppendLine("<script>");
            _stringBuilder.AppendLine("function toggle(elem) { elem.classList.toggle('collapsed'); }");
            _stringBuilder.AppendLine("</script></head><body>");

            ProcessDirectoryHtml(new DirectoryInfo(rootPath));
            _stringBuilder.AppendLine("</body></html>");
        }

        private void ProcessDirectoryHtml(DirectoryInfo directoryInfo, int level = 0)
        {
            _stringBuilder.AppendLine($"<div class='folder' onclick='toggle(this)'>");
            _stringBuilder.AppendLine($"📁 {directoryInfo.Name}");
            _stringBuilder.AppendLine("<div class='tree-node'>");

            foreach (var dir in directoryInfo.GetDirectories())
            {
                if (!Array.Exists(_excludedFolders, x => x.Equals(dir.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    ProcessDirectoryHtml(dir, level + 1);
                }
            }

            foreach (var file in directoryInfo.GetFiles())
            {
                if (!Array.Exists(_excludedExtensions, x => x.Equals(file.Extension, StringComparison.OrdinalIgnoreCase)))
                {
                    _stringBuilder.AppendLine($"📄 {file.Name}<br>");
                }
            }

            _stringBuilder.AppendLine("</div></div>");
        }

        private string GenerateJsonStructure(string rootPath)
        {
            return System.Text.Json.JsonSerializer.Serialize(
                GenerateJsonNode(new DirectoryInfo(rootPath)),
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
            );
        }

        private object GenerateJsonNode(DirectoryInfo directory)
        {
            var node = new
            {
                name = directory.Name,
                type = "directory",
                children = directory.GetDirectories()
                    .Where(dir => !Array.Exists(_excludedFolders, x => x.Equals(dir.Name, StringComparison.OrdinalIgnoreCase)))
                    .Select(dir => GenerateJsonNode(dir))
                    .Concat(
                        directory.GetFiles()
                            .Where(file => !Array.Exists(_excludedExtensions, x => x.Equals(file.Extension, StringComparison.OrdinalIgnoreCase)))
                            .Select(file => new { name = file.Name, type = "file" })
                    )
            };
            return node;
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
            {
                if (!Array.Exists(_excludedFolders, x => x.Equals(dir.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    ProcessDirectoryYaml(dir, sb, level + 1);
                }
            }

            foreach (var file in directory.GetFiles())
            {
                if (!Array.Exists(_excludedExtensions, x => x.Equals(file.Extension, StringComparison.OrdinalIgnoreCase)))
                {
                    sb.AppendLine($"{indent}  - {file.Name}");
                }
            }
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
            {
                _stringBuilder.AppendLine($"    {parentId}-->{currentId}[{directory.Name}]");
            }
            else
            {
                _stringBuilder.AppendLine($"    {currentId}[{directory.Name}]");
            }

            foreach (var dir in directory.GetDirectories())
            {
                if (!Array.Exists(_excludedFolders, x => x.Equals(dir.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    ProcessDirectoryMermaid(dir, currentId);
                }
            }

            foreach (var file in directory.GetFiles())
            {
                if (!Array.Exists(_excludedExtensions, x => x.Equals(file.Extension, StringComparison.OrdinalIgnoreCase)))
                {
                    var fileId = currentId + file.Name.Replace(" ", "_");
                    _stringBuilder.AppendLine($"    {currentId}-->{fileId}(({file.Name}))");
                }
            }
        }
    }
}