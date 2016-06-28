using System;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Threading;

namespace UChatClient
{
    /// <summary>
    /// Prompt.xaml 的交互逻辑
    /// </summary>
    public partial class Prompt : Window
    {
        private System.Timers.Timer timer = new System.Timers.Timer(2000);

        public Prompt()
        {
            InitializeComponent();
        }

        public Prompt(string info, int cnt)
        {
            InitializeComponent();
            text.Content = info;

            timer.Elapsed += new ElapsedEventHandler(timer_Elapsed);
            timer.Start();

            this.Left = SystemParameters.WorkArea.Width - this.Width;
            this.Top = SystemParameters.WorkArea.Height - this.Height - this.Height * cnt;
        }

        void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            timer.Stop();
            Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate ()
            {
                this.Close();
            });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}