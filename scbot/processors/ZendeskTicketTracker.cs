﻿using System.Linq;
using System.Text.RegularExpressions;
using scbot.services;

namespace scbot.processors
{
    public class ZendeskTicketTracker : IMessageProcessor
    {
        private struct TrackedTicket
        {
            public readonly ZendeskTicket Ticket;
            public readonly string Channel;

            public TrackedTicket(ZendeskTicket ticket, string channel)
            {
                Ticket = ticket;
                Channel = channel;
            }
        }

        private readonly ICommandParser m_CommandParser;
        private readonly IListPersistenceApi<TrackedTicket> m_Persistence;
        private readonly IZendeskApi m_ZendeskApi;
        private static readonly Regex s_ZendeskIdRegex = new Regex(@"^ZD#(?<id>\d{5})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public ZendeskTicketTracker(ICommandParser commandParser, IKeyValueStore persistence, IZendeskApi zendeskApi)
        {
            m_CommandParser = commandParser;
            m_Persistence = new ListPersistenceApi<TrackedTicket>(persistence);
            m_ZendeskApi = zendeskApi;
        }

        public MessageResult ProcessTimerTick()
        {
            var trackedTickets = m_Persistence.ReadList("tracked-zd-tickets");
            var comparison = trackedTickets.Select(x => new
            {
                channel = x.Channel, oldValue = x.Ticket, newValue = m_ZendeskApi.FromId(x.Ticket.Id).Result
            });
            var different = comparison.Where(x =>
                x.oldValue.Status != x.newValue.Status ||
                x.oldValue.CommentCount != x.newValue.CommentCount ||
                x.oldValue.Description != x.newValue.Description
                );
            var responses = different.Select(x => new Response(
                string.Format("Ticket <https://redgatesupport.zendesk.com/agent/tickets/{0}|ZD#{0}> was updated",
                    x.oldValue.Id), x.channel));
            
            return new MessageResult(responses.ToList());
        }

        public MessageResult ProcessMessage(Message message)
        {
            string toTrack;
            if (m_CommandParser.TryGetCommand(message, "track", out toTrack) && s_ZendeskIdRegex.IsMatch(toTrack))
            {
                var ticket = m_ZendeskApi.FromId(toTrack.Substring(3)).Result;
                m_Persistence.AddToList("tracked-zd-tickets", new TrackedTicket(ticket, message.Channel));
                return new MessageResult(new[] {Response.ToMessage(message, FormatNowTrackingMessage(toTrack))});
            }
            return MessageResult.Empty;
        }

        private static string FormatNowTrackingMessage(string toTrack)
        {
            return string.Format("Now tracking {0}. To stop tracking, use `scbot untrack {0}`", toTrack);
        }
    }
}