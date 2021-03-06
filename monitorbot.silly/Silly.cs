﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using monitorbot.core.bot;
using monitorbot.core.utils;

namespace monitorbot.silly
{
    public class Silly : ICommandProcessor
    {
        public static IFeature Create(ICommandParser commandParser, IWebClient webClient)
        {
            return new BasicFeature("silly",
                "get a random quote, class name, gif, etc",
                "use `quote`, `class name`, or `giphy <search>` to find something interesting",
                new HandlesCommands(commandParser, new Silly(webClient)));
        }

        private readonly RegexCommandMessageProcessor m_Underlying;
        private readonly IWebClient m_WebClient;
        private readonly Random m_Random = new Random();

        public Silly(IWebClient webclient)
        {
            m_Underlying = new RegexCommandMessageProcessor(Commands);
            m_WebClient = webclient;
        }

        public Dictionary<string, MessageHandler> Commands
        {
            get
            {
                return new Dictionary<string, MessageHandler>
                {
                    { "quote", Quote },
                    { "class name|name (a )?class", ClassName },
                    { "giphy (?<search>.*)", Giphy},
                    //{ new Regex(@"method name|name (a )?method"), MethodName },
                };
            }
        }

        private MessageResult Quote(Command message, Match args)
        {
            var categories = String.Join("+", new[] { "computers", "fortunes", "technology", "computation", "science" });
            var url = "http://www.iheartquotes.com/api/v1/random?format=json&max_lines=5&source="+categories;
            var quoteResult = m_WebClient.DownloadJson(url).Result;
            return Response.ToMessage(message, string.Format("{1}\n<{0}|[source]>", quoteResult.link, quoteResult.quote));
        }

        private MessageResult ClassName(Command message, Match args)
        {
            var className = m_WebClient.DownloadString("http://www.classnamer.com/index.txt?generator=spring").Result;
            return Response.ToMessage(message, string.Format("How about {0}?", className));
        }

        private MessageResult Giphy(Command message, Match args)
        {
            var search = args.Group("search");
            var giphyResponse =
                m_WebClient.DownloadJson(string.Format(
                    "http://api.giphy.com/v1/gifs/random?tag={0}&api_key=dc6zaTOxFJmzC&rating=pg", 
                    HttpUtility.UrlEncode(search))).Result;
            if (giphyResponse.data.image_url == null)
            {
                return Response.ToMessage(message, string.Format("No results found for '{0}'", search));
            }
            var url = giphyResponse.data.image_url.Replace("giphy.gif", "200.gif");
            return Response.ToMessage(message, string.Format("<{0}|{1}>", url, search));
        }

        public MessageResult ProcessCommand(Command message)
        {
            return m_Underlying.ProcessCommand(message);
        }

        public MessageResult ProcessTimerTick()
        {
            return m_Underlying.ProcessTimerTick();
        }
    }
}
