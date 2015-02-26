﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using scbot.bot;
using scbot.processors.html;
using scbot.processors.jira;
using scbot.processors.meta;
using scbot.processors.notes;
using scbot.processors.zendesk;
using scbot.services;
using scbot.services.html;
using scbot.services.jira;
using scbot.services.notes;
using scbot.services.persistence;
using scbot.services.teamcity;
using scbot.services.zendesk;
using scbot.slack;
using scbot.utils;


namespace scbot
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            MainAsync().Wait();
        }

        private static async Task MainAsync()
        {
            var htmlDomainBlacklist = new[]
            {"jira", "jira.red-gate.com", "rg-jira01", "rg-jira01.red-gate.com", "redgatesupport.zendesk.com", "slack.com"};
            var persistence = new JsonFileKeyValueStore(new FileInfo("scbot.db.json"));

            var slackApi = new SlackApi(Configuration.SlackApiKey);
            var slackRtmConnection = ReconnectingSlackRealTimeMessaging.CreateAsync(
                async () => await slackApi.StartRtm());

            var teamcityApi = new TeamcityBuildApi(new JsonProxyTeamcityApi(Configuration.TeamcityApiBase));

            var time = new Time();
            var jiraApi = new CachedJiraApi(time, new JiraApi());
            var zendeskApiConnection = ReconnectingZendeskApi.CreateAsync(
                    async () => await ZendeskApi.CreateAsync(Configuration.RedgateId));
            var zendeskApi = new ErrorCatchingZendeskTicketApi(
                new ZendeskTicketApi(new CachedZendeskApi(time, await zendeskApiConnection)));

            var slackRtm = await slackRtmConnection;

            var commandParser = new SlackCommandParser("scbot", slackRtm.BotId);

            var tcWebHooksProcessor = TeamcityWebhooksMessageProcessor.Start(persistence, commandParser, "http://*:51307");

            var processor =
                new ErrorCatchingMessageProcessor(
                    new ConcattingMessageProcessor(
                        new CompositeMessageProcessor(
                            new NoteProcessor(commandParser, new NoteApi(persistence)),
                            new JiraBugProcessor(jiraApi),
                            new ZendeskTicketProcessor(zendeskApi),
                            new ZendeskTicketTracker(commandParser, persistence, zendeskApi),
                            new HtmlTitleProcessor(new HtmlTitleParser(), htmlDomainBlacklist),
                            //new TeamcityBuildTracker(commandParser, persistence, teamcityApi),
                            tcWebHooksProcessor)));

            var bot = new Bot(processor);

            var handler = new SlackMessageHandler(bot, slackRtm.BotId);
            var cancellationToken = new CancellationToken();

            MainLoop(slackRtm, handler, slackApi, cancellationToken);
        }

        private static void MainLoop(ISlackRealTimeMessaging slackRtm, SlackMessageHandler handler, SlackApi slackApi, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var nextMessage = slackRtm.Receive(cancellationToken);
                while (!nextMessage.Wait(TimeSpan.FromSeconds(10)))
                {
                    var tickResult = handler.HandleTimerTick();
                    Console.Write(".");
                    DoResponse(slackApi, tickResult, cancellationToken);
                }

                var messageResult = handler.Handle(nextMessage.Result);
                DoResponse(slackApi, messageResult, cancellationToken);
            }
        }

        private static void DoResponse(SlackApi slackApi, MessageResult result, CancellationToken cancellationToken)
        {
            foreach (var response in result.Responses)
            {
                slackApi.PostMessage(response).Wait(cancellationToken);
            }
        }
    }
}
