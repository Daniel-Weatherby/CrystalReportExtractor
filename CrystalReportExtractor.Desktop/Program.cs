// ============================================================================
// File: Program.cs
// Purpose: Starts the Windows desktop batch-extraction interface.
// ============================================================================

using System;
using System.Windows.Forms;

namespace CrystalReportExtractor.Desktop
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
