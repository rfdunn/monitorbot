﻿using scbot.core.utils;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

namespace scbot.zendesk.services
{
    public class ReconnectingZendeskApi : IZendeskApi
    {
        private readonly Func<Task<IZendeskApi>> m_ZdApiFactory;
        private readonly ITime m_Time;
        private IZendeskApi m_ZdApi;
        private DateTime m_Backoff = new DateTime(2000, 1, 1);

        private ReconnectingZendeskApi(IZendeskApi zdApi, Func<Task<IZendeskApi>> zdApiFactory, ITime time)
        {
            m_ZdApiFactory = zdApiFactory;
            m_ZdApi = zdApi;
            m_Time = time;
        }

        public static async Task<ReconnectingZendeskApi> CreateAsync(Func<Task<IZendeskApi>> zdApiFactory, ITime time)
        {
            var api = await zdApiFactory();
            return new ReconnectingZendeskApi(api, zdApiFactory, time);
        }

        public async Task<dynamic> Ticket(string id)
        {
            return await DoWithRetry(async () => await m_ZdApi.Ticket(id));
        }
        public async Task<dynamic> Comments(string id)
        {
            return await DoWithRetry(async () => await m_ZdApi.Comments(id));
        }
        public async Task<dynamic> User(string id)
        {
            return await DoWithRetry(async () => await m_ZdApi.User(id));
        }

        private async Task<T> DoWithRetry<T>(Func<Task<T>> method)
        {
            if (m_Backoff > m_Time.Now) { return default(T); }

            try
            {
                return await method();
            }
            catch (WebException we)
            {
                var httpResponse = we.Response as HttpWebResponse;
                if (httpResponse == null || httpResponse.StatusCode != HttpStatusCode.Unauthorized)
                {
                    SetBackoff(2.Minutes());
                    throw;
                }
            }

            // You can trigger this code by going to the Zendesk profile page, 
            // selecting the Devices and apps tab and deleting other sessions
            Trace.WriteLine("Caught 401 from zendesk .. attempting to reauth");
            SetBackoff(20.Seconds());
            // reconnect and retry once
            m_ZdApi = await m_ZdApiFactory();
            return await method();
        }

        private void SetBackoff(TimeSpan timeToDelay)
        {
            m_Backoff = m_Time.Now + timeToDelay;
        }
    }
}
