﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.IO;

namespace OptimizedClevoFan
{
    public partial class AppWindow : Form
    {
        // The path to the key where Windows looks for startup applications
        RegistryKey rkApp = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
        const string APP_NAME = "OptimizedClevoFan";

        private Timer timer;

        private FanController fanController;

        private List<FanInfo> fanInfos;

        private bool firstRun = true;

        private FanController LoadConfiguration()
        {
            string confJson = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "configuration.json");
            FanController fanController = JsonConvert.DeserializeObject<FanController>(confJson);

            foreach( Fan fan in fanController.fans)
            {
                fan.SetFanControl(fanController.GetFanControl());
                fan.SetNumberOfValuesForAvgTemperature(fanController.numberOfValuesForAvgTemperature);
            }

            return fanController;
        }

        private void SaveConfiguration(FanController fanController)
        {
            JsonSerializer serializer = new JsonSerializer();
            using (StreamWriter sw = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "configuration.json"))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, fanController);
            }
        }

        public AppWindow()
        {
            InitializeComponent();

            this.Icon = Properties.Resources.Default;
            this.SystemTrayIcon.Icon = Properties.Resources.Default;

            this.Text = "Optimized Clevo Fan";
            this.SystemTrayIcon.Text = this.Text;
            this.SystemTrayIcon.Visible = true;

            // Modify the right-click menu of your system tray icon here
            ContextMenu menu = new ContextMenu();
            menu.MenuItems.Add("Exit", ContextMenuExit);
            this.SystemTrayIcon.ContextMenu = menu;

            this.FormClosing += WindowClosing;

            ///////////////////////////////////////////////////////////////////////////////////////////////
            
            // Check to see the current state (running at startup or not)
            if (rkApp.GetValue(APP_NAME) == null)
                checkBoxStartWithWindows.Checked = false;
            else
            {
                checkBoxStartWithWindows.Checked = true;

                // sleep during 20 seconds so it lets Clevo Control Center start...
                // Kinda horrible solution, but it works in my case
                //System.Threading.Thread.Sleep(20000);
            }

            ///////////////////////////////////////////////////////////////////////////////////////////////
            // Create & init fan controller
            try
            {
                this.fanController = this.LoadConfiguration();
            }
            catch (Exception ex)
            {
                this.fanController = new FanController();
                this.fanController.LoadDefaults();
            }

            // Create timer for updating fan RPMs and so on
            timer = new Timer { Interval = fanController.updateFanStep };
            timer.Tick += new EventHandler(CheckFansTick);
            timer.Start();

            ///////////////////////////////////////////////////////////////////////////////////////////////
            // Add infos
            this.step.Text = this.fanController.updateFanStep.ToString() + " ms";

            fanInfos = new List<FanInfo>();
            foreach (Fan fan in this.fanController.fans)
            {
                FanInfo fanInfo = new FanInfo(fan);
                this.panelFanInfos.Controls.Add(fanInfo);
                this.fanInfos.Add(fanInfo);
            }

            // Start minimized
            this.WindowState = FormWindowState.Minimized;
        }

        public void CheckFansTick(object sender, EventArgs e)
        {
            if (sender == timer)
            {
                // Ugly trick to start minimized in task tray
                if (firstRun)
                {
                    this.Hide();
                    firstRun = false;
                }

                this.fanController.DoUpdate(this.offsetTrackBar.Value);

                foreach( FanInfo fanInfo in this.fanInfos)
                    fanInfo.UpdateInfos();
            }
        }

        private void SystemTrayIconDoubleClick(object sender, MouseEventArgs e)
        {
            if (this.Visible)
                this.Hide();
            else
            {
                this.WindowState = FormWindowState.Minimized;
                this.Show();
                //this.CenterToScreen();
                this.WindowState = FormWindowState.Normal;
            }
        }

        private void offsetTrackBar_Scroll(object sender, EventArgs e)
        {
            this.offsetValue.Text = this.offsetTrackBar.Value.ToString() + " %";
        }

        private void checkBoxStartWithWindows_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxStartWithWindows.Checked)
                rkApp.SetValue(APP_NAME, Application.ExecutablePath);
            else
                rkApp.DeleteValue(APP_NAME, false);
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            this.SaveConfiguration(this.fanController);
        }

        private void ExitApplication()
        {
            timer.Stop();

            this.fanController.Finish();

            this.SystemTrayIcon.Visible = false;
            Application.Exit();
            Environment.Exit(0);
        }

        private void buttonExit_Click(object sender, EventArgs e)
        {
            ExitApplication();
        }

        private void ContextMenuExit(object sender, EventArgs e)
        {
            ExitApplication();
        }

        private void WindowClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            this.Hide();
        }
    }
}
