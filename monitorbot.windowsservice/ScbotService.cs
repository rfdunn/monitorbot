﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using monitorbot.core.utils;
using monitorbot.logging;

namespace monitorbot.windowsservice
{
    public class ScbotService
    {
        private LoggingDashboard m_Dash;
        private Task m_Bot;
        private CancellationTokenSource m_StopTheBot;
        private EventLog m_EventLog;

        public void Start()
        {
            m_EventLog = new EventLog("Application") {Source = "Application"};
            m_EventLog.WriteEntry("scbot starting up ...");

            try
            {
                var configuration = Configuration.Load();
                m_Dash = new LoggingDashboard(configuration.Get("logging-dashboard-endpoint"));
                m_StopTheBot = new CancellationTokenSource();
                m_Bot = monitorbot.Program.MainAsync(configuration, m_StopTheBot.Token);
            }
            catch (Exception ex)
            {
                m_EventLog.WriteEntry("Error starting scbot: "+ex);
                throw;
            }
        }

        public void Stop()
        {
            try
            {
                if (m_StopTheBot != null)
                {
                    m_StopTheBot.Cancel();
                    m_StopTheBot = null;
                }
                if (m_Bot != null)
                {
                    m_Bot.Wait();
                    m_Bot = null;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                m_EventLog.WriteEntry("Error stopping scbot: " + ex);
                // throw; // TODO: figure out why TaskCanceledExceptions seem to be hitting here
                // (they should get caught as an OperationCanceledException)
            }
            finally
            {
                m_Dash.Dispose();
            }
        }
    }
}
