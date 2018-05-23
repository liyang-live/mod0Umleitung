﻿/*
 * ------------------------------------------------------------------------------
 *
 * This file is part of: mod0Umleitung - DNS-masquerading server for Windows.
 *
 * ------------------------------------------------------------------------------
 *
 * BSD 3-Clause License
 *
 * Copyright (c) 2017, modzero GmbH
 * Author: Thorsten Schroeder
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *
 * * Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 *
 * * Redistributions in binary form must reproduce the above copyright notice,
 *   this list of conditions and the following disclaimer in the documentation
 *   and/or other materials provided with the distribution.
 *
 * * Neither the name of the copyright holder nor the names of its
 *   contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * * NON-MILITARY-USAGE CLAUSE
 *   Redistribution and use in source and binary form for military use and
 *   military research is not permitted. Infringement of these clauses may
 *   result in publishing the source code of the utilizing applications and
 *   libraries to the public. As this software is developed, tested and
 *   reviewed by *international* volunteers, this clause shall not be refused
 *   due to the matter of *national* security concerns.
 *  
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
 * FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
 * DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
 * OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 * ------------------------------------------------------------------------------
*/

using modzero.Logger;
using modzero.Umleitung.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace modzero.Umleitung
{
    public partial class UmleitungManagerForm : Form
    {
        private m0Logger m_log;
        private UmleitungServer m_server;
        private ToolStripStatusLabel m_status_label;
        private Boolean m_server_running;
        private UmleitungProperties m_props;

        public UmleitungManagerForm(Boolean autostart)
        {
            InitializeComponent();

            m_props = new UmleitungProperties();
            m_props.DebugLevel = Properties.Settings.Default.umlDebug;
            m_props.UseCustomDns = Properties.Settings.Default.useCustomUpstream;
            m_props.CustomDnsServers = new List<IPAddress>();

            m_log = new m0Logger(m0Logger.m0LogDestination.LOG_TEXTBOX, textLog);
            m_log.SetDebuglevel(Properties.Settings.Default.umlDebug);

            if (m_props.UseCustomDns)
            {
                try
                {
                    IPAddress ip = IPAddress.Parse(Properties.Settings.Default.upstreamDNS1);
                    m_props.CustomDnsServers.Add(ip);
                }
                catch (Exception e)
                {
                    if (!(Properties.Settings.Default.upstreamDNS1 == ""))
                        m_log.WriteLine(2, "[!] UmleitungManagerForm.ctor: failed to add upstreamDNS1: " + e.Message);
                    Properties.Settings.Default.upstreamDNS1 = "";
                    Properties.Settings.Default.Save();
                }

                try
                {
                    IPAddress ip = IPAddress.Parse(Properties.Settings.Default.upstreamDNS2);
                    m_props.CustomDnsServers.Add(ip);
                }
                catch (Exception e)
                {
                    if (!(Properties.Settings.Default.upstreamDNS2 == ""))
                        m_log.WriteLine(2, "[!] UmleitungManagerForm.ctor: failed to add upstreamDNS2: " + e.Message);
                    Properties.Settings.Default.upstreamDNS2 = "";
                    Properties.Settings.Default.Save();
                }
            }

            m_log.WriteLine(2, "[d] UmleitungManagerForm.ctor: Debug Level     = " + Properties.Settings.Default.umlDebug);
            m_log.WriteLine(2, "[d] UmleitungManagerForm.ctor: Use Custom DNS  = " + Properties.Settings.Default.useCustomUpstream);
            m_log.WriteLine(2, "[d] UmleitungManagerForm.ctor: Primary DNS     = " + Properties.Settings.Default.upstreamDNS1);
            m_log.WriteLine(2, "[d] UmleitungManagerForm.ctor: Alternate DNS   = " + Properties.Settings.Default.upstreamDNS2);

            m_server = new UmleitungServer(m_props);
            m_server.EnableLogging(m_log);

            m_server.ReadMasqConfig();
            m_server.DumpRunningConfig();

            create_main_menu();

            statusText.BackColor = Color.LightCoral;
            statusText.Text = "Stopped";

            m_status_label = toolStripStatusLabel1;

            set_status_modified(false);
            m_server_running = false;

            if(autostart)
                this.start_server();
        }

        private void addRule_Click(object sender, EventArgs e)
        {
            PromptRuleAdd pra = new PromptRuleAdd();

            DNSMasqHost newHost = pra.ShowDnsRuleDialog();

            if (newHost == null)
                return;

            m_log.WriteLine("[+] Added new rule for " + newHost.name + ": a=" + newHost.a + " aaaa=" + newHost.aaaa);

            m_server.AddMasq(newHost);
            set_status_modified(true);
        }

        private void startButton_Click(object sender, EventArgs e)
        {
            if (m_server_running)
                return;

            this.start_server();
        }

        private void stopButton_Click(object sender, EventArgs e)
        {
            if (!m_server_running)
                return;

            this.stop_server();
        }

        private void stop_server()
        {
            m_server.Stop();
            if (!m_server.IsRunning)
            {
                statusText.Text = "Stopped";
                statusText.BackColor = Color.LightCoral;
                m_server_running = false;
            }
        }

        private void start_server()
        {
            m_server.Run();
            if (m_server.IsRunning)
            {
                statusText.Text = "Running";
                statusText.BackColor = Color.LightGreen;
                m_server_running = true;
            }
        }

        private void loadRules_Click(object sender, EventArgs e)
        {
            this.stop_server();

            string path;
            OpenFileDialog file = new OpenFileDialog();
            file.InitialDirectory = m_server.config_dir;

            if (file.ShowDialog() == DialogResult.OK)
            {
                path = file.FileName;
                m_log.WriteLine("[+] loading config-file: " + path);
                m_server.ReadMasqConfig(path);
                m_server.DumpRunningConfig();
            }
            set_status_modified(false);
        }

        private void saveRules_Click(object sender, EventArgs e)
        {
            string path;
            SaveFileDialog file = new SaveFileDialog();
            file.InitialDirectory = m_server.config_dir;

            if (file.ShowDialog() == DialogResult.OK)
            {
                path = file.FileName;
                m_log.WriteLine("[+] saving config-file: " + path);
                m_server.SaveMasq(path);
            }
            set_status_modified(false);
        }

        public void create_main_menu()
        {
            MainMenu mainMenu1 = new MainMenu();

            MenuItem menuItem1 = new MenuItem();
            MenuItem menuItem2 = new MenuItem();

            menuItem1.Text = "File";
            menuItem2.Text = "Help";

            MenuItem subMenuItemFile1 = new MenuItem("&Exit", new System.EventHandler(this.OnExit_Click));
            
            MenuItem subMenuItemFile2 = new MenuItem("&Add Rule", new System.EventHandler(this.addRule_Click));
            subMenuItemFile2.Shortcut = Shortcut.CtrlN;
            MenuItem subMenuItemFile3 = new MenuItem("&Load Rules", new System.EventHandler(this.loadRules_Click));
            subMenuItemFile3.Shortcut = Shortcut.CtrlO;
            MenuItem subMenuItemFile4 = new MenuItem("&Save Rules", new System.EventHandler(this.saveRules_Click));
            subMenuItemFile4.Shortcut = Shortcut.CtrlS;

            MenuItem subMenuItemFile5 = new MenuItem("&Preferences", new System.EventHandler(this.OnPreferences_Click));

            MenuItem subMenuItemHelp1 = new MenuItem("&About", new System.EventHandler(this.OnAbout_Click));
            MenuItem subMenuItemHelp2 = new MenuItem("&Manual", new System.EventHandler(this.OnManual_Click));

            menuItem1.MenuItems.Add(subMenuItemFile2);
            menuItem1.MenuItems.Add(subMenuItemFile3);
            menuItem1.MenuItems.Add(subMenuItemFile4);
            menuItem1.MenuItems.Add(subMenuItemFile5);
            menuItem1.MenuItems.Add(subMenuItemFile1);

            menuItem2.MenuItems.Add(subMenuItemHelp1);
            menuItem2.MenuItems.Add(subMenuItemHelp2);

            // Add two MenuItem objects to the MainMenu.
            mainMenu1.MenuItems.Add(menuItem1);
            mainMenu1.MenuItems.Add(menuItem2);

            // Bind the MainMenu to Form1.
            Menu = mainMenu1;
        }

        private void OnPreferences_Click(object sender, EventArgs e)
        {
            PromptPreferences prefs = new PromptPreferences();

            m_log.WriteLine(2, "[d] Display preferences dialog");

            prefs.ShowPrefsDialog();

            m_props.DebugLevel = Properties.Settings.Default.umlDebug;
            m_props.UseCustomDns = Properties.Settings.Default.useCustomUpstream;

            if (m_props.UseCustomDns)
            {
                m_props.CustomDnsServers = new List<IPAddress>();

                try
                {
                    IPAddress ip = IPAddress.Parse(Properties.Settings.Default.upstreamDNS1);
                    m_props.CustomDnsServers.Add(ip);
                    m_log.WriteLine(2, "[d] OnPreferences_Click: added upstreamDNS1: " + Properties.Settings.Default.upstreamDNS1);
                }
                catch (Exception ex)
                {
                    if (!(Properties.Settings.Default.upstreamDNS1 == ""))
                        m_log.WriteLine(2, "[!] OnPreferences_Click: failed to add upstreamDNS1: " + ex.Message);
                    Properties.Settings.Default.upstreamDNS1 = "";
                    Properties.Settings.Default.Save();
                }

                try
                {
                    IPAddress ip = IPAddress.Parse(Properties.Settings.Default.upstreamDNS2);
                    m_props.CustomDnsServers.Add(ip);
                    m_log.WriteLine(2, "[d] OnPreferences_Click: added upstreamDNS2: " + Properties.Settings.Default.upstreamDNS2);
                }
                catch (Exception ex)
                {
                    if (!(Properties.Settings.Default.upstreamDNS2 == ""))
                        m_log.WriteLine(2, "[!] OnPreferences_Click: failed to add upstreamDNS2: " + ex.Message + ": " + Properties.Settings.Default.upstreamDNS2);
                    Properties.Settings.Default.upstreamDNS2 = "";
                    Properties.Settings.Default.Save();
                }
            }
            else
            {
                m_props.CustomDnsServers = new List<IPAddress>();
            }

            m_log.WriteLine(2, "[D] updated preferences:");
            m_log.WriteLine(2, "[-] DebugLevel:         " + m_props.DebugLevel);
            m_log.WriteLine(2, "[-] UseCustomDns:       " + m_props.UseCustomDns);
            m_log.WriteLine(2, "[-] CustomDnsServers 0: " + (m_props.CustomDnsServers.Count > 0 ? m_props.CustomDnsServers[0].ToString() : ""));
            m_log.WriteLine(2, "[-] CustomDnsServers 1: " + (m_props.CustomDnsServers.Count > 1 ? m_props.CustomDnsServers[1].ToString() : ""));

            m_log.SetDebuglevel(m_props.DebugLevel);
            m_server.SetPreferences(m_props);
        }


        private void OnExit_Click(Object sender, System.EventArgs e)
        {
            Application.Exit();
        }

        private void OnAbout_Click(Object sender, System.EventArgs e)
        {
            string about_text = "modzero Umleitung - DNS Masquerading for Windows\n\n"
                                + "   Version: " + Application.ProductVersion + "\n"
                                + "   Author: Thorsten Schroeder\n"
                                + "   Copyright 2018, modzero GmbH\n\n"
                                + "Contact: \n"
                                + "   modzero GmbH\n"
                                + "   Marienstr. 12\n"
                                + "   10117 Berlin\n"
                                + "   Germany\n"
                                + "   Web: https://www.modzero.ch/\n"
                                + "   Mail: contact@modzero.ch\n\n"
                                + "This program is free software. It comes without any warranty.\n"
                                + "Use at your own risk!";

            MessageBox.Show(about_text, "About mod0 Umleitung");

        }

        private void OnManual_Click(Object sender, System.EventArgs e)
        {
            System.Diagnostics.Process.Start("https://modzero.github.io/mod0Umleitung/");
        }

        private void set_status_modified(Boolean s)
        {
            if (this.m_status_label != null)
            {
                if (s)
                {
                    this.m_status_label.Text = "Ruleset Modified";
                    this.m_status_label.ForeColor = Color.LightCoral;
                    this.m_status_label.ToolTipText = "Rules have been modified. Changes are lost, if you quit.";
                }
                else
                {
                    this.m_status_label.Text = "Ruleset Loaded";
                    this.m_status_label.ForeColor = Color.Black;
                    this.m_status_label.ToolTipText = "Rules are loaded and not modified.";
                }
            }
        }
    }
}
