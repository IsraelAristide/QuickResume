using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using PInvoke;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace QuickResumePc
{

    [Flags]
    public enum ThreadAccess : int
    {
        TERMINATE = (0x001),
        SUSPEND_RESUME = (0x0002),
        GET_CONTEXT = (0x0008),
        SET_CONTEXT = (0x0010),
        SET_INFORMATION = (0x0020),
        QUERY_INFORMATION = (0x0040),
        SET_THREAD_TOKEN = (0x0080),
        IMPERSONATE = (0x0100),
        DIRECT_IMPERSONATION = (0x0200)
    }

    public enum ProcessState
    {
        SUSPENDED,
        RUNNING
    }

    public sealed partial class MainWindow : Window
    {
    
        [DllImport("kernel32.dll")]
        static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
        [DllImport("kernel32.dll")]
        static extern uint SuspendThread(IntPtr hTread);
        [DllImport("kernel32.dll")]
        static extern int ResumeThread(IntPtr hThread);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);

        static Dictionary<int, ProcessState> suspendableProcs = new Dictionary<int, ProcessState>();
        static Dictionary<int, IntPtr> hiddenWindows = new Dictionary<int, IntPtr>();
        int selectedProgram;
        

        public MainWindow()
        {
            
            this.InitializeComponent();
            Fetch_Running_Programs();
            
        }

        private void Update_Proclist()
        {

            ProcList.ItemsSource = null;
            
            // ProcList.Items.Clear();


            suspendableProcs.ToList().ForEach(procD =>
            {
                ComboBoxItem boxItem = new ComboBoxItem();
                Process proc = Process.GetProcessById(procD.Key);

         
                boxItem.Tag = procD.Key;
                boxItem.Name = proc.ProcessName;
                boxItem.Content = proc.ProcessName;

                ProcList.Items.Add(boxItem);


            });


        }

        private void Fetch_Running_Programs()
        {

            Dictionary<int, ProcessState> procList = new Dictionary<int, ProcessState>();

            Process.GetProcesses().ToList().ForEach(proc =>
            {

                if ((proc.PrivateMemorySize64 / 1000) / 1000 > 100)
                {
                    //Debug.Write(proc.ProcessName);
                    //Debug.Write(" ");
                    //Debug.Write((proc.PrivateMemorySize64 / 1000)/1000);
                    //Debug.Write(" mb");
                    //Debug.Write("\n");

                    procList[proc.Id] = ProcessState.RUNNING;

                    if (proc.Threads[0].ThreadState == ThreadState.Wait)
                    {
                        if (proc.Threads[0].WaitReason == ThreadWaitReason.Suspended)
                        {
                            procList[proc.Id] = ProcessState.SUSPENDED;
                        }
                    }

                }
            });

            suspendableProcs.Clear();
            suspendableProcs = procList;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Fetch_Running_Programs();
            Update_Proclist();
 
        }

        private void ProcList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem selectedItem = (ComboBoxItem)e.AddedItems[0];
            
            if (suspendableProcs[(int)selectedItem.Tag] == ProcessState.SUSPENDED)
            {
                Resume.Visibility = Visibility.Visible;
            } else
            {
                Suspend.Visibility = Visibility.Visible;
                Resume.Visibility = Visibility.Collapsed;
            }

            selectedProgram = (int)selectedItem.Tag;

        }

        private void Suspend_Click(object sender, RoutedEventArgs e)
        {
            SuspendProcess(selectedProgram);
            Resume.Visibility = Visibility.Visible;
        }

        private void Resume_Click(object sender, RoutedEventArgs e)
        {
            ResumeProcess(selectedProgram);
            Resume.Visibility = Visibility.Collapsed;
        }

        private void StackPanel_Loaded(object sender, RoutedEventArgs e)
        {
            Update_Proclist();
        }

        private static void SuspendProcess(int pid)
        {

            var process = Process.GetProcessById(pid); // throws exception if process does not exist
            hiddenWindows[pid] = process.MainWindowHandle;
            suspendableProcs[pid] = ProcessState.SUSPENDED;
            User32.ShowWindow(process.MainWindowHandle, User32.WindowShowStyle.SW_HIDE);
            
            foreach (ProcessThread pT in process.Threads)
            {
                IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

                if (pOpenThread == IntPtr.Zero)
                {
                    continue;
                }

                SuspendThread(pOpenThread);

                CloseHandle(pOpenThread);
            }
            
        }

        public static void ResumeProcess(int pid)
        {
            var process = Process.GetProcessById(pid);

            if (process.ProcessName == string.Empty)
                return;

            foreach (ProcessThread pT in process.Threads)
            {
                IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

                if (pOpenThread == IntPtr.Zero)
                {
                    continue;
                }

                var suspendCount = 0;
                do
                {
                    suspendCount = ResumeThread(pOpenThread);
                } while (suspendCount > 0);

                CloseHandle(pOpenThread);
            }

            User32.ShowWindow(hiddenWindows[pid], User32.WindowShowStyle.SW_SHOW);
        }

    }

}
