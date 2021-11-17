using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


namespace QuickResume1
{

    [Flags]
    public enum ThreadAccess : int
    {
        TERMINATE = (0x0001),
        SUSPEND_RESUME = (0x0002),
        GET_CONTEXT = (0x0008),
        SET_CONTEXT = (0x0010),
        SET_INFORMATION = (0x0020),
        QUERY_INFORMATION = 0x0040,
        SET_THREAD_TOKEN = (0x0080),
        IMPERSONATE = (0x0100),
        DIRECT_IMPERSONATION = (0x0200)

    }

    [Flags]
    public enum WindowAccess : int
    {
        SW_MAXIMIZE = 3,
        SW_MINIMIZE = 6
    }

    public enum ProcessState
    {
        SUSPENDED,
        ACTIVE
    }


    public partial class MainWindow : Window
    {



        [DllImport("kernel32.dll")]
        static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
        [DllImport("kernel32.dll")]
        static extern uint SuspendThread(IntPtr hThread);
        [DllImport("kernel32.dll")]
        static extern int ResumeThread(IntPtr hThread);
        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);
        [DllImport("user32.dll", EntryPoint = "FindWindow")]
        public static extern IntPtr FindWindowByCaption(IntPtr ZeroOnly, string lpWindowName);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ShowWindow(IntPtr hWnd, WindowAccess windowAccesscmd);

        public List<KeyValuePair<string, float>> runningPrograms = new List<KeyValuePair<string, float>>();
        public Dictionary<string, ProcessState> processStates = new Dictionary<string, ProcessState>();
        Process currentlySelected;

        public MainWindow()
        {
            InitializeComponent();
       
          
            
        }

        private void updateCombox()
        {

            Combox.Items.Clear();

            runningPrograms.ForEach(prog =>
            {
                ComboBoxItem boxItem = new ComboBoxItem();
                Process process = Process.GetProcessesByName(prog.Key)[0];

                boxItem.Tag = process.Id;
                boxItem.Name = process.ProcessName;
                boxItem.Content = process.ProcessName;

                if (process.Threads[0].ThreadState == System.Diagnostics.ThreadState.Wait)
                {
                    if (process.Threads[0].WaitReason == ThreadWaitReason.Suspended)
                    {
                        processStates.Add(process.ProcessName, ProcessState.SUSPENDED);
                    }
                    
                }

                try
                {
                    if (processStates.ContainsKey(process.ProcessName))
                    {

                    }
                    else
                    {
                        Combox.Items.Add(boxItem);
                    }
                } catch
                {

                }



              

            });


            processStates.ToList().ForEach(v =>
            {
                ComboBoxItem boxItem = new ComboBoxItem();
                Process process = Process.GetProcessesByName(v.Key)[0];

                boxItem.Tag = process.Id;
                boxItem.Name = process.ProcessName;
                boxItem.Content = process.ProcessName;

                Combox.Items.Add(boxItem);
            });

        }

        private static List<KeyValuePair<string, float>> GetProc()
        {
            var counterList = new List<PerformanceCounter>();

            while (true)
            {
                var procDict = new Dictionary<string, float>();

                Process.GetProcesses().ToList().ForEach(p =>
                {
                    using (p)
                        if (counterList
                            .FirstOrDefault(c => c.InstanceName == p.ProcessName) == null)
                            counterList.Add(
                                new PerformanceCounter("Process", "% Processor Time",
                                    p.ProcessName, true));
                });

                counterList.ForEach(c =>
                {
                    try
                    {
                        // http://social.technet.microsoft.com/wiki/contents/
                        // articles/12984.understanding-processor-processor-
                        // time-and-process-processor-time.aspx

                        // This value is calculated over the base line of 
                        // (No of Logical CPUS * 100), So this is going to be a 
                        // calculated over a baseline of more than 100. 
                        var percent = c.NextValue() / Environment.ProcessorCount;
                        if (percent < 3)
                            return;

                        // Uncomment if you want to filter the "Idle" process
                        if (c.InstanceName.Trim().ToLower() == "idle")
                            return;

                        procDict[c.InstanceName] = percent;
                    }
                    catch (InvalidOperationException) { /* some will fail */ }
                });


                if (procDict.OrderByDescending(d => d.Value).ToList().Count == 0)
                {
                    continue;
                }
                else
                {
                    return procDict.OrderByDescending(d => d.Value).ToList();
                }

            }

        }

        private static void SuspendProcess(int pid)
        {
            var process = Process.GetProcessById(pid); // throws exception if process does not exist
            ShowWindow(process.MainWindowHandle, WindowAccess.SW_MINIMIZE);

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
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        private void HelloWorld_Clicked (object sender, RoutedEventArgs e)
        {
            Console.WriteLine(runningPrograms[0].Key);
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {

            Task.Run(update);

           

            updateCombox();

     
            
        }

        private async Task update()
        {
            try
            {
                runningPrograms = GetProc();


            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
            } finally
            {
                MessageBox.Show("processes refreshed");
            }
        }


        private void Combox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string itemSelected = "";
            try
            {
                itemSelected = e.AddedItems[0].ToString().Split(':')[1].Split(' ')[1];
            } catch
            {
              if (e.AddedItems.Count == 0)
                {
                    return;
                }
            }
           

            if (processStates.ContainsKey(itemSelected))
            {
                if (processStates[itemSelected] == ProcessState.ACTIVE)
                {
                    SuspendButton.Visibility = Visibility.Visible;
                } else
                {
                    ResumeButton.Visibility = Visibility.Visible;
                }

            } else
            {
                SuspendButton.Visibility = Visibility.Visible;
            }
            Console.WriteLine(itemSelected);
            currentlySelected = Process.GetProcessesByName(itemSelected)[0];

        }

        private void SuspendButton_Click(object sender, RoutedEventArgs e)
        {
            SuspendProcess(currentlySelected.Id);
            ResumeButton.Visibility = Visibility.Visible;
        }

        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {

            ResumeProcess(currentlySelected.Id);
            ResumeButton.Visibility = Visibility.Hidden;
        }
    }
}
