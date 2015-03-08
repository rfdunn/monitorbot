﻿using scbot.core.bot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace scbot.rg
{
    public class Webcams : IMessageProcessor
    {
        private readonly RegexCommandMessageProcessor m_Underlying;
        private readonly string m_User;

        private readonly string m_Pass;

        public Webcams(ICommandParser commandParser, string webcamAuth)
        {
            m_Underlying = new RegexCommandMessageProcessor(commandParser, Commands);
            var userPass = webcamAuth.Split(new[] { ':' }, 2);
            m_User = userPass[0];
            m_Pass = userPass[1];
        }

        public Dictionary<Regex, Func<Message, Match, MessageResult>> Commands
        {
            get
            {
                return new Dictionary<Regex, Func<Message, Match, MessageResult>>
                {
                    { new Regex(@"cafcam|lunch|food"), PostCafcam },
                    { new Regex(@"foos|fooscam"), PostFooscam },
                };
            }
        }

        private MessageResult PostCafcam(Message message, Match args)
        {
            return Response.ToMessage(message, string.Format("<http://10.120.115.227/snapshot.cgi?loginuse={0}&loginpas={1}|{2}>", m_User, m_Pass, LunchMessage()));
        }

        private MessageResult PostFooscam(Message message, Match args)
        {
            return Response.ToMessage(message, string.Format("<http://10.120.115.224/snapshot.cgi?user={0}&pwd={1}&.jpg|{2}>", m_User, m_Pass, FoosMessage()));
        }

        private string FoosMessage()
        {
            return "How about a nice friendly game of foosball?";
        }

        private static string LunchMessage()
        {
            return LunchMessage(DateTime.Now.TimeOfDay);
        }

        internal static string LunchMessage(TimeSpan timeOfDay)
        {
            var hours = timeOfDay.TotalHours;
            if (hours < 11)
            {
                return "Patience, young padawan. It is not time for lunch yet.";
            }
            if (hours < 12)
            {
                return "It's nearly lunchtime!";
            }
            if (hours < 13)
            {
                return "Lunchtime is nearly over! There's probably some food left ...";
            }
            if (hours < 24)
            {
                return "Alas, there is no more lunch for today.";
            }
            return "I'm not sure ..";
        }

        public MessageResult ProcessMessage(Message message)
        {
            return m_Underlying.ProcessMessage(message);
        }

        public MessageResult ProcessTimerTick()
        {
            return m_Underlying.ProcessTimerTick();
        }
    }
}
