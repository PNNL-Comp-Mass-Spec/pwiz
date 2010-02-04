﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.ComponentModel;
//using System.Threading;

namespace ScanRanker
{
    public class Workspace
    {
        public static TextBoxForm statusForm;

       
        //public static EventWaitHandle wh = new AutoResetEvent(false);

        public static string OpenFileBrowseDialog(string sPrevFile)
        {
            try
            {
                OpenFileDialog dlgBrowseSource = new OpenFileDialog();

                if (!sPrevFile.Equals(string.Empty))
                {
                    dlgBrowseSource.InitialDirectory = sPrevFile;
                }
                else
                {
                    dlgBrowseSource.InitialDirectory = "c:\\";
                }

                DialogResult result = dlgBrowseSource.ShowDialog();

                if (result == DialogResult.OK)
                {
                    return dlgBrowseSource.FileName;
                }

                return string.Empty;

            }
            catch (Exception exc)
            {
                throw new Exception("Error opening file dialog\r\n", exc);
            }
        }

        public static string OpenDirBrowseDialog(string sPrevDir, Boolean newFolderOption)
        {
            try
            {
                FolderBrowserDialog dlgBrowseSource = new FolderBrowserDialog();

                if (!sPrevDir.Equals(string.Empty))
                {
                    dlgBrowseSource.SelectedPath = sPrevDir;
                }
                else
                {
                    dlgBrowseSource.SelectedPath = "c:\\";
                }

                dlgBrowseSource.ShowNewFolderButton = newFolderOption;

                DialogResult result = dlgBrowseSource.ShowDialog();

                if (result == DialogResult.OK)
                {
                    return dlgBrowseSource.SelectedPath;
                }

                return string.Empty;

            }
            catch (Exception exc)
            {
                throw new Exception("Error opening directory dialog\r\n", exc);
            }

        }

        /// <summary>
        /// TODO: exception handler
        /// </summary>
        /// <param name="exc"></param>
        //private void HandleExceptions(Exception exc)
        //{
        //    MessageBox.Show(exc.Message);

        //}

        /// <summary>
        /// send text to status form
        /// </summary>
        /// <param name="text"></param>
        delegate void SetTextCallback(string text);
        public static void SetText(string text)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (statusForm.tbStatus.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText);
                statusForm.Invoke(d, new object[] { text });
            }
            else
            {
                statusForm.tbStatus.AppendText(text);
            }
        }

        /// <summary>
        /// change button in status form to "close" or "stop"
        /// </summary>
        /// <param name="btn"></param>
        delegate void ChangeButtonCallback(string btn);
        public static void ChangeButtonTo(string btn)
        {
            if (statusForm.btnStop.InvokeRequired)
            {
                ChangeButtonCallback d = new ChangeButtonCallback(ChangeButtonTo);
                statusForm.Invoke(d, new object[] { btn });
            }
            else
            {
                //statusForm.btnClose.Visible = !(statusForm.btnClose.Visible);
                //statusForm.btnStop.Visible = !(statusForm.btnStop.Visible);
                if (btn.Equals("Stop"))
                {
                    statusForm.btnStop.Visible = true;
                    statusForm.btnClose.Visible = false;
                }
                else
                {
                    statusForm.btnStop.Visible = false;
                    statusForm.btnClose.Visible = true;
                }
            }

        }

        /// <summary>
        /// run a process and send standard output and standard error to status form
        /// </summary>
        /// <param name="pathAndExeFile"></param>
        /// <param name="args"></param>
        /// <param name="outputDir"></param>
        public static void RunProcess(string pathAndExeFile, string args, string outputDir)
        {
            Process RunProc = new Process();
            RunProc.EnableRaisingEvents = true;
            RunProc.StartInfo.CreateNoWindow = true;
            RunProc.StartInfo.WorkingDirectory = outputDir;
            RunProc.StartInfo.UseShellExecute = false;
            RunProc.StartInfo.RedirectStandardError = true;
            RunProc.StartInfo.RedirectStandardOutput = true;

            //RunProc.OutputDataReceived += new DataReceivedEventHandler( StdOutputHandler );
            //RunProc.ErrorDataReceived += new DataReceivedEventHandler(StdErrorHandler);

            RunProc.StartInfo.FileName = pathAndExeFile;
            RunProc.StartInfo.Arguments = args;

            RunProc.Start();

            //RunProc.BeginOutputReadLine();
            //RunProc.BeginErrorReadLine();

            //string stdOutput = RunProc.StandardOutput.ReadToEnd();
            //statusForm.tbStatus.AppendText(stdOutput);
            //RunProc.WaitForExit();

            StreamReader srOut = RunProc.StandardOutput;
            StreamReader srErr = RunProc.StandardError;
            //while (!RunProc.HasExited)

            string outLine = string.Empty;
            //string errLine = string.Empty;
            //while (((outLine = srOut.ReadLine()) != null) || ((errLine = srErr.ReadLine()) != null))
            //while (((outLine = srOut.ReadLine()) != null) && (!RunProc.HasExited))

            while ((outLine = srOut.ReadLine()) != null)
            {
                SetText(outLine+"\r\n");
                //lock(statusForm.tbStatus)
                //statusForm.tbStatus.AppendText(outLine + "\n");
                // RunProc.WaitForExit(500);
            }
            SetText("\r\nError message from " + pathAndExeFile + " :\r\n");
            SetText(srErr.ReadToEnd().ToString()+"\r\n");
            RunProc.WaitForExit();

        }


    }
}