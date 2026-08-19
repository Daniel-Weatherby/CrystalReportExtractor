// ============================================================================
// File: MainForm.cs
// Purpose:
//   Provides the local Windows interface for selecting approved input/output
//   folders and monitoring a batch Crystal metadata extraction run.
//
// Important considerations:
//   This interface never uploads reports or sends them across a network. All
//   source reports and JSON outputs remain in user-selected local locations.
// ============================================================================

using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CrystalReportExtractor.Desktop
{
    internal sealed class MainForm : Form
    {
        private readonly TextBox _inputTextBox;
        private readonly TextBox _outputTextBox;
        private readonly Button _inputBrowseButton;
        private readonly Button _outputBrowseButton;
        private readonly CheckBox _subdirectoriesCheckBox;
        private readonly CheckBox _overwriteCheckBox;
        private readonly Button _startButton;
        private readonly Button _cancelButton;
        private readonly ProgressBar _progressBar;
        private readonly Label _statusLabel;
        private readonly RichTextBox _logTextBox;
        private CancellationTokenSource _cancellation;

        public MainForm()
        {
            Text = "Crystal Report Extractor";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 560);
            Size = new Size(920, 680);
            Font = new Font("Segoe UI", 9F);

            var titleLabel = new Label
            {
                Text = "Crystal Report Metadata Batch Extractor",
                AutoSize = true,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                Margin = new Padding(3, 3, 3, 12)
            };

            var informationLabel = new Label
            {
                Text = "Select an input folder containing .rpt files and a separate output folder for JSON metadata.",
                AutoSize = true,
                MaximumSize = new Size(820, 0)
            };

            _inputTextBox = new TextBox { Dock = DockStyle.Fill };
            _outputTextBox = new TextBox { Dock = DockStyle.Fill };
            _inputBrowseButton = new Button { Text = "Browse…", AutoSize = true };
            _outputBrowseButton = new Button { Text = "Browse…", AutoSize = true };
            _subdirectoriesCheckBox = new CheckBox
            {
                Text = "Include subfolders",
                Checked = true,
                AutoSize = true
            };
            _overwriteCheckBox = new CheckBox
            {
                Text = "Overwrite existing JSON files",
                Checked = false,
                AutoSize = true
            };
            _startButton = new Button
            {
                Text = "Start extraction",
                AutoSize = true,
                Padding = new Padding(10, 4, 10, 4)
            };
            _cancelButton = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                Enabled = false,
                Padding = new Padding(10, 4, 10, 4)
            };
            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 1
            };
            _statusLabel = new Label
            {
                Text = "Ready",
                AutoSize = true
            };
            _logTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                DetectUrls = false,
                BackColor = Color.White,
                Font = new Font("Consolas", 9F)
            };

            Controls.Add(BuildLayout(titleLabel, informationLabel));

            _inputBrowseButton.Click += (sender, args) =>
                BrowseForFolder(_inputTextBox, "Select the report input folder");
            _outputBrowseButton.Click += (sender, args) =>
                BrowseForFolder(_outputTextBox, "Select the JSON output folder");
            _startButton.Click += StartButton_Click;
            _cancelButton.Click += (sender, args) => _cancellation?.Cancel();
        }

        private Control BuildLayout(Label titleLabel, Label informationLabel)
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18),
                ColumnCount = 1,
                RowCount = 9
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            root.Controls.Add(titleLabel, 0, 0);
            root.Controls.Add(informationLabel, 0, 1);
            root.Controls.Add(
                BuildFolderRow("Input folder", _inputTextBox, _inputBrowseButton),
                0,
                2);
            root.Controls.Add(
                BuildFolderRow("Output folder", _outputTextBox, _outputBrowseButton),
                0,
                3);

            var optionsPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0, 8, 0, 8)
            };
            optionsPanel.Controls.Add(_subdirectoriesCheckBox);
            optionsPanel.Controls.Add(_overwriteCheckBox);
            root.Controls.Add(optionsPanel, 0, 4);

            var buttonPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0, 0, 0, 8)
            };
            buttonPanel.Controls.Add(_startButton);
            buttonPanel.Controls.Add(_cancelButton);
            root.Controls.Add(buttonPanel, 0, 5);
            root.Controls.Add(_progressBar, 0, 6);
            root.Controls.Add(_statusLabel, 0, 7);
            root.Controls.Add(_logTextBox, 0, 8);

            return root;
        }

        private static Control BuildFolderRow(
            string labelText,
            TextBox textBox,
            Button browseButton)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 3,
                Margin = new Padding(0, 12, 0, 0)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel.Controls.Add(
                new Label
                {
                    Text = labelText,
                    AutoSize = true,
                    Anchor = AnchorStyles.Left
                },
                0,
                0);
            panel.Controls.Add(textBox, 1, 0);
            panel.Controls.Add(browseButton, 2, 0);
            return panel;
        }

        private static void BrowseForFolder(TextBox target, string description)
        {
            using (var dialog = new FolderBrowserDialog
            {
                Description = description,
                ShowNewFolderButton = true
            })
            {
                if (Directory.Exists(target.Text))
                {
                    dialog.SelectedPath = target.Text;
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    target.Text = dialog.SelectedPath;
                }
            }
        }

        private async void StartButton_Click(object sender, EventArgs e)
        {
            var options = new BatchOptions
            {
                InputDirectory = _inputTextBox.Text.Trim(),
                OutputDirectory = _outputTextBox.Text.Trim(),
                IncludeSubdirectories = _subdirectoriesCheckBox.Checked,
                OverwriteExisting = _overwriteCheckBox.Checked
            };

            SetRunningState(true);
            _logTextBox.Clear();
            _progressBar.Value = 0;
            _statusLabel.Text = "Preparing extraction…";
            _cancellation = new CancellationTokenSource();

            try
            {
                var progress = new Progress<BatchProgress>(UpdateProgress);
                var processor = new BatchProcessor();

                BatchRunSummary summary = await Task.Run(
                    () => processor.Run(
                        options,
                        progress,
                        _cancellation.Token));

                _statusLabel.Text = summary.Cancelled
                    ? "Extraction cancelled"
                    : "Extraction complete";

                AppendLog(string.Format(
                    "Finished: {0} succeeded, {1} failed, {2} skipped.",
                    summary.Succeeded,
                    summary.Failed,
                    summary.Skipped));

                MessageBox.Show(
                    this,
                    string.Format(
                        "Reports found: {0}\nSucceeded: {1}\nFailed: {2}\nSkipped: {3}",
                        summary.ReportsFound,
                        summary.Succeeded,
                        summary.Failed,
                        summary.Skipped),
                    "Extraction complete",
                    MessageBoxButtons.OK,
                    summary.Failed == 0
                        ? MessageBoxIcon.Information
                        : MessageBoxIcon.Warning);
            }
            catch (Exception exception)
            {
                _statusLabel.Text = "Extraction could not start";
                AppendLog(exception.Message);
                MessageBox.Show(
                    this,
                    exception.Message,
                    "Crystal Report Extractor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _cancellation?.Dispose();
                _cancellation = null;
                SetRunningState(false);
            }
        }

        private void UpdateProgress(BatchProgress progress)
        {
            _progressBar.Maximum = Math.Max(1, progress.Total);
            _progressBar.Value = Math.Min(progress.Completed, _progressBar.Maximum);
            _statusLabel.Text = string.Format(
                "{0} of {1}: {2}",
                progress.Completed,
                progress.Total,
                progress.Status);
            AppendLog(string.Format(
                "[{0}] {1}",
                progress.Status,
                progress.RelativeSourcePath));
        }

        private void AppendLog(string message)
        {
            _logTextBox.AppendText(message + Environment.NewLine);
            _logTextBox.SelectionStart = _logTextBox.TextLength;
            _logTextBox.ScrollToCaret();
        }

        private void SetRunningState(bool running)
        {
            _inputTextBox.Enabled = !running;
            _outputTextBox.Enabled = !running;
            _inputBrowseButton.Enabled = !running;
            _outputBrowseButton.Enabled = !running;
            _subdirectoriesCheckBox.Enabled = !running;
            _overwriteCheckBox.Enabled = !running;
            _startButton.Enabled = !running;
            _cancelButton.Enabled = running;
        }
    }
}
