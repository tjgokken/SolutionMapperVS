using System;
using System.ComponentModel.Design;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace SolutionMapper
{
    /// <summary>
    ///     Command handler
    /// </summary>
    internal sealed class SolutionMapperCommand
    {
        /// <summary>
        ///     Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        ///     Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("2583744c-a058-4a98-83a3-27427d0f4cbc");

        /// <summary>
        ///     VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SolutionMapperCommand" /> class.
        ///     Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private SolutionMapperCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand((s, e) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                ExecuteAsync().FireAndForget();
            }, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        ///     Gets the instance of the command.
        /// </summary>
        public static SolutionMapperCommand Instance { get; private set; }

        /// <summary>
        ///     Gets the service provider from the owner package.
        /// </summary>
        private IAsyncServiceProvider ServiceProvider => package;

        /// <summary>
        ///     Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in SolutionStructureCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new SolutionMapperCommand(package, commandService);
        }

        /// <summary>
        ///     This function is the callback used to execute the command when the menu item is clicked.
        ///     See the constructor to see how the menu item is associated with this function using
        ///     OleMenuCommandService service and MenuCommand class.
        /// </summary>
        private async Task ExecuteAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = await ServiceProvider.GetServiceAsync(typeof(DTE)) as DTE ?? throw new InvalidOperationException("Could not get DTE service");
            if (dte?.Solution == null || string.IsNullOrEmpty(dte.Solution.FullName))
            {
                ShowError("No solution is currently open.");
                return;
            }

            var solutionDir = Path.GetDirectoryName(dte.Solution.FullName);
            var solutionName = Path.GetFileNameWithoutExtension(dte.Solution.FullName);

            var format = PromptForFormat(out var cancelled, out var includeCodeDetails);
            if (cancelled)
                return; // User cancelled

            var structure = new SolutionMapGenerator(includeCodeDetails).GenerateStructure(solutionDir, format);

            using (var saveFileDialog = new SaveFileDialog())
            {
                var extension = GetFileExtension(format);
                saveFileDialog.FileName = $"{solutionName}-solution-structure{extension}";
                saveFileDialog.Filter = GetFileFilter(format);

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    try
                    {
                        File.WriteAllText(saveFileDialog.FileName, structure);
                        ShowMessage($"Solution structure has been exported to: {saveFileDialog.FileName}");
                    }
                    catch (Exception ex)
                    {
                        ShowError($"Error saving file: {ex.Message}");
                    }
            }
        }

        private SolutionMapGenerator.OutputFormat PromptForFormat(out bool cancelled, out bool includeCodeDetails)
        {
            var form = new Form
            {
                Width = 400,
                Height = 240,
                Text = "Select Output Format",
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                Font = new System.Drawing.Font("Segoe UI", 10)
            };

            var label = new Label
            {
                Text = "Select output format:",
                Location = new Point(12, 15),
                AutoSize = true
            };

            var combo = new ComboBox
            {
                Location = new Point(12, 50),
                Width = 360,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            combo.Items.AddRange(Enum.GetNames(typeof(SolutionMapGenerator.OutputFormat)));
            combo.SelectedIndex = 0;

            var detailsCheckbox = new CheckBox
            {
                Text = "Include code classes/methods",
                Location = new Point(12, 90),
                Width = 360,
                AutoSize = true
            };

            var button = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Width = 88,
                Height = 37,
                Location = new Point(180, 130)
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 88,
                Height = 37,
                Location = new Point(284, 130)
            };

            form.Controls.AddRange(new Control[] { combo, button, cancelButton, label, detailsCheckbox });
            form.AcceptButton = button;
            form.CancelButton = cancelButton;
            form.FormClosing += (s, e) => {
                if (form.DialogResult == DialogResult.None)
                    form.DialogResult = DialogResult.Cancel;
            };

            var result = form.ShowDialog();
            cancelled = result == DialogResult.Cancel;
            includeCodeDetails = detailsCheckbox.Checked;

            return result == DialogResult.OK
                ? (SolutionMapGenerator.OutputFormat)Enum.Parse(typeof(SolutionMapGenerator.OutputFormat), combo.SelectedItem.ToString())
                : SolutionMapGenerator.OutputFormat.Text;
        }

        private string GetFileExtension(SolutionMapGenerator.OutputFormat format)
        {
            switch (format)
            {
                case SolutionMapGenerator.OutputFormat.Markdown:
                    return ".md";
                case SolutionMapGenerator.OutputFormat.Html:
                    return ".html";
                case SolutionMapGenerator.OutputFormat.Json:
                    return ".json";
                case SolutionMapGenerator.OutputFormat.Yaml:
                    return ".yaml";
                case SolutionMapGenerator.OutputFormat.Mermaid:
                    return ".mmd";
                default:
                    return ".txt";
            }
        }

        private string GetFileFilter(SolutionMapGenerator.OutputFormat format)
        {
            switch (format)
            {
                case SolutionMapGenerator.OutputFormat.Markdown:
                    return "Markdown files (*.md)|*.md|All files (*.*)|*.*";
                case SolutionMapGenerator.OutputFormat.Html:
                    return "HTML files (*.html)|*.html|All files (*.*)|*.*";
                case SolutionMapGenerator.OutputFormat.Json:
                    return "JSON files (*.json)|*.json|All files (*.*)|*.*";
                case SolutionMapGenerator.OutputFormat.Yaml:
                    return "YAML files (*.yaml)|*.yaml|All files (*.*)|*.*";
                case SolutionMapGenerator.OutputFormat.Mermaid:
                    return "Mermaid files (*.mmd)|*.mmd|All files (*.*)|*.*";
                default:
                    return "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            }
        }

        private void ShowMessage(string message)
        {
            VsShellUtilities.ShowMessageBox(
                package,
                message,
                "Solution Structure Exporter",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        private void ShowError(string message)
        {
            VsShellUtilities.ShowMessageBox(
                package,
                message,
                "Solution Structure Exporter",
                OLEMSGICON.OLEMSGICON_CRITICAL,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}